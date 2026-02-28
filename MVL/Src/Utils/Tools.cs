using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CSemVer;
using Flurl.Http;
using Godot;
using Nerdbank.MessagePack;
using FileAccess = Godot.FileAccess;

namespace MVL.Utils;

public static class Tools {
	public static readonly Version VSRunTargetFramework = Version.Parse("10.0");
	static private readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();
	public static MessagePackSerializer PackSerializer { get; } = new();
	public static bool IsEditorHint { get; } = Engine.IsEditorHint();

	public static void RichTextOpenUrl(Variant url) { Task.Run(() => OS.ShellOpen(url.AsString())); }

	public static float GetAutoDisplayScale() {
#if GODOT_LINUXBSD
		if (DisplayServer.GetName() == "Wayland") {
			var mainWindowScale = DisplayServer.ScreenGetScale();

			if (DisplayServer.GetScreenCount() == 1 || Fract(mainWindowScale) != 0) {
				return mainWindowScale;
			}

			return DisplayServer.ScreenGetMaxScale();
		}
#endif
		var screen = DisplayServer.WindowGetCurrentScreen();

		if (DisplayServer.ScreenGetSize(screen) == default) {
			return 1;
		}
#if GODOT_WINDOWS
		return DisplayServer.ScreenGetDpi(screen) / 96.0f;
#else
		var size = DisplayServer.ScreenGetSize(screen);
		var smallestDimension = Math.Min(size.X, size.Y);
		if (DisplayServer.ScreenGetDpi(screen) >= 192 && smallestDimension >= 1400) {
			return 2;
		}

		return smallestDimension switch { >= 1700 => 1.5f, <= 800 => 0.75f, _ => 1.0f };
#endif
	}

	public static double Fract(double value) {
		if (double.IsInfinity(value) || double.IsNaN(value)) {
			return 0;
		}

		return value - Math.Floor(value);
	}

	public static async Task<bool> HasRequiredDotNetVersionInstalledAsync(Version targetFrameworkVersion) {
		try {
			if (await IsDotNetX64Async()) {
				return await HasMatchingRuntimeAsync(targetFrameworkVersion);
			}

			Log.Info("检测到DotNet，但其架构不是x64");
			return false;
		} catch (Exception e) {
			Log.Error("检查DotNet环境失败", e);
			return false;
		}
	}

	static private async Task<bool> IsDotNetX64Async() {
		var output = await ExecuteDotNetCommandAsync("--info");

		return !string.IsNullOrEmpty(output) && output.Contains("Architecture: x64", StringComparison.OrdinalIgnoreCase);
	}

	static private async Task<bool> HasMatchingRuntimeAsync(Version targetFrameworkVersion) {
		using var process = CreateDotNetProcess("--list-runtimes");
		process.Start();

		var has = false;
		while (await process.StandardOutput.ReadLineAsync() is { } line) {
			Log.Debug($"检测到运行时: {line}");
			if (!line.Contains("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2) {
				continue;
			}

			var versionString = parts[1];

			if (!SVersion.TryParse(versionString, out var installedVersion)) {
				continue;
			}

			if (installedVersion.Major < targetFrameworkVersion.Major) {
				continue;
			}

			Log.Debug($"检测到可用的运行时: {parts[0]} {versionString}");
			has = true;
		}

		await process.WaitForExitAsync();
		return has;
	}

	static private async Task<string?> ExecuteDotNetCommandAsync(string arguments) {
		using var process = CreateDotNetProcess(arguments);
		process.Start();

		var output = await process.StandardOutput.ReadToEndAsync();
		await process.WaitForExitAsync();

		if (process.ExitCode == 0) {
			return output;
		}

		Log.Debug($"'dotnet {arguments}'返回了退出代码 {process.ExitCode}");
		return null;
	}

	static private Process CreateDotNetProcess(string arguments) {
		return new() {
			StartInfo = new() {
				FileName = "dotnet",
				Arguments = arguments,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			}
		};
	}

	public static ImageTexture? CreateTextureFromBytes(ReadOnlySpan<byte> iconBytes, ImageFormat format = ImageFormat.Png) {
		using var image = new Image();
		switch (format) {
			case ImageFormat.Png:
				image.LoadPngFromBuffer(iconBytes);
				break;
			case ImageFormat.Jpg:
				image.LoadJpgFromBuffer(iconBytes);
				break;
			case ImageFormat.Bmp:
				image.LoadBmpFromBuffer(iconBytes);
				break;
			case ImageFormat.Webp:
				image.LoadWebpFromBuffer(iconBytes);
				break;
			case ImageFormat.Unknown:
			default:
				throw new NotSupportedException($"不支持的图像格式: {format}");
		}

		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	static private readonly byte[] Bmp = "BM"u8.ToArray();
	static private readonly byte[] Webp = "RIFF"u8.ToArray();
	static private readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
	static private readonly byte[] Jpeg = [0xFF, 0xD8];
	static private readonly byte[] Jpeg2 = [0xFF, 0xD9];

	public static ImageFormat GetImageFormat(ReadOnlySpan<byte> fileHeader) {
		return fileHeader switch {
			_ when fileHeader.StartsWith(Bmp) => ImageFormat.Bmp,
			_ when fileHeader.StartsWith(Png) => ImageFormat.Png,
			_ when fileHeader.StartsWith(Jpeg) && fileHeader.EndsWith(Jpeg2) => ImageFormat.Jpg,
			_ when fileHeader.StartsWith(Webp) => ImageFormat.Webp,
			_ => ImageFormat.Unknown,
		};
	}

	public static async Task<ImageTexture?> LoadTextureFromPath(string path) {
		var fileInfo = new FileInfo(path);
		return await LoadTextureFromPath(fileInfo);
	}

	public static async Task<ImageTexture?> LoadTextureFromPath(FileInfo fileInfo) {
		if (!fileInfo.Exists || fileInfo.Length == 0) {
			return null;
		}

		return await Task.Run(async () => {
			var path = fileInfo.FullName;
			var length = (int)fileInfo.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			try {
				var fileSpan = buffer.AsSpan(0, length);
				await using var fs = fileInfo.OpenRead();
				fs.ReadExactly(fileSpan);

				Span<byte> hashBytes = stackalloc byte[32];
				CryptographicOperations.HashData(HashAlgorithmName.SHA256, fileSpan, hashBytes);
				var fileSha256 = Convert.ToHexStringLower(hashBytes);
				var shaPath = $"{path}.{fileSha256}";
				if (ResourceLoader.Exists(shaPath)) {
					return ResourceLoader.Load<ImageTexture>(shaPath);
				}

				var format = GetImageFormat(fileSpan);
				if (format == ImageFormat.Unknown) {
					return null;
				}

				var texture = CreateTextureFromBytes(fileSpan, format);
				texture?.TakeOverPath(path);
				return texture;
			} catch (Exception e) {
				Log.Warn($"加载贴图失败: {path}", e);
				return null;
			} finally {
				ArrayPool<byte>.Shared.Return(buffer);
			}
		});
	}

	public static async Task<ImageTexture?> LoadTextureFromUrl(string url) {
		return await Task.Run(async () => {
			try {
				var name = $"{new Guid(url.Sha256Buffer().Take(16).ToArray())}.png";
				var path = Path.Join(Paths.CacheFolder, name);
				if (File.Exists(path)) {
					return await LoadTextureFromPath(path);
				}

				Log.Trace($"正在从 {url} 下载贴图...");
				var buffer = await url.GetBytesAsync();

				var format = GetImageFormat(buffer);
				if (format == ImageFormat.Unknown) {
					Log.Warn($"无法识别 {url} 的格式");
					return null;
				}

				var texture = CreateTextureFromBytes(buffer, format);
				if (texture is null) {
					return null;
				}

				texture.GetImage().SavePng(path);
				Log.Trace($"已保存 {url} 为 {name}");

				var fileSha256 = FileAccess.GetSha256(path);
				var shaPath = $"{path}.{fileSha256}";
				texture.TakeOverPath(shaPath);

				return texture;
			} catch (Exception e) {
				Log.Warn("下载贴图失败", e);
				return null;
			}
		});
	}

	public static ushort GetAvailablePort() {
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return (ushort)port;
	}

	public static bool PortIsAvailable(ushort port) {
		try {
			using var listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			listener.Stop();
			return true;
		} catch {
			return false;
		}
	}

	public static (double Value, string Unit) GetSizeAndUnit(ulong bytes) {
		if (bytes == 0) {
			return (0, Units[0]);
		}

		var index = (64 - BitOperations.LeadingZeroCount(bytes) - 1) / 10;

		if (index >= Units.Length) {
			index = Units.Length - 1;
		}

		var value = (double)bytes / (1L << index * 10);

		return (value, Units[index]);
	}
}
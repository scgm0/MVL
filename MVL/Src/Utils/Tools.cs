using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;
using Nerdbank.MessagePack;
using FileAccess = Godot.FileAccess;

namespace MVL.Utils;

public static class Tools {
	public const string VSRunDirPath = "res://Misc/VSRun";

	static private readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

	public static ulong MainThreadId { get; } = OS.GetMainThreadId();
	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();
	public static MessagePackSerializer PackSerializer { get; } = new();
	public static bool IsEditorHint { get; } = Engine.IsEditorHint();
	public static string UserAgent { get; } = $"MVL/{BuildInfo.InformationalVersion} ({OS.GetName()} {SystemInfo.OSVersion}; {RuntimeInformation.OSArchitecture})";
	public static string DotNetPath { get; } = GetDotNetExecutablePath();

	public static string GetDotNetExecutablePath() {
#if GODOT_WINDOWS
		var programFiles = System.Environment.GetEnvironmentVariable("ProgramW6432")
			?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
		var x64Path = Path.Combine(programFiles, "dotnet", "dotnet.exe");

		if (File.Exists(x64Path)) {
			return x64Path;
		}
#endif

		return "dotnet";
	}

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

		return ImageTexture.CreateFromImage(image);
	}

	static private ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47];
	static private ReadOnlySpan<byte> JpegStart => [0xFF, 0xD8];
	static private ReadOnlySpan<byte> JpegEnd => [0xFF, 0xD9];

	public static ImageFormat GetImageFormat(ReadOnlySpan<byte> fileHeader) {
		return fileHeader switch {
			_ when fileHeader.StartsWith("BM"u8) => ImageFormat.Bmp,
			_ when fileHeader.StartsWith(PngSignature) => ImageFormat.Png,
			_ when fileHeader.StartsWith(JpegStart) && fileHeader.EndsWith(JpegEnd) => ImageFormat.Jpg,
			_ when fileHeader.StartsWith("RIFF"u8) => ImageFormat.Webp,
			_ => ImageFormat.Unknown,
		};
	}

	public static async Task<ImageTexture?> LoadTextureFromStream(Stream stream, int length = -1) {
		try {
			if (length <= 0 && stream.CanSeek) {
				length = (int)(stream.Length - stream.Position);
			}

			ImageFormat format;
			if (length > 0) {
				const int poolLimit = 128 * 1024;
				var usePool = length <= poolLimit;
				var buffer = usePool
					? ArrayPool<byte>.Shared.Rent(length)
					: GC.AllocateUninitializedArray<byte>(length);
				var memoryBuffer = buffer.AsMemory(0, length);

				try {
					await stream.ReadExactlyAsync(memoryBuffer);
					format = GetImageFormat(memoryBuffer.Span);
					return format == ImageFormat.Unknown ? null : CreateTextureFromBytes(memoryBuffer.Span, format);
				} finally {
					if (usePool) {
						ArrayPool<byte>.Shared.Return(buffer);
					}
				}
			}

			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);

			if (ms.Length == 0) {
				return null;
			}

			var span = new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length);

			format = GetImageFormat(span);
			return format == ImageFormat.Unknown ? null : CreateTextureFromBytes(span, format);
		} catch (Exception e) {
			Log.Error(e);
			return null;
		}
	}

	public static async Task<ImageTexture?> LoadTextureFromPath(string path) {
		var fileInfo = new FileInfo(path);
		return await LoadTextureFromPath(fileInfo);
	}

	static private readonly ConcurrentDictionary<string, (long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc, string Sha256)> FileSha256Cache = new(StringComparer.Ordinal);

	static private async Task<string> GetFileSha256Async(FileInfo fileInfo) {
		fileInfo.Refresh();
		var fullName = fileInfo.FullName;
		if (FileSha256Cache.TryGetValue(fullName, out var cached) &&
			cached.Length == fileInfo.Length &&
			cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
			cached.CreationTimeUtc == fileInfo.CreationTimeUtc) {
			return cached.Sha256;
		}

		await using var fs = fileInfo.OpenRead();
		var hash = new byte[32];
		await CryptographicOperations.HashDataAsync(HashAlgorithmName.SHA256, fs, hash);
		var sha256 = Convert.ToHexStringLower(hash);
		FileSha256Cache[fullName] = (fileInfo.Length, fileInfo.LastWriteTimeUtc, fileInfo.CreationTimeUtc, sha256);
		return sha256;
	}

	public static async Task<ImageTexture?> LoadTextureFromPath(FileInfo fileInfo) {
		if (!fileInfo.Exists || fileInfo.Length == 0) {
			return null;
		}

		var path = fileInfo.FullName;
		try {
			var fileSha256 = await GetFileSha256Async(fileInfo);
			var shaPath = $"{path}.{fileSha256}";
			if (ResourceLoader.Exists(shaPath)) {
				return ResourceLoader.Load<ImageTexture>(shaPath);
			}

			await using var fs = fileInfo.OpenRead();
			var texture = await LoadTextureFromStream(fs);
			texture?.TakeOverPath(shaPath);
			return texture;
		} catch (Exception e) {
			Log.Warn($"加载贴图失败: {path}", e);
			return null;
		}
	}

	public static async Task<ImageTexture?> LoadTextureFromUrl(string url) {
		try {
			var hash = url.Sha256Buffer();
			var name = $"{new Guid(hash.AsSpan(0, 16))}.png";
			var path = Path.Join(Paths.CacheFolder, name);
			if (File.Exists(path)) {
				return await LoadTextureFromPath(path);
			}

			Log.Trace($"正在从 {url} 下载图像...");
			var stream = await url.GetStreamAsync();

			var texture = await LoadTextureFromStream(stream);
			if (texture is null) {
				Log.Warn($"从 {url} 获取图像失败");
				return null;
			}

			texture.GetImage().SavePng(path);
			Log.Trace($"已保存 {url} 为 {name}");

			var fileSha256 = FileAccess.GetSha256(path);
			var shaPath = $"{path}.{fileSha256}";
			texture.TakeOverPath(shaPath);

			return texture;
		} catch (Exception e) {
			Log.Warn($"下载图像失败: {url}", e);
			return null;
		}
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

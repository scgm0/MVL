using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;
using Nerdbank.MessagePack;
using FileAccess = Godot.FileAccess;
using Process = System.Diagnostics.Process;

namespace MVL.Utils;

public static class Tools {
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

	public static async Task<bool> HasRequiredDotNetVersionInstalled(
		string targetFrameworkName,
		Version targetFrameworkVersion) {
		try {
			using var process = new Process();
			process.StartInfo = new() {
				FileName = "dotnet",
				Arguments = "--list-runtimes",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			process.Start();
			await process.WaitForExitAsync();

			if (process.ExitCode != 0) {
				return false;
			}

			while (await process.StandardOutput.ReadLineAsync() is { } line) {
				GD.Print(line);
				if (!line.Contains(targetFrameworkName, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				var runtimeInfo = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (runtimeInfo.Length < 2) {
					continue;
				}

				var versionString = runtimeInfo[1];
				if (Version.TryParse(versionString, out var installedVersion) &&
					installedVersion.Major == targetFrameworkVersion.Major) {
					return true;
				}
			}

			return false;
		} catch (Exception e) {
			GD.PrintErr(e);
			return false;
		}
	}

	public static ImageTexture CreateTextureFromBytes(byte[] iconBytes, ImageFormat format = ImageFormat.Png) {
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
				throw new NotSupportedException($"Unsupported image format: {format}");
		}

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

	public static ImageTexture? LoadTextureFromPath(string path) {
		if (!File.Exists(path)) {
			return null;
		}

		var fileSha256 = FileAccess.GetSha256(path);
		var shaPath = $"{path}.{fileSha256}";
		if (ResourceLoader.Exists(shaPath)) {
			return ResourceLoader.Load<ImageTexture>(shaPath);
		}

		var buffer = FileAccess.GetFileAsBytes(path);
		var format = GetImageFormat(buffer);
		if (format == ImageFormat.Unknown) {
			return null;
		}

		var texture = CreateTextureFromBytes(buffer, format);
		texture.TakeOverPath(path);
		return texture;
	}

	public static async Task<ImageTexture?> LoadTextureFromUrl(string url) {
		try {
			var path = Path.Join(Paths.CacheFolder, $"{new Guid(url.Sha256Buffer().Take(16).ToArray())}.png");
			if (File.Exists(path)) {
				return LoadTextureFromPath(path);
			}

			GD.Print(url);
			var buffer = await url.GetBytesAsync();

			var format = GetImageFormat(buffer);
			if (format == ImageFormat.Unknown) {
				return null;
			}

			var texture = CreateTextureFromBytes(buffer, format);
			texture.GetImage().SavePng(path);

			var fileSha256 = FileAccess.GetSha256(path);
			var shaPath = $"{path}.{fileSha256}";
			texture.TakeOverPath(shaPath);

			return texture;
		} catch (Exception e) {
			GD.PrintErr(e);
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
}
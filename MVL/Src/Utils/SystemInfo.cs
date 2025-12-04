using System.IO;
using System.Runtime.InteropServices;
using Godot;
using Environment = System.Environment;

namespace MVL.Utils;

public static class SystemInfo {
#if GODOT_WINDOWS
	public static string OSDescription => field ??= $"{OS.GetName()} {OS.GetVersionAlias()} {RuntimeInformation.OSArchitecture}";
#elif GODOT_LINUXBSD
	public static string OSDescription =>
		field ??=
			$"{RuntimeInformation.OSDescription} {File.ReadAllText("/proc/sys/kernel/osrelease").Trim()} {RuntimeInformation.OSArchitecture}";
#endif
	public static string ProcessorName => field ??= OS.GetProcessorName();
	public static int ProcessorCount => Environment.ProcessorCount;
	public static string GraphicsCardDescription => field ??= RenderingServer.GetVideoAdapterName();
	public static string GraphicsCardVendor => field ??= RenderingServer.GetVideoAdapterVendor();
	public static string GraphicsCardVersion => field ??= string.Join(" ", OS.GetVideoAdapterDriverInfo());
}
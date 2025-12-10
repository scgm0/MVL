using System;
using System.IO;
using System.Runtime.InteropServices;
using Godot;
using Environment = System.Environment;

namespace MVL.Utils;

// ReSharper disable once PartialTypeWithSinglePart
public static partial class SystemInfo {
#if GODOT_WINDOWS
	public static string OSDescription => field ??=
 $"{OS.GetName()} {OS.GetVersionAlias()} {RuntimeInformation.OSArchitecture}";
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
#if GODOT_LINUXBSD
	public static ulong TotalPhysicalMemory => GetBytesFromLine("MemTotal:");
	public static ulong AvailablePhysicalMemory => GetBytesFromLine("MemAvailable:");

	static private ulong GetBytesFromLine(string token) {
		var lines = File.ReadAllLines("/proc/meminfo");
		foreach (var line in lines) {
			if (!line.StartsWith(token)) {
				continue;
			}

			var str = line[token.Length..];
			if (str.EndsWith("kB") && ulong.TryParse(str.AsSpan(0, str.Length - "kB".Length), out var result)) {
				return result * 1024UL;
			}
		}

		throw new();
	}

#elif GODOT_WINDOWS
	public static ulong TotalPhysicalMemory => GetMemoryStatus().ullTotalPhys;
	public static ulong AvailablePhysicalMemory => GetMemoryStatus().ullAvailPhys;

	static private MEMORYSTATUSEX GetMemoryStatus() {
		var msex = new MEMORYSTATUSEX();
		msex.Init();
		return !GlobalMemoryStatusEx(ref msex)
			? throw new MarshalDirectiveException($"获取内存信息失败. Error Code: {Marshal.GetLastPInvokeError()}")
			: msex;
	}

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static private partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private record struct MEMORYSTATUSEX {
		public uint dwLength;
		public uint dwMemoryLoad;
		public ulong ullTotalPhys;
		public ulong ullAvailPhys;
		public ulong ullTotalPageFile;
		public ulong ullAvailPageFile;
		public ulong ullTotalVirtual;
		public ulong ullAvailVirtual;
		public ulong ullAvailExtendedVirtual;
		public void Init() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); }
	}
#endif
}
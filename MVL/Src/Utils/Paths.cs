using Godot;
using MVL.Utils.Extensions;

namespace MVL.Utils;

public static class Paths {
	public static string BaseConfigPath { get; } = OS.GetUserDataDir().PathJoin("data.json").NormalizePath();
	public static string ModpackFolder { get; } = OS.GetUserDataDir().PathJoin("Modpack").NormalizePath();
	public static string ReleaseFolder { get; } = OS.GetUserDataDir().PathJoin("Release").NormalizePath();
	public static string LockFile { get; } = OS.GetTempDir().PathJoin("MVL-Lock").NormalizePath();
	public static string PortFile { get; } = OS.GetTempDir().PathJoin("MVL-Port").NormalizePath();
	static Paths() { EnsureFolderExists(); }

	public static void EnsureFolderExists() {
		if (!DirAccess.DirExistsAbsolute(ModpackFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(ModpackFolder);
		}

		if (!DirAccess.DirExistsAbsolute(ReleaseFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(ReleaseFolder);
		}
	}
}
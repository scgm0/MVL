using Godot;
using MVL.Utils.Extensions;

namespace MVL.Utils;

public static class Paths {
	public static string BaseConfigPath { get; } = OS.GetUserDataDir().PathJoin("data.json").NormalizePath();
	public static string ModpackFolder { get; } = OS.GetUserDataDir().PathJoin("Modpack").NormalizePath();
	public static string ReleaseFolder { get; } = OS.GetUserDataDir().PathJoin("Release").NormalizePath();
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
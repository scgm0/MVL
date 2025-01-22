using Godot;

namespace MVL.Utils;

public static class Paths {
	public static string BaseConfigPath { get; } = OS.GetUserDataDir().PathJoin("data.json");
	public static string ModpackFolder { get; } = OS.GetUserDataDir().PathJoin("Modpack");

	static Paths() { EnsureFolderExists(); }

	public static void EnsureFolderExists() {
		if (!DirAccess.DirExistsAbsolute(ModpackFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(ModpackFolder);
		}
	}
}
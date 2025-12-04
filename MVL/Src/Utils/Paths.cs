using Godot;
using MVL.Utils.Extensions;

namespace MVL.Utils;

public static class Paths {
	public static string AppDataFolder { get; } = OS.GetUserDataDir().NormalizePath();
	public static string BaseConfigPath { get; } = AppDataFolder.PathJoin("data.json").NormalizePath();
	public static string ModpackFolder { get; } = AppDataFolder.PathJoin("Modpack").NormalizePath();
	public static string ReleaseFolder { get; } = AppDataFolder.PathJoin("Release").NormalizePath();
	public static string CacheFolder { get; } = AppDataFolder.PathJoin(".Cache").NormalizePath();
	public static string LogFolder { get; } = AppDataFolder.PathJoin("logs").NormalizePath();
	public static string LockFile { get; } = OS.GetTempDir().PathJoin("MVL-Lock").NormalizePath();
	public static string PortFile { get; } = OS.GetTempDir().PathJoin("MVL-Port").NormalizePath();
	public static string OverrideConfigPath { get; } = AppDataFolder.PathJoin("override.cfg").NormalizePath();
	public static string EasyTierFolder { get; } = AppDataFolder.PathJoin("EasyTier").NormalizePath();
	static Paths() { EnsureFolderExists(); }

	static private void EnsureFolderExists() {
		if (!DirAccess.DirExistsAbsolute(ModpackFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(ModpackFolder);
		}

		if (!DirAccess.DirExistsAbsolute(ReleaseFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(ReleaseFolder);
		}

		if (!DirAccess.DirExistsAbsolute(CacheFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(CacheFolder);
		}

		if (!DirAccess.DirExistsAbsolute(LogFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(LogFolder);
		}

		if (!DirAccess.DirExistsAbsolute(EasyTierFolder)) {
			DirAccess.MakeDirRecursiveAbsolute(EasyTierFolder);
		}
	}
}
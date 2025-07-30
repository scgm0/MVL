using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MVL.UI;
using MVL.Utils.Game;
using FileAccess = Godot.FileAccess;

namespace MVL.Utils.Config;

public class ModpackConfig {
	private string _configPath = string.Empty;
	public string? Name { get; set; }
	public GameVersion? Version { get; set; }

	public string? ReleasePath {
		get;
		set {
			field = value;
			ReleaseInfo = Main.ReleaseInfos.GetValueOrDefault(field ?? string.Empty);
		}
	}

	public string Command { get; set; } = "%command%";

	public string GameAssembly { get; set; } = "Vintagestory.dll";

	[JsonIgnore]
	public string? Path {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;
			if (field != null) {
				_configPath = System.IO.Path.Combine(field, "modpack.json");
			}
		}
	}

	public ConcurrentDictionary<string, ModInfo> Mods = [];
	public ReleaseInfo? ReleaseInfo;

	public void UpdateMods() {
		Mods.Clear();

		if (string.IsNullOrEmpty(Path)) {
			return;
		}

		var modsPath = System.IO.Path.Combine(Path, "Mods");
		if (!Directory.Exists(modsPath)) {
			Directory.CreateDirectory(modsPath);
		}

		foreach (var entryPath in Directory.EnumerateFileSystemEntries(modsPath)) {
			var modInfo = TryLoadMod(entryPath);
			if (modInfo == null) {
				continue;
			}

			modInfo.ModpackConfig = this;

			if (Mods.TryAdd(modInfo.ModId, modInfo)) {
				continue;
			}

			try {
				var oldModInfo = Mods[modInfo.ModId];
				var version1 = SemVer.Parse(oldModInfo.Version);
				var version2 = SemVer.Parse(modInfo.Version);
				if (version1 > version2) {
					Mods[modInfo.ModId] = oldModInfo;
				} else {
					Mods[modInfo.ModId] = modInfo;
				}
			} catch (Exception ex) {
				GD.PrintErr($"从 {entryPath} 加载 mod 时出错: {ex.Message}");
			}
		}
	}

	static private ModInfo? TryLoadMod(string entryPath) {
		try {
			var attributes = File.GetAttributes(entryPath);

			if ((attributes & FileAttributes.Directory) == FileAttributes.Directory) {
				return ModInfo.FromDirectory(entryPath);
			}

			return System.IO.Path.GetExtension(entryPath).ToLowerInvariant() switch {
				".zip" => ModInfo.FromZip(entryPath), ".dll" => ModInfo.FromAssembly(entryPath), _ => null
			};
		} catch (Exception ex) {
			GD.PrintErr($"从 {entryPath} 加载 mod 时出错: {ex.Message}");
			return null;
		}
	}

	public static ModpackConfig Load(string modpackPath) {
		var configPath = System.IO.Path.Combine(modpackPath, "modpack.json");
		ModpackConfig modPackConfig;
		try {
			if (FileAccess.FileExists(configPath)) {
				modPackConfig = JsonSerializer.Deserialize<ModpackConfig>(FileAccess.GetFileAsString(configPath),
						SourceGenerationContext.Default.ModpackConfig) ??
					new ModpackConfig();
			} else {
				modPackConfig = new();
			}
		} catch {
			modPackConfig = new();
		}

		modPackConfig.Path = modpackPath;
		Save(modPackConfig);
		return modPackConfig;
	}

	public static void Save(ModpackConfig modPackConfig) {
		using var file = FileAccess.Open(modPackConfig._configPath, FileAccess.ModeFlags.Write);
		file.StoreString(JsonSerializer.Serialize(modPackConfig, SourceGenerationContext.Default.ModpackConfig));
		file.Flush();
	}
}
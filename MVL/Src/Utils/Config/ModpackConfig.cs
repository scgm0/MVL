using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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

	public string Command { get; set; }= "%command%";

	public string GameAssembly { get; set; } = "Vintagestory.dll";

	[JsonIgnore]
	public string? Path {
		get;
		set {
			if (field == value) {
				return;
			}

			field = value;
			if (value == null) {
				return;
			}

			Mods = [];
			_configPath = System.IO.Path.Combine(value, "modpack.json");
			var modsPath = System.IO.Path.Combine(value, "Mods");
			if (!Directory.Exists(modsPath)) {
				return;
			}

			foreach (var entry in Directory.GetFiles(modsPath, "*.zip", SearchOption.TopDirectoryOnly)) {
				var modInfo = ModInfo.FromZip(entry);
				if (modInfo != null) {
					Mods[entry] = modInfo;
				}
			}

			foreach (var entry in Directory.GetFiles(modsPath, "*.dll", SearchOption.TopDirectoryOnly)) {
				var modInfo = ModInfo.FromAssembly(entry);
				if (modInfo != null) {
					Mods[entry] = modInfo;
				}
			}

			foreach (var directory in Directory.GetDirectories(modsPath)) {
				var modInfo = ModInfo.FromDirectory(directory);
				if (modInfo != null) {
					Mods[directory] = modInfo;
				}
			}
		}
	}

	public ConcurrentDictionary<string, ModInfo> Mods = [];
	public ReleaseInfo? ReleaseInfo;

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
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MVL.Utils.Game;

namespace MVL.Utils.Config;

public class ModpackConfig {
	private string _configPath = string.Empty;
	public string? Name { get; set; }
	public GameVersion? Version { get; set; }
	public string? ReleasePath { get; set; }

	[JsonIgnore]
	public string? Path {
		get;
		set {
			field = value;
			if (value != null) {
				_configPath = System.IO.Path.Combine(value, "modpack.json");
			}
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
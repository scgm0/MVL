using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace MVL.Utils.Config;

public class BaseConfig {
	private string _configPath = string.Empty;

	public string? ProxyUrl { get; set; }
	public List<string> Release { get; set; } = [];
	public List<string> Modpack { get; set; } = [];

	public static BaseConfig Load(string configPath) {
		BaseConfig baseConfig;
		try {
			if (FileAccess.FileExists(configPath)) {
				baseConfig = JsonSerializer.Deserialize(FileAccess.GetFileAsString(configPath), SourceGenerationContext.Default.BaseConfig) ?? new BaseConfig();
			} else {
				baseConfig = new();
			}
		} catch {
			baseConfig = new();
		}

		baseConfig._configPath = configPath;
		Save(baseConfig);
		return baseConfig;
	}

	public static void Save(BaseConfig baseConfig) {
		using var file = FileAccess.Open(baseConfig._configPath, FileAccess.ModeFlags.Write);
		file.StoreString(JsonSerializer.Serialize(baseConfig, SourceGenerationContext.Default.BaseConfig));
		file.Flush();
	}
}
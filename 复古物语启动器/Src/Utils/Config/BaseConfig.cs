using System.Collections.Generic;
using System.Text.Json;
using Godot;
using 复古物语启动器.Utils.Game;

namespace 复古物语启动器.Utils.Config;

public class BaseConfig {
	private string _configPath = string.Empty;

	public List<ReleaseInfo> Release { get; set; } = [];

	public static BaseConfig Load(string configPath) {
		BaseConfig baseConfig;
		try {
			if (FileAccess.FileExists(configPath)) {
				baseConfig = JsonSerializer.Deserialize(FileAccess.GetFileAsString(configPath), SourceGenerationContext.Default.BaseConfig) as BaseConfig ?? new BaseConfig();
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
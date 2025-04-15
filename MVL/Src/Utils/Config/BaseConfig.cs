using System.Collections.Generic;
using System.Text.Json;
using Godot;
using SharedLibrary;
using Environment = System.Environment;

namespace MVL.Utils.Config;

public class BaseConfig {
	private string _configPath = string.Empty;

	public string CurrentModpack { get; set; } = "";
	public string CurrentAccount { get; set; } = "";
	public string DisplayLanguage { get; set; } = OS.GetLocale();
	public double DisplayScale { get; set; } = Tools.GetAutoDisplayScale();
	public string ProxyAddress { get; set; } = "";
	public int DownloadThreads { get; set; } = Environment.ProcessorCount;
	public string ModpackFolder { get; set; } = Paths.ModpackFolder;
	public string ReleaseFolder { get; set; } = Paths.ReleaseFolder;
	public List<string> Release { get; set; } = [];
	public List<string> Modpack { get; set; } = [];
	public List<Account> Account { get; set; } = [];

	public static BaseConfig Load(string configPath) {
		BaseConfig baseConfig;
		try {
			if (FileAccess.FileExists(configPath)) {
				baseConfig = JsonSerializer.Deserialize(FileAccess.GetFileAsString(configPath),
					SourceGenerationContext.Default.BaseConfig) ?? new BaseConfig();
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
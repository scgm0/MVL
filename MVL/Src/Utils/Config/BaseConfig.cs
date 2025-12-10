using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
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
	public bool MenuExpand { get; set; } = true;
	public string ProxyAddress { get; set; } = "";
	public int DownloadThreads { get; set; } = Environment.ProcessorCount;
	public string ModpackFolder { get; set; } = Paths.ModpackFolder;
	public string ReleaseFolder { get; set; } = Paths.ReleaseFolder;
	public List<string> Release { get; set; } = [];
	public List<string> Modpack { get; set; } = [];
	public List<Account> Account { get; set; } = [];

	static private readonly Lock Lock = new();

	public static BaseConfig Load(string configPath) {
		BaseConfig baseConfig;
		try {
			if (File.Exists(configPath)) {
				using var file = File.OpenRead(configPath);
				baseConfig = JsonSerializer.Deserialize(file,
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
		var data = JsonSerializer.SerializeToUtf8Bytes(baseConfig, SourceGenerationContext.Default.BaseConfig);
		lock (Lock) {
			File.WriteAllBytes(baseConfig._configPath, data);
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using SharedLibrary;
using Environment = System.Environment;

namespace MVL.Utils.Config;

public partial class BaseConfig {
	private const int LastVersion = 1;
	private string _configPath = string.Empty;
	private readonly Lock _syncLock = new();

	public int Version { get; set; }
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

	[JsonExtensionData]
	public Dictionary<string, JsonElement>? ExtensionData { get; set; }

	public void Save() {
		lock (_syncLock) {
			if (string.IsNullOrEmpty(_configPath)) {
				return;
			}

			try {
				if (Version < LastVersion) {
					Version = LastVersion;
				}

				var bytes = JsonSerializer.SerializeToUtf8Bytes(this, SourceGenerationContext.Default.BaseConfig);

				var tempPath = _configPath + ".tmp";

				File.WriteAllBytes(tempPath, bytes);

				File.Move(tempPath, _configPath, overwrite: true);
			} catch (Exception e) {
				Log.Error("保存配置文件失败", e);
			}
		}
	}

	public Task SaveAsync() { return Task.Run(Save); }

	public static BaseConfig Load(string configPath) {
		BaseConfig? baseConfig;
		try {
			if (!File.Exists(configPath)) {
				baseConfig = new() {
					_configPath = configPath,
					Version = LastVersion
				};
				baseConfig.Save();
				Log.Info($"创建配置文件 {configPath}");
				return baseConfig;
			}

			using var fileStream = File.OpenRead(configPath);
			var rootNode = JsonNode.Parse(fileStream);
			if (rootNode is not JsonObject jsonObj) {
				throw new InvalidDataException("配置文件不是有效的 JSON 对象");
			}

			var fileVersion = 0;
			if (jsonObj.TryGetPropertyValue(nameof(Version), out var versionNode) ||
				jsonObj.TryGetPropertyValue("version", out versionNode)) {
				fileVersion = versionNode?.GetValue<int>() ?? 0;
			}

			if (fileVersion == 0) {
				baseConfig = Migrate(jsonObj.Deserialize(SourceGenerationContext.Default.V0) ?? new V0());
			} else {
				baseConfig = jsonObj.Deserialize(SourceGenerationContext.Default.BaseConfig);
			}

			baseConfig ??= new();
		} catch (Exception e) {
			Log.Error($"从 {configPath} 读取配置文件失败，将使用默认配置", e);
			baseConfig = new();
		}

		baseConfig._configPath = configPath;
		baseConfig.Save();
		return baseConfig;
	}

	static private BaseConfig Migrate(V0 config) {
		Log.Info($"正在迁移配置文件 0 -> {LastVersion}");
		return new() {
			Version = LastVersion,
			CurrentModpack = config.CurrentModpack,
			CurrentAccount = config.CurrentAccount,
			DisplayLanguage = config.DisplayLanguage,
			DisplayScale = config.DisplayScale,
			MenuExpand = config.MenuExpand,
			ProxyAddress = config.ProxyAddress,
			DownloadThreads = config.DownloadThreads,
			ModpackFolder = config.ModpackFolder,
			ReleaseFolder = config.ReleaseFolder,
			Release = [..config.Release],
			Modpack = [..config.Modpack],
			Account = [..config.Account]
		};
	}
}
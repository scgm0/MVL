using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CSemVer;
using Godot;
using MVL.UI;
using MVL.Utils.Game;

namespace MVL.Utils.Config;

public class ModpackConfig {
	private readonly record struct ModCacheEntry(ModInfo ModInfo, DateTime LastWriteTimeUtc);

	private string _configPath = string.Empty;
	private readonly ConcurrentDictionary<string, ModCacheEntry> _modInfoCache = new(StringComparer.OrdinalIgnoreCase);
	public string ModpackName { get; set; } = string.Empty;
	public GameVersion? GameVersion { get; set; }
	public SVersion ModpackVersion { get; set; } = SVersion.ZeroVersion;
	public List<string> ModpackAuthors { get; set; } = [];
	public List<string> ModpackTags { get; set; } = [];
	public string ModpackSummary { get; set; } = string.Empty;
	public string ModpackDescription { get; set; } = string.Empty;
	public string ModpackWebsite { get; set; } = string.Empty;
	public string Command { get; set; } = string.Empty;
	public string GameAssembly { get; set; } = string.Empty;
	public string? ReleasePath { get; set; }

	[JsonIgnore]
	public string? Path {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;
			if (field == null) {
				return;
			}

			lock (Lock) {
				_configPath = System.IO.Path.Combine(field, "modpack.json");
			}
		}
	}

	[JsonIgnore]
	public ConcurrentDictionary<string, ModInfo> Mods { get; } = [];

	[JsonIgnore]
	public ReleaseInfo? ReleaseInfo {
		get {
			if (Main.ReleaseInfos.TryGetValue(ReleasePath ?? string.Empty, out var releaseInfo)) {
				return releaseInfo;
			}

			foreach (var info in Main.ReleaseInfos.Values) {
				if (info.Version == GameVersion) {
					return info;
				}
			}

			return null;
		}
	}

	[JsonExtensionData]
	public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];

	public event Action<ModpackConfig>? ModsUpdated;

	public async Task UpdateModsAsync() {
		Log.Debug($"开始更新整合包《{ModpackName}》模组信息");

		if (string.IsNullOrEmpty(Path) || !Directory.Exists(Path)) {
			Log.Warn($"整合包《{ModpackName}》路径为无效，无法更新模组信息");
			return;
		}

		var modsPath = System.IO.Path.Combine(Path, "Mods");
		var dirInfo = new DirectoryInfo(modsPath);

		if (!dirInfo.Exists) {
			dirInfo.Create();
			Log.Warn($"整合包《{ModpackName}》未找到Mods目录，已创建");
		}

		var fileSystemInfos = dirInfo.EnumerateFileSystemInfos().ToArray();

		var existingPaths = new HashSet<string>(
			fileSystemInfos.Select(fsi => fsi.FullName),
			StringComparer.OrdinalIgnoreCase
		);

		foreach (var cachedPath in _modInfoCache.Keys) {
			if (!existingPaths.Contains(cachedPath)) {
				_modInfoCache.TryRemove(cachedPath, out _);
			}
		}

		foreach (var kvp in Mods) {
			if (!existingPaths.Contains(kvp.Value.ModPath)) {
				Mods.TryRemove(kvp.Key, out _);
			}
		}

		await Parallel.ForEachAsync(fileSystemInfos,
			async (fsi, _) => {
				var modInfo = await TryLoadMod(fsi);
				if (modInfo == null) {
					return;
				}

				modInfo.ModpackConfig = this;

				SVersion? newVersion;
				try {
					newVersion = SVersion.Parse(modInfo.Version);
				} catch (Exception ex) {
					Log.Error($"解析{modInfo.ModPath}版本失败: {modInfo.Version}", ex);
					return;
				}

				Mods.AddOrUpdate(
					modInfo.ModId,
					modInfo,
					(_, existingModInfo) => {
						try {
							var oldVersion = SVersion.Parse(existingModInfo.Version);

							return newVersion > oldVersion ? modInfo : existingModInfo;
						} catch (Exception ex) {
							Log.Error($"比较{modInfo.ModPath}和{existingModInfo.ModPath}版本时出错", ex);
							return existingModInfo;
						}
					}
				);
			});
		Log.Debug($"更新整合包《{ModpackName}》模组信息完成，共 {Mods.Count} 个模组");
		ModsUpdated?.Invoke(this);
	}

	public async Task<Texture2D?> GetModpackIconAsync() {
		if (string.IsNullOrEmpty(Path)) {
			return null;
		}

		var iconPaths = Directory.EnumerateFileSystemEntries(Path, "modpackIcon.*", SearchOption.TopDirectoryOnly);
		foreach (var iconPath in iconPaths) {
			var icon = await Tools.LoadTextureFromPath(iconPath);
			if (icon is null) {
				continue;
			}

			return icon;
		}

		return null;
	}

	private async Task<ModInfo?> TryLoadMod(FileSystemInfo fsi) {
		var entryPath = fsi.FullName;
		try {
			var lastWriteTime = fsi.LastWriteTimeUtc;

			if (_modInfoCache.TryGetValue(entryPath, out var cachedEntry) &&
				cachedEntry.LastWriteTimeUtc == lastWriteTime) {
				return cachedEntry.ModInfo;
			}

			var newModInfo = fsi switch {
				DirectoryInfo => await ModInfo.FromDirectory(entryPath),
				FileInfo { Extension: var ext } when ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) =>
					await ModInfo.FromZip(entryPath),
				FileInfo { Extension: var ext } when ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) =>
					ModInfo.FromAssembly(entryPath),
				_ => null
			};

			if (newModInfo != null) {
				var newCacheEntry = new ModCacheEntry(newModInfo, lastWriteTime);
				_modInfoCache[entryPath] = newCacheEntry;
			} else {
				_modInfoCache.TryRemove(entryPath, out _);
			}

			return newModInfo;
		} catch (Exception ex) {
			Log.Error($"从 {entryPath} 加载 mod 时出错", ex);
			_modInfoCache.TryRemove(entryPath, out _);
			return null;
		}
	}

	public void Save() {
		var data = JsonSerializer.SerializeToUtf8Bytes(this, SourceGenerationContext.Default.ModpackConfig);
		lock (Lock) {
			File.WriteAllBytes(_configPath, data);
		}
	}

	static private readonly Lock Lock = new();

	public static async Task<ModpackConfig> Load(string modpackPath) {
		var configPath = System.IO.Path.Combine(modpackPath, "modpack.json");
		ModpackConfig? modPackConfig = null;
		try {
			if (File.Exists(configPath)) {
				var jsonBytes = await File.ReadAllBytesAsync(configPath);
				using var doc = JsonDocument.Parse(jsonBytes);
				var root = doc.RootElement;
				var isV0 = root.TryGetProperty("name", out _) && !root.TryGetProperty("modpackName", out _);
				if (isV0) {
					var v0Config = JsonSerializer.Deserialize(jsonBytes, SourceGenerationContext.Default.ModpackConfigV0);
					if (v0Config != null) {
						modPackConfig = MigrateFromV0(v0Config);
						Log.Debug($"已将旧版配置文件迁移至新格式: {configPath}");
					}
				} else {
					modPackConfig = JsonSerializer.Deserialize(jsonBytes, SourceGenerationContext.Default.ModpackConfig);
				}
			}
		} catch (Exception ex) {
			Log.Error($"加载配置文件失败: {configPath}", ex);
		}

		modPackConfig ??= new();
		modPackConfig.Path = modpackPath;
		modPackConfig.Save();
		return modPackConfig;
	}

	static private ModpackConfig MigrateFromV0(ModpackConfigV0 v0) {
		return new() {
			ModpackName = v0.Name,
			GameVersion = v0.Version,
			ReleasePath = v0.ReleasePath,
			Command = v0.Command.Equals("%command%", StringComparison.OrdinalIgnoreCase) ? string.Empty : v0.Command,
			GameAssembly = v0.GameAssembly.Equals("Vintagestory.dll", StringComparison.OrdinalIgnoreCase)
				? string.Empty
				: v0.GameAssembly,
			ExtensionData = v0.ExtensionData
		};
	}
}
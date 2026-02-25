using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CSemVer;
using MVL.UI;
using MVL.Utils.Game;

namespace MVL.Utils.Config;

public class ModpackConfig {
	private readonly record struct ModCacheEntry(ModInfo ModInfo, DateTime LastWriteTimeUtc);

	private string _configPath = string.Empty;
	private readonly ConcurrentDictionary<string, ModCacheEntry> _modInfoCache = new();
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

	public void UpdateMods() {
		Mods.Clear();

		if (string.IsNullOrEmpty(Path)) {
			return;
		}

		var modsPath = System.IO.Path.Combine(Path, "Mods");
		if (!Directory.Exists(modsPath)) {
			Directory.CreateDirectory(modsPath);
		}

		var existingPaths = new HashSet<string>(Directory.EnumerateFileSystemEntries(modsPath));
		foreach (var cachedPath in _modInfoCache.Keys) {
			if (!existingPaths.Contains(cachedPath)) {
				_modInfoCache.TryRemove(cachedPath, out _);
			}
		}

		Parallel.ForEach(Directory.EnumerateFileSystemEntries(modsPath),
			entryPath => {
				var modInfo = TryLoadMod(entryPath);
				if (modInfo == null) {
					return;
				}

				modInfo.ModpackConfig = this;

				var success = false;
				while (!success) {
					if (Mods.TryGetValue(modInfo.ModId, out var existingModInfo)) {
						try {
							var newVersion = SVersion.Parse(modInfo.Version);
							var oldVersion = SVersion.Parse(existingModInfo.Version);

							if (newVersion > oldVersion) {
								if (Mods.TryUpdate(modInfo.ModId, modInfo, existingModInfo)) {
									success = true;
								}
							} else {
								success = true;
							}
						} catch (Exception ex) {
							Log.Error($"比较 {modInfo.ModId} 版本时出错", ex);
							success = true;
						}
					} else {
						if (Mods.TryAdd(modInfo.ModId, modInfo)) {
							success = true;
						}
					}
				}
			});
	}

	private ModInfo? TryLoadMod(string entryPath) {
		try {
			var lastWriteTime = File.GetLastWriteTimeUtc(entryPath);

			if (_modInfoCache.TryGetValue(entryPath, out var cachedEntry) &&
				cachedEntry.LastWriteTimeUtc == lastWriteTime) {
				return cachedEntry.ModInfo;
			}

			ModInfo? newModInfo = null;
			var attributes = File.GetAttributes(entryPath);

			if ((attributes & FileAttributes.Directory) == FileAttributes.Directory) {
				newModInfo = ModInfo.FromDirectory(entryPath);
			} else {
				if (entryPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
					newModInfo = ModInfo.FromZip(entryPath);
				} else if (entryPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
					newModInfo = ModInfo.FromAssembly(entryPath);
				}
			}

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
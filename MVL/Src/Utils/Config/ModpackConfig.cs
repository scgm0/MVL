using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
	public LocalizedString ModpackName { get; set; } = LocalizedString.Empty;
	public GameVersion? GameVersion { get; set; }
	public SVersion ModpackVersion { get; set; } = SVersion.ZeroVersion;
	public List<string> ModpackAuthors { get; set; } = [];
	public List<string> ModpackTags { get; set; } = [];
	public LocalizedString ModpackSummary { get; set; } = LocalizedString.Empty;
	public LocalizedString ModpackDescription { get; set; } = LocalizedString.Empty;
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

	[JsonIgnore]
	public TranslationDomain TranslationDomain { get; set; } = TranslationServer.GetOrAddDomain("");

	[JsonIgnore]
	public string LocalizeModpackName => GetLocalizedText("modpackName", ModpackName);

	[JsonIgnore]
	public string LocalizeModpackSummary => GetLocalizedText("modpackSummary", ModpackSummary);

	[JsonIgnore]
	public string LocalizeModpackDescription => GetLocalizedText("modpackDescription", ModpackDescription);

	[JsonExtensionData]
	public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];

	public event Action<ModpackConfig>? ModsUpdated;

	public static Texture2D DefaultIcon { get; } = GD.Load<Texture2D>("res://Assets/Icon/VS/gameicon.png");
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

	public async Task<Texture2D> GetModpackIconAsync() {
		if (string.IsNullOrEmpty(Path)) {
			return DefaultIcon;
		}

		var iconPaths = Directory.EnumerateFileSystemEntries(Path, "modpackIcon.*", SearchOption.TopDirectoryOnly);
		foreach (var iconPath in iconPaths) {
			var icon = await Tools.LoadTextureFromPath(iconPath);
			if (icon is null) {
				continue;
			}

			return icon;
		}

		return DefaultIcon;
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

	public string GetLocalizedText(string ctx, LocalizedString key) { return GetLocalizedText(ctx, key.Value); }

	public string GetLocalizedText(string ctx, string key) { return TranslationDomain.Translate(key, ctx); }

	public void Save() {
		lock (Lock) {
			InjectLocalizations(ModpackName, "modpackName");
			InjectLocalizations(ModpackSummary, "modpackSummary");
			InjectLocalizations(ModpackDescription, "modpackDescription");

			var data = JsonSerializer.SerializeToUtf8Bytes(this, SourceGenerationContext.Default.ModpackConfig);
			File.WriteAllBytes(_configPath, data);

			RemoveLocalizations(ModpackName, "modpackName");
			RemoveLocalizations(ModpackSummary, "modpackSummary");
			RemoveLocalizations(ModpackDescription, "modpackDescription");
		}
	}

	private void InjectLocalizations(LocalizedString ls, string baseKey) {
		if (ls.Localizations == null || ls.Localizations.Count == 0) {
			return;
		}

		foreach (var (lang, text) in ls.Localizations) {
			var val = JsonSerializer.SerializeToElement(text, SourceGenerationContext.Default.String);
			ExtensionData[$"{baseKey}[{lang}]"] = val;
		}
	}

	private void RemoveLocalizations(LocalizedString ls, string baseKey) {
		if (ls.Localizations == null || ls.Localizations.Count == 0) {
			return;
		}

		foreach (var (lang, _) in ls.Localizations) {
			ExtensionData.Remove($"{baseKey}[{lang}]");
		}
	}

	private void AddLocalizationTranslation(LocalizedString ls) {
		var localizations = ls.Localizations;
		if (localizations is not { Count: not 0 }) {
			return;
		}

		foreach (var (lang, text) in localizations) {
			var translation = TranslationDomain.HasTranslationForLocale(lang, false)
				? TranslationDomain.FindTranslations(lang, false)[0]
				: new() {
					Locale = lang
				};
			translation.AddMessage(ls.Value, text);
			TranslationDomain.AddTranslation(translation);
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
		modPackConfig.TranslationDomain = TranslationServer.GetOrAddDomain(modpackPath);
		modPackConfig.OnDeserialized();
		modPackConfig.Save();
		return modPackConfig;
	}

	static private ModpackConfig MigrateFromV0(ModpackConfigV0 v0) {
		return new() {
			ModpackName = new(v0.Name),
			GameVersion = v0.Version,
			ReleasePath = v0.ReleasePath,
			Command = v0.Command.Equals("%command%", StringComparison.OrdinalIgnoreCase) ? string.Empty : v0.Command,
			GameAssembly = v0.GameAssembly.Equals("Vintagestory.dll", StringComparison.OrdinalIgnoreCase)
				? string.Empty
				: v0.GameAssembly,
			ExtensionData = v0.ExtensionData
		};
	}

	public void OnDeserialized() {
		TranslationDomain.Clear();

		string[]? keysToRemove = null;
		try {
			if (ExtensionData is { Count: not 0 }) {
				var removeCount = 0;
				foreach (var kvp in ExtensionData) {
					if (kvp.Value.ValueKind != JsonValueKind.String) {
						continue;
					}

					var key = kvp.Key.AsSpan();
					var openBracket = key.IndexOf('[');

					if (openBracket <= 0 || key[^1] is not ']') {
						continue;
					}

					var prefix = key[..openBracket];

					var isName = prefix is "modpackName";
					var isSummary = !isName && prefix is "modpackSummary";
					var isDesc = !isName && !isSummary && prefix is "modpackDescription";

					if (!isName && !isSummary && !isDesc) {
						continue;
					}

					keysToRemove ??= ArrayPool<string>.Shared.Rent(ExtensionData.Count);
					keysToRemove[removeCount++] = kvp.Key;

					var lang = TranslationServer.StandardizeLocale(key.Slice(openBracket + 1, key.Length - openBracket - 2)
						.ToString());
					var val = kvp.Value.GetString() ?? string.Empty;

					if (isName) {
						var ls = ModpackName;
						(ls.Localizations ??= [])[lang] = val;
						ModpackName = ls;
					} else if (isSummary) {
						var ls = ModpackSummary;
						(ls.Localizations ??= [])[lang] = val;
						ModpackSummary = ls;
					} else {
						var ls = ModpackDescription;
						(ls.Localizations ??= [])[lang] = val;
						ModpackDescription = ls;
					}
				}

				if (keysToRemove != null) {
					for (var i = 0; i < removeCount; i++) {
						ExtensionData.Remove(keysToRemove[i]);
					}
				}
			}

			var summary = ModpackSummary;
			var summaryUpdated = false;

			if (NeedsLineEndingReplacement(summary.Value)) {
				summary = summary with { Value = summary.Value.ReplaceLineEndings(string.Empty) };
				summaryUpdated = true;
			}

			if (summary.Localizations != null) {
				foreach (var (key, value) in summary.Localizations) {
					if (NeedsLineEndingReplacement(value)) {
						summary.Localizations[key] = value.ReplaceLineEndings(string.Empty);
					}
				}
			}

			if (summaryUpdated) {
				ModpackSummary = summary;
			}

			AddLocalizationTranslation(ModpackName);
			AddLocalizationTranslation(ModpackSummary);
			AddLocalizationTranslation(ModpackDescription);
		} catch (Exception e) {
			Log.Error($"解析整合包本地化字段失败: {Path}", e);
		} finally {
			if (keysToRemove != null) {
				ArrayPool<string>.Shared.Return(keysToRemove);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static private bool NeedsLineEndingReplacement(string? text) {
		return !string.IsNullOrEmpty(text) && (text.Contains('\n') || text.Contains('\r'));
	}
}
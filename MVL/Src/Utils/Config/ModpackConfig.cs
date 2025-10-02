using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Godot;
using MVL.UI;
using MVL.Utils.Game;
using FileAccess = Godot.FileAccess;

namespace MVL.Utils.Config;

public class ModpackConfig {
	private readonly record struct ModCacheEntry(ModInfo ModInfo, DateTime LastWriteTimeUtc);

	private string _configPath = string.Empty;
	private readonly ConcurrentDictionary<string, ModCacheEntry> _modInfoCache = new();
	public string? Name { get; set; }
	public GameVersion? Version { get; set; }

	public string? ReleasePath {
		get;
		set {
			field = value;
			ReleaseInfo = Main.ReleaseInfos.GetValueOrDefault(field ?? string.Empty);
		}
	}

	public string Command { get; set; } = "%command%";

	public string GameAssembly { get; set; } = "Vintagestory.dll";

	[JsonIgnore]
	public string? Path {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;
			if (field != null) {
				_configPath = System.IO.Path.Combine(field, "modpack.json");
			}
		}
	}

	public ConcurrentDictionary<string, ModInfo> Mods = [];
	public ReleaseInfo? ReleaseInfo;

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
							var newVersion = SemVer.Parse(modInfo.Version);
							var oldVersion = SemVer.Parse(existingModInfo.Version);

							if (newVersion > oldVersion) {
								if (Mods.TryUpdate(modInfo.ModId, modInfo, existingModInfo)) {
									success = true;
								}
							} else {
								success = true;
							}
						} catch (Exception ex) {
							GD.PrintErr($"比较 {modInfo.ModId} 版本时出错: {ex.Message}");
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
			GD.PrintErr($"从 {entryPath} 加载 mod 时出错: {ex.Message}");
			_modInfoCache.TryRemove(entryPath, out _);
			return null;
		}
	}

	public static ModpackConfig Load(string modpackPath) {
		var configPath = System.IO.Path.Combine(modpackPath, "modpack.json");
		ModpackConfig modPackConfig;
		try {
			if (File.Exists(configPath)) {
				using var file = File.OpenRead(configPath);
				modPackConfig = JsonSerializer.Deserialize<ModpackConfig>(file,
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
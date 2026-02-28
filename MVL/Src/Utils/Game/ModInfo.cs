using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CSemVer;
using Godot;
using Mono.Cecil;
using MVL.Utils.Config;
using FileAccess = System.IO.FileAccess;

namespace MVL.Utils.Game;

public record ModInfo : IComparable<ModInfo> {
	public required string Name { get; set; }

	[field: AllowNull, MaybeNull]
	public string ModId { get => field ??= ToModId(Name) ?? string.Empty; set; }

	public string ModPath { get; set; } = "";
	public string Version { get; set; } = "1.0.0";
	public IReadOnlyList<string> Authors { get; set; } = [];
	public string Description { get; set; } = "";

	[JsonConverter(typeof(DependenciesConverter))]
	public IReadOnlyList<ModDependency> Dependencies { get; set; } = [];

	public ModpackConfig? ModpackConfig { get; set; }
	public ModFileType ModFileType { get; set; } = ModFileType.None;

	public const string JsonName = "modinfo.json";
	public const string IconName = "modicon.png";

	public int CompareTo(ModInfo? other) {
		if (other is null) {
			return 1;
		}

		var num = string.CompareOrdinal(ModId, other.ModId);
		return num != 0 ? num : SVersion.Parse(Version).CompareTo(SVersion.Parse(other.Version));
	}

	public async Task<Texture2D?> GetIconAsync() {
		switch (ModFileType) {
			case ModFileType.Assembly or ModFileType.None: {
				break;
			}
			case ModFileType.Zip: {
				await using var fs = new FileStream(ModPath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					4096,
					useAsync: true);
				await using var zipArchive = new ZipArchive(fs, ZipArchiveMode.Read);
				var iconEntry = zipArchive.GetEntry(IconName);

				if (iconEntry == null) {
					foreach (var entry in zipArchive.Entries) {
						if (!string.Equals(entry.Name, IconName, StringComparison.OrdinalIgnoreCase)) {
							continue;
						}

						iconEntry = entry;
						break;
					}
				}

				if (iconEntry == null) {
					return null;
				}

				var path = Path.Combine(ModPath, iconEntry.FullName);
				if (ResourceLoader.Exists(path)) {
					return ResourceLoader.Load<Texture2D>(path);
				}

				var length = (int)iconEntry.Length;
				await using var iconStream = await iconEntry.OpenAsync();
				var texture = await Tools.LoadTextureFromStream(iconStream, length);

				if (texture is null) {
					return null;
				}

				texture.TakeOverPath(path);
				return texture;
			}
			case ModFileType.Directory: {
				var iconPath = Path.Combine(ModPath, IconName);
				return await Tools.LoadTextureFromPath(iconPath);
			}
		}

		return null;
	}

	public static string? ToModId(string? name) {
		if (name == null) {
			return null;
		}

		var stringBuilder = new StringBuilder(name.Length);
		for (var index = 0; index < name.Length; ++index) {
			var c = name[index];
			var num1 = c is < 'a' or > 'z' ? (c < 'A' ? 0 : (c <= 'Z' ? 1 : 0)) : 1;
			var flag = c is >= '0' and <= '9';
			var num2 = flag ? 1 : 0;
			if ((num1 | num2) != 0) {
				stringBuilder.Append(char.ToLower(c));
			}

			if (flag && index == 0) {
				throw new ArgumentException(
					$"无法自动将“{name}”转换为模组ID，因其以数字开头，不符合命名规范",
					nameof(name));
			}
		}

		return stringBuilder.ToString();
	}

	public static bool IsValidModId([NotNull] string? str) {
		if (string.IsNullOrEmpty(str)) {
#pragma warning disable CS8777 // 退出时，参数必须具有非 null 值。
			return false;
#pragma warning restore CS8777 // 退出时，参数必须具有非 null 值。
		}

		for (var index = 0; index < str.Length; ++index) {
			var ch = str[index];
			var num = ch < 'a' ? 0 : ch <= 'z' ? 1 : 0;
			var flag = ch is >= '0' and <= '9';
			if (num == 0 && (!flag || index == 0)) {
				return false;
			}
		}

		return true;
	}

	public static async Task<ModInfo?> FromZip(string zipPath) {
		try {
			await using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
			await using var zipArchive = new ZipArchive(fs, ZipArchiveMode.Read);

			var jsonEntry = zipArchive.GetEntry(JsonName);

			if (jsonEntry == null) {
				foreach (var entry in zipArchive.Entries) {
					if (!string.Equals(entry.Name, JsonName, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}

					jsonEntry = entry;
					break;
				}
			}

			if (jsonEntry == null) {
				return null;
			}

			await using var stream = await jsonEntry.OpenAsync();
			var modInfo = await JsonSerializer.DeserializeAsync(stream, SourceGenerationContext.Default.ModInfo);

			modInfo?.ModPath = zipPath;
			modInfo?.ModFileType = ModFileType.Zip;

			return modInfo;
		} catch (Exception e) {
			Log.Error($"无法从 {zipPath} 加载模组信息", e);
			return null;
		}
	}

	public static async Task<ModInfo?> FromDirectory(string directoryPath) {
		try {
			var jsonPath = Path.Combine(directoryPath, JsonName);
			if (!File.Exists(jsonPath)) {
				return null;
			}

			await using var fileStream = File.OpenRead(jsonPath);
			var modInfo = await JsonSerializer.DeserializeAsync(fileStream, SourceGenerationContext.Default.ModInfo);

			modInfo?.ModPath = directoryPath;
			modInfo?.ModFileType = ModFileType.Directory;

			return modInfo;
		} catch (Exception e) {
			Log.Error($"无法从 {directoryPath} 加载模组信息", e);
			return null;
		}
	}

	static private string[] ParseStringArray(CustomAttributeArgument argument) {
		if (argument.Value is not CustomAttributeArgument[] arrayArgs) {
			return [];
		}

		var array = arrayArgs.Select(a => a.Value.ToString() ?? string.Empty).ToArray();
		return array;
	}

	public static ModInfo? FromAssembly(string assemblyPath) {
		try {
			using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

			var modInfoAttribute = assembly.CustomAttributes
				.FirstOrDefault(a => a.AttributeType.FullName == "Vintagestory.API.Common.ModInfoAttribute");

			if (modInfoAttribute == null) {
				return null;
			}

			var modInfo = new ModInfo {
				Name = modInfoAttribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty,
				ModId = string.Empty,
				ModPath = assemblyPath,
				ModFileType = ModFileType.Assembly
			};

			foreach (var property in modInfoAttribute.Properties) {
				switch (property.Name) {
					case "Name":
						modInfo.Name = property.Argument.Value?.ToString() ?? modInfo.Name;
						break;
					case "Authors":
						modInfo.Authors = ParseStringArray(property.Argument);
						break;
					case "ModID":
						modInfo.ModId = property.Argument.Value?.ToString() ?? string.Empty;
						break;
					case "Version":
						modInfo.Version = property.Argument.Value?.ToString() ?? "1.0.0";
						break;
				}
			}

			return modInfo;
		} catch (Exception e) {
			Log.Error($"无法从 {assemblyPath} 加载模组信息", e);
			return null;
		}
	}

	public class DependenciesConverter : JsonConverter<IReadOnlyList<ModDependency>> {
		public override IReadOnlyList<ModDependency> Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options) {
			var dependencies = new List<ModDependency>();
			if (reader.TokenType != JsonTokenType.StartObject) {
				return [];
			}

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
				var modId = reader.GetString()!;
				reader.Read();
				var version = reader.GetString()!;
				dependencies.Add(new() { ModId = modId, Version = version });
			}

			return dependencies;
		}

		public override void Write(Utf8JsonWriter writer, IReadOnlyList<ModDependency> value, JsonSerializerOptions options) {
			writer.WriteStartObject();
			foreach (var dep in value)
				writer.WriteString(dep.ModId, dep.Version);
			writer.WriteEndObject();
		}
	}
}
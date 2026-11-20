using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Mono.Cecil;

namespace MVL.Utils.Game;

[JsonConverter(typeof(GameVersionJsonConverter))]
public readonly record struct GameVersion {
	private const int MaxVersionParts = 32;

	public GameVersion(string ShortGameVersion) {
		if (ShortGameVersion.StartsWith('v')) {
			ShortGameVersion = ShortGameVersion[1..];
		}

		this.ShortGameVersion = ShortGameVersion;

		Span<int> parts = stackalloc int[MaxVersionParts];
		SplitVersionString(ShortGameVersion, parts);
		OverallVersion = $"{parts[0]}.{parts[1]}";
		ReleaseType = ReleaseTypeFromParts(parts[3]);
		Branch = ReleaseType == EnumGameReleaseType.Stable ? EnumGameBranch.Stable : EnumGameBranch.Unstable;
	}

	public string OverallVersion { get; }
	public EnumGameBranch Branch { get; }
	public string ShortGameVersion { get; }
	public EnumGameReleaseType ReleaseType { get; }

	public static readonly Comparer<GameVersion> Comparer = Comparer<GameVersion>.Create(ComparerVersion);

	static private EnumGameReleaseType ReleaseTypeFromParts(int value) {
		return value switch {
			0 => EnumGameReleaseType.Development,
			1 => EnumGameReleaseType.Preview,
			2 => EnumGameReleaseType.Candidate,
			3 => EnumGameReleaseType.Stable,
			_ => throw new ArgumentException("未知版本类型")
		};
	}

	public static EnumGameReleaseType GetReleaseType(string version) {
		Span<int> parts = stackalloc int[MaxVersionParts];
		SplitVersionString(version, parts);
		return ReleaseTypeFromParts(parts[3]);
	}

	static private int SplitVersionString(ReadOnlySpan<char> version, Span<int> parts) {
		var dash = version.IndexOf('-');
		var hasDash = dash >= 1;
		var head = hasDash ? version[..dash] : version;
		var padZero = head.Count('.') == 1;

		var count = 0;
		var start = 0;
		ReadOnlySpan<char> fourth = default;
		for (var index = 0; index <= version.Length && count < parts.Length; index++) {
			if (index < version.Length) {
				var c = version[index];
				if (c != '.' && c != '-') {
					continue;
				}
			}

			var token = version[start..index];
			if (count == 3) {
				fourth = token;
			}

			parts[count++] = int.TryParse(token, out var value) ? value : 0;

			if (hasDash && index == dash && padZero) {
				parts[count++] = 0;
			}

			start = index + 1;
		}

		if (!hasDash && padZero) {
			parts[count++] = 0;
		}

		if (count <= 3) {
			while (count < 4 && count < parts.Length) {
				parts[count++] = 3;
			}
		} else {
			parts[3] = fourth is "rc" ? 2 : fourth is "pre" ? 1 : 0;
		}

		return count;
	}

	public static bool IsLowerVersionThan(string version, string reference) {
		return version != reference && !IsNewerVersionThan(version, reference);
	}

	public static bool IsNewerVersionThan(string version, string reference) {
		Span<int> referenceParts = stackalloc int[MaxVersionParts];
		Span<int> versionParts = stackalloc int[MaxVersionParts];
		var referenceCount = SplitVersionString(reference, referenceParts);
		var versionCount = SplitVersionString(version, versionParts);
		for (var index = 0;
			index < referenceCount && index < versionCount && referenceParts[index] <= versionParts[index];
			++index) {
			if (referenceParts[index] < versionParts[index]) {
				return true;
			}
		}

		return false;
	}

	public static int ComparerVersion(GameVersion version, GameVersion reference) {
		Span<int> referenceParts = stackalloc int[MaxVersionParts];
		Span<int> versionParts = stackalloc int[MaxVersionParts];
		var referenceCount = SplitVersionString(reference.ShortGameVersion, referenceParts);
		var versionCount = SplitVersionString(version.ShortGameVersion, versionParts);
		for (var index = 0; index < referenceCount; ++index) {
			if (index >= versionCount || referenceParts[index] > versionParts[index]) {
				return -1;
			}

			if (referenceParts[index] < versionParts[index]) {
				return 1;
			}
		}

		return 0;
	}

	public static GameVersion? FromGamePath(string gamePath) {
		var assemblyPath = Path.Combine(gamePath, "VintagestoryAPI.dll");
		if (!File.Exists(assemblyPath)) {
			Log.Error($"游戏程序集 {assemblyPath} 不存在");
			return null;
		}

		try {
			using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
			var type = assembly.MainModule.GetType("Vintagestory.API.Config.GameVersion");
			var versionField = type.Fields.FirstOrDefault(field =>
				field.Name == "ShortGameVersion" && field.IsLiteral);
			if (versionField?.Constant is string version) {
				var gameVersion = new GameVersion(version);
				return gameVersion;
			}

			Log.Error($"无法从游戏程序集 {assemblyPath} 获取游戏版本，未找到 ShortGameVersion 字段");
			return null;
		} catch (Exception e) {
			Log.Error($"无法从游戏程序集 {assemblyPath} 获取游戏版本", e);
			return null;
		}
	}

	public static bool TryFromGamePath(string path, out GameVersion version) {
		var versionFromPath = FromGamePath(path);
		if (versionFromPath != null) {
			version = versionFromPath.Value;
			return true;
		}

		version = default;
		return false;
	}

	public class GameVersionJsonConverter : JsonConverter<GameVersion> {

		public override GameVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			if (str != null) {
				return new(str);
			}

			return default;
		}

		public override void Write(Utf8JsonWriter writer, GameVersion value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ShortGameVersion);
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, GameVersion value, JsonSerializerOptions options) {
			writer.WritePropertyName(value.ToString());
		}

		public override GameVersion ReadAsPropertyName(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options) {
			var versionString = reader.GetString();
			if (versionString != null) {
				return new(versionString);
			}

			return default;
		}
	}
}
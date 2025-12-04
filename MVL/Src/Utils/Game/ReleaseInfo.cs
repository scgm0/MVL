using System;
using System.IO;
using System.Runtime.Versioning;
using Godot;
using Mono.Cecil;

namespace MVL.Utils.Game;

public record ReleaseInfo {
	public string Path { get; set; } = string.Empty;

	public GameVersion Version {
		get;
		set {
			field = value;
			var assemblyPath = Path.PathJoin("VintagestoryAPI.dll");
			if (!File.Exists(assemblyPath)) {
				return;
			}

			var targetFramework = GetTargetFramework(assemblyPath);
			TargetFrameworkName = targetFramework.FrameworkName;
			TargetFrameworkVersion = targetFramework.Version;
		}
	}

	public string Name { get; set; } = string.Empty;
	public string? TargetFrameworkName { get; private set; }
	public Version? TargetFrameworkVersion { get; private set; }

	public static (string? FrameworkName, Version? Version) GetTargetFramework(string assemblyPath) {
		try {
			using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
			var targetFrameworkAttributeName = typeof(TargetFrameworkAttribute).FullName;

			foreach (var attribute in assembly.CustomAttributes) {
				if (attribute.AttributeType.FullName != targetFrameworkAttributeName) {
					continue;
				}

				if (attribute.ConstructorArguments.Count > 0 &&
					attribute.ConstructorArguments[0].Value is string frameworkString) {
					return ParseTargetFrameworkInfo(frameworkString);
				}

				break;
			}
		} catch (Exception ex) {
			Log.Error($"无法从 {assemblyPath} 读取目标框架:", ex);
		}

		return (null, null);
	}

	static private (string? FrameworkName, Version? Version) ParseTargetFrameworkInfo(ReadOnlySpan<char> frameworkSpan) {
		ReadOnlySpan<char> nameSpan;
		ReadOnlySpan<char> versionSpan = [];

		var commaIndex = frameworkSpan.IndexOf(',');
		if (commaIndex != -1) {
			nameSpan = frameworkSpan[..commaIndex];
			versionSpan = frameworkSpan[(commaIndex + 1)..];
		} else {
			nameSpan = frameworkSpan;
		}

		var finalName = ParseFrameworkName(nameSpan);
		Version? finalVersion = null;

		if (versionSpan.IsEmpty) {
			return (finalName.ToString(), finalVersion);
		}

		const string versionPrefix = "Version=";
		var versionPart = versionSpan.Trim();
		var prefixIndex = versionPart.IndexOf(versionPrefix, StringComparison.OrdinalIgnoreCase);

		if (prefixIndex == -1) {
			return (finalName.ToString(), finalVersion);
		}

		var versionValueSpan = versionPart[(prefixIndex + versionPrefix.Length)..];
		if (versionValueSpan.Length > 0 && (versionValueSpan[0] == 'v' || versionValueSpan[0] == 'V')) {
			versionValueSpan = versionValueSpan[1..];
		}

		if (System.Version.TryParse(versionValueSpan, out finalVersion)) {
			return (finalName.ToString(), finalVersion);
		}

		Log.Error($"无法从 {versionSpan} 解析版本");
		return (finalName.ToString(), null);
	}

	static private ReadOnlySpan<char> ParseFrameworkName(ReadOnlySpan<char> nameSpan) {
		var processedSpan = nameSpan.Trim();

		if (!processedSpan.IsEmpty && processedSpan[0] == '.') {
			processedSpan = processedSpan[1..];
		}

		if (processedSpan.EndsWith("App", StringComparison.Ordinal)) {
			processedSpan = processedSpan[..^3];
		}

		return processedSpan;
	}
}
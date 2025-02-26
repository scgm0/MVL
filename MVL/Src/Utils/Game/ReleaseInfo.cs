using System;
using System.IO;
using System.Linq;
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
			if (!File.Exists(assemblyPath)) return;
			var targetFramework = GetTargetFramework(assemblyPath);
			TargetFrameworkName = targetFramework.FrameworkName;
			TargetFrameworkVersion = targetFramework.Version;
		}
	}

	public string Name { get; set; } = string.Empty;
	public string? TargetFrameworkName { get; private set; }
	public string? TargetFrameworkVersion { get; private set; }

	public static (string? FrameworkName, string? Version) GetTargetFramework(string assemblyPath) {
		using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
		var targetFrameworkAttributeName = typeof(System.Runtime.Versioning.TargetFrameworkAttribute).FullName;

		return (from attribute in assembly.CustomAttributes
				where attribute.AttributeType.FullName == targetFrameworkAttributeName
				select ParseFramework((string)attribute.ConstructorArguments[0].Value)).FirstOrDefault();
	}

	public static (string? FrameworkName, string? Version) ParseFramework(string framework) {
		var parts = framework.Split(',');
		var name = parts[0].Trim()[1..];
		var version = ParseVersionFromFramework(framework);
		return (name, version);
	}

	public static string? ParseVersionFromFramework(string framework) {
		try {
			const string versionPrefix = "Version=";
			var versionIndex = framework.IndexOf(versionPrefix, StringComparison.OrdinalIgnoreCase);
			if (versionIndex == -1) {
				return null;
			}

			var versionString = framework[(versionIndex + versionPrefix.Length)..]
				.TrimStart('v', 'V');

			return versionString;
		} catch {
			return null;
		}
	}
}
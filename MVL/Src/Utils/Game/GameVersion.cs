using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Mono.Cecil;

namespace MVL.Utils.Game;

public readonly record struct GameVersion {
	public GameVersion(string ShortGameVersion) {
		this.ShortGameVersion = ShortGameVersion;
		OverallVersion = ShortGameVersion[..3];
		ReleaseType = GetReleaseType(this.ShortGameVersion);
		Branch = ReleaseType == EnumGameReleaseType.Stable ? EnumGameBranch.Stable : EnumGameBranch.Unstable;
	}

	public string OverallVersion { get; }
	public EnumGameBranch Branch { get; }
	public string ShortGameVersion { get; }
	public EnumGameReleaseType ReleaseType { get; }

	static private readonly string[] Separators = [
		".",
		"-"
	];

	public static readonly Comparer<GameVersion> Comparer = Comparer<GameVersion>.Create(ComparerVersion);

	public static EnumGameReleaseType GetReleaseType(string version) {
		return SplitVersionString(version)[3] switch {
			0 => EnumGameReleaseType.Development,
			1 => EnumGameReleaseType.Preview,
			2 => EnumGameReleaseType.Candidate,
			3 => EnumGameReleaseType.Stable,
			_ => throw new ArgumentException("未知版本类型")
		};
	}

	public static int[] SplitVersionString(string version) {
		var num = version.IndexOf('-');
		var text = num < 1 ? version : version[..num];
		if (text.Count(t => t == '.') == 1) {
			var str = text + ".0";
			version = num < 1 ? str : string.Concat(str, version.AsSpan(num));
		}

		var array = version.Split(Separators, StringSplitOptions.None).ToList();
		if (array.Count <= 3) {
			array.Add("3");
		} else {
			array[3] = array[3] != "rc" ? array[3] != "pre" ? "0" : "1" : "2";
		}

		var numArray = new int[array.Count];
		for (var index = 0; index < array.Count; ++index) {
			if (!int.TryParse(array[index], out var result)) continue;
			numArray[index] = result;
		}

		return numArray;
	}

	public static int ComparerVersion(GameVersion version, GameVersion reference) {
		var numArray1 = SplitVersionString(reference.ShortGameVersion);
		var numArray2 = SplitVersionString(version.ShortGameVersion);
		for (var index = 0; index < numArray1.Length; ++index) {
			if (index >= numArray2.Length || numArray1[index] > numArray2[index]) {
				return -1;
			}

			if (numArray1[index] < numArray2[index]) {
				return 1;
			}
		}

		return 0;
	}

	public static GameVersion? FromGamePath(string gamePath) {
		try {
			var assembly = AssemblyDefinition.ReadAssembly(gamePath.PathJoin("VintagestoryAPI.dll"));
			var type = assembly.MainModule.GetType("Vintagestory.API.Config.GameVersion");
			var fields = type.Fields.ToDictionary(definition => definition.Name, definition => definition.Constant);
			var gameVersion = new GameVersion((string)fields["ShortGameVersion"]);
			return gameVersion;
		} catch {
			return null;
		}
	}
}
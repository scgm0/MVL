using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Game;

namespace MVL.Utils.Config;

public class ModpackConfigV0 {
	public string Name { get; set; } = string.Empty;
	public GameVersion? Version { get; set; }

	public string? ReleasePath { get; set; }

	public string Command { get; set; } = "";

	public string GameAssembly { get; set; } = "";

	[JsonExtensionData]
	public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
}
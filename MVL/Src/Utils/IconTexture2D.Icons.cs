using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace MVL.Utils;

public partial class IconTexture2D {
	static private readonly Dictionary<string, string> Icons = JsonSerializer.Deserialize(FileAccess.GetFileAsString(
		"res://Assets/Icon/MD/icons.json"), SourceGenerationContext.Default.DictionaryStringString)!;
}
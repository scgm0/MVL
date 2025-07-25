using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Config;
using MVL.Utils.Game;
using SharedLibrary;

namespace MVL.Utils;

[JsonSerializable(typeof(Dictionary<GameVersion, GameRelease>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(RunConfig))]
[JsonSerializable(typeof(BaseConfig))]
[JsonSerializable(typeof(ModpackConfig))]
[JsonSerializable(typeof(GameRelease))]
[JsonSerializable(typeof(GameDownloadInfo))]
[JsonSerializable(typeof(GameDownloadUrl))]
[JsonSerializable(typeof(GameVersion))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(ModInfo))]
[JsonSerializable(typeof(ModDependency))]
[JsonSerializable(typeof(ApiStatusModInfo))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(ValidateResponse))]
[JsonSerializable(typeof(ApiStatusAuthors))]
[JsonSerializable(typeof(ApiStatusGameVersions))]
[JsonSerializable(typeof(ApiStatusModTags))]
[JsonSerializable(typeof(ApiStatusModsList))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web,
	AllowTrailingCommas = true,
	UseStringEnumConverter = true,
	WriteIndented = true)]
public partial class SourceGenerationContext : JsonSerializerContext {
	static SourceGenerationContext() { Default = new(CreateJsonSerializerOptions(Default)); }

	static private JsonSerializerOptions CreateJsonSerializerOptions(SourceGenerationContext defaultContext) {
		var options = new JsonSerializerOptions(defaultContext.GeneratedSerializerOptions!) {
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		options.Converters.Add(new DateTimeOffsetConverter());

		return options;
	}
}
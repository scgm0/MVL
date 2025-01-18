using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Config;
using SharedLibrary;

namespace MVL.Utils;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(RunConfig))]
[JsonSerializable(typeof(BaseConfig))]
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

		return options;
	}
}
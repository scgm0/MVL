using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using 复古物语启动器.Utils.Config;

namespace 复古物语启动器.Utils;

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
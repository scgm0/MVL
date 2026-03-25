using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVL.Utils;

public class LocalizedStringConverter : JsonConverter<LocalizedString> {
	public override LocalizedString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return new(reader.GetString() ?? string.Empty);
	}

	public override void Write(Utf8JsonWriter writer, LocalizedString value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.Value);
	}
}
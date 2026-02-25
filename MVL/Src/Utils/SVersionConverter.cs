using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSemVer;

namespace MVL.Utils;

public class SVersionConverter : JsonConverter<SVersion> {
	public override SVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return SVersion.Parse(reader.GetString() ?? "0.0.0-0");
	}

	public override void Write(Utf8JsonWriter writer, SVersion value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}
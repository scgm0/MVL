using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVL.Utils;

public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset> {
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return DateTimeOffset.Parse(reader.GetString() ?? string.Empty);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}
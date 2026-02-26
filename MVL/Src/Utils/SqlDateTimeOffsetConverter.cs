using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVL.Utils;

public class SqlDateTimeOffsetConverter : JsonConverter<DateTimeOffset> {
	private const string Format = "yyyy-MM-dd HH:mm:ss";

	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		var dateString = reader.GetString();

		return string.IsNullOrWhiteSpace(dateString) ? default : DateTimeOffset.Parse(dateString, CultureInfo.InvariantCulture);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
	}
}
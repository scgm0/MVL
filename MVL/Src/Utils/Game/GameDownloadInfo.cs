using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVL.Utils.Game;

public readonly record struct GameDownloadInfo() {
	public required string FileName { get; init; } = string.Empty;
	public required string FileSize { get; init; } = string.Empty;

	[JsonConverter(typeof(Md5JsonConverter))]
	public required byte[] Md5 { get; init; } = [];

	public required GameDownloadUrl Urls { get; init; } = default;
	public int Latest { get; init; } = 0;

	public class Md5JsonConverter : JsonConverter<byte[]> {
		public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			return string.IsNullOrEmpty(str) ? null : Convert.FromHexString(str);
		}

		public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) {
			writer.WriteStringValue(Convert.ToHexString(value));
		}
	}
}
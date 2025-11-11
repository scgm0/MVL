using System.Text.Json;
using System.Text.Json.Serialization;

namespace MVL.Utils.Multiplayer;

public record struct EasyTierPlayerInfo {
	[JsonPropertyName("hostname")]
	public string HostName { get; set; }

	[JsonPropertyName("ipv4")]
	public string IpV4 { get; set; }

	public string Version { get; set; }
	public long Id { get; set; }
	public string Cost { get; set; }
	public override string ToString() => JsonSerializer.Serialize(this, SourceGenerationContext.Default.EasyTierPlayerInfo);

	public static EasyTierPlayerInfo Parse(string data) {
		try {
			var playerInfo = JsonSerializer.Deserialize(data, SourceGenerationContext.Default.EasyTierPlayerInfo);
			return playerInfo;
		} catch {
			return new();
		}
	}
}
using System.Text.Json.Serialization;

namespace MVL.Utils.Multiplayer;

public record struct NodeInfo {
	public string Address { get; set; }

	[JsonPropertyName("allow_relay")]
	public bool AllowRelay { get; set; }

	[JsonPropertyName("is_active")]
	public bool IsActive { get; set; }
}
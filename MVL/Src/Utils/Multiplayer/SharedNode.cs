using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Help;

namespace MVL.Utils.Multiplayer;

public record SharedNode : IJsonOnDeserialized {
	public required string Address { get; set; }
	public LocalizedString Name { get; set; }
	public LocalizedString Remark { get; set; }
	public CodecType? Codec { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement>? ExtensionData { get; set; }

	public void RestoreLocalizations() {
		LocalizedStringJsonHelper.RestoreLocalizationsFromExtensionData(this, SourceGenerationContext.Default.SharedNode,
			ExtensionData, keepExtensionData: true);
	}

	void IJsonOnDeserialized.OnDeserialized() { RestoreLocalizations(); }

	public void UpdateAddress(string address) {
		Address = Codec switch {
			CodecType.None or null => address,
			CodecType.Base62 => Base62Encoder.Encode(Encoding.UTF8.GetBytes(address)),
			CodecType.Base64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(address)),
			_ => throw new ArgumentOutOfRangeException(nameof(address))
		};
	}

	public string GetDecodedAddress() {
		return Codec switch {
			CodecType.None or null => Address,
			CodecType.Base62 => Encoding.UTF8.GetString(Base62Encoder.Decode(Address)),
			CodecType.Base64 => Encoding.UTF8.GetString(Convert.FromBase64String(Address)),
			_ => throw new ArgumentOutOfRangeException(nameof(Codec))
		};
	}
}
using System;
using System.Text;

namespace MVL.Utils.Multiplayer;

public record SharedNode {
	public required string Address { get; set; }
	public string? Name { get; set; }
	public string? Remark { get; set; }
	public CodecType? Codec { get; set; }

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
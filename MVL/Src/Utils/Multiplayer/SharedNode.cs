using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Help;

namespace MVL.Utils.Multiplayer;

public record SharedNode : IJsonOnDeserialized {
	static private readonly UTF8Encoding StrictUtf8 = new(false, true);

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

	void IJsonOnDeserialized.OnDeserialized() {
		RestoreLocalizations();
		if (Codec is null && TryDetectCodec(Address, out var codec, out _) && codec is not CodecType.None) {
			Codec = codec;
		}
	}

	public static bool IsValidNodeAddress([NotNullWhen(true)] string? address) {
		return !string.IsNullOrEmpty(address) && address.Contains("://", StringComparison.Ordinal) &&
			Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
			uri.HostNameType is not UriHostNameType.Unknown && !string.IsNullOrEmpty(uri.Host);
	}

	public static bool TryDecode(string? data, CodecType codec, [NotNullWhen(true)] out string? address) {
		address = null;
		if (string.IsNullOrEmpty(data)) {
			return false;
		}

		if (codec is CodecType.None) {
			address = data;
			return true;
		}

		if (codec is CodecType.Base64 && data.Length % 4 != 0) {
			return false;
		}

		try {
			address = codec switch {
				CodecType.Base62 => StrictUtf8.GetString(Base62Encoder.Decode(data)),
				CodecType.Base64 => StrictUtf8.GetString(Convert.FromBase64String(data)),
				_ => null
			};
		} catch (Exception e) when (e is FormatException or DecoderFallbackException) {
			return false;
		}

		return !string.IsNullOrEmpty(address);
	}

	public static bool TryDetectCodec(string? data, out CodecType codec, [NotNullWhen(true)] out string? address) {
		codec = CodecType.None;
		address = null;
		if (string.IsNullOrEmpty(data)) {
			return false;
		}

		if (IsValidNodeAddress(data)) {
			address = data;
			return true;
		}

		ReadOnlySpan<CodecType> candidates = [CodecType.Base64, CodecType.Base62];
		foreach (var candidate in candidates) {
			if (!TryDecode(data, candidate, out address) || !IsValidNodeAddress(address)) {
				continue;
			}

			codec = candidate;
			return true;
		}

		address = null;
		return false;
	}

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
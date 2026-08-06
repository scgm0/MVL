using System;
using System.Buffers;
using System.Text;

namespace MVL.Utils;

public static class Base62Encoder {
	private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
	private const uint Pow62 = 916_132_832;

	public static string Encode(byte[] data) {
		if (data.Length == 0) {
			return string.Empty;
		}

		if (data.Length > ushort.MaxValue) {
			throw new ArgumentException("数据过大，超出单次编码上限。");
		}

		var byteLength = data.Length + 2;
		var limbCount = (byteLength + 3) >> 2;
		var limbs = ArrayPool<uint>.Shared.Rent(limbCount);
		try {
			Array.Clear(limbs, 0, limbCount);

			var length = (uint)data.Length;
			var limb = length;
			var shift = 16;
			var limbIndex = 0;

			foreach (var b in data) {
				limb |= (uint)b << shift;
				shift += 8;
				if (shift != 32) {
					continue;
				}

				limbs[limbIndex++] = limb;
				limb = 0;
				shift = 0;
			}

			if (shift != 0) {
				limbs[limbIndex] = limb;
			}

			var top = limbCount - 1;
			while (top > 0 && limbs[top] == 0) {
				top--;
			}

			var sb = new StringBuilder(byteLength * 7 / 5 + 1);
			while (true) {
				ulong carry = 0;
				for (var i = top; i >= 0; i--) {
					var current = (carry << 32) | limbs[i];
					var (quotient, remainder) = Math.DivRem(current, Pow62);
					limbs[i] = (uint)quotient;
					carry = remainder;
				}

				sb.Append(Alphabet[(int)(carry % 62)]);
				carry /= 62;
				sb.Append(Alphabet[(int)(carry % 62)]);
				carry /= 62;
				sb.Append(Alphabet[(int)(carry % 62)]);
				carry /= 62;
				sb.Append(Alphabet[(int)(carry % 62)]);
				carry /= 62;
				sb.Append(Alphabet[(int)carry]);

				while (top > 0 && limbs[top] == 0) {
					top--;
				}

				if (top == 0 && limbs[0] == 0) {
					break;
				}
			}

			var end = sb.Length;
			while (end > 1 && sb[end - 1] == '0') {
				end--;
			}

			sb.Length = end;
			return sb.ToString();
		} finally {
			ArrayPool<uint>.Shared.Return(limbs);
		}
	}

	public static byte[] Decode(ReadOnlySpan<char> token) {
		if (token.IsEmpty) {
			return [];
		}

		var limbCount = token.Length / 5 + 1;
		var limbs = ArrayPool<uint>.Shared.Rent(limbCount);
		try {
			Array.Clear(limbs, 0, limbCount);
			var used = 1;

			var index = token.Length;
			var head = index % 5;
			if (head == 0) {
				head = 5;
			}

			while (index > 0) {
				var start = index - (index == token.Length ? head : 5);
				uint chunk = 0;
				for (var i = index - 1; i >= start; i--) {
					var digit = CharToDigit(token[i]);
					if (digit < 0) {
						throw new FormatException("发现非法字符，Token 已被破坏。");
					}

					chunk = chunk * 62 + (uint)digit;
				}

				ulong carry = chunk;
				for (var i = 0; i < used; i++) {
					var value = (ulong)limbs[i] * Pow62 + carry;
					limbs[i] = (uint)value;
					carry = value >> 32;
				}

				if (carry != 0) {
					limbs[used++] = (uint)carry;
				}

				index = start;
			}

			var top = used - 1;
			while (top > 0 && limbs[top] == 0) {
				top--;
			}

			var high = limbs[top];
			var byteCount = top << 2;
			while (high != 0) {
				high >>= 8;
				byteCount++;
			}

			if (byteCount < 2) {
				throw new FormatException("Token 数据由于不完整导致解析失败。");
			}

			var originalLength = (ushort)(limbs[0] & 0xFFFF);
			var result = new byte[originalLength];

			var copyLength = Math.Min(byteCount - 2, originalLength);
			for (var i = 0; i < copyLength; i++) {
				result[i] = (byte)(limbs[(i + 2) >> 2] >> (((i + 2) & 3) << 3));
			}

			return result;
		} finally {
			ArrayPool<uint>.Shared.Return(limbs);
		}
	}

	static private int CharToDigit(char c) {
		return c switch {
			>= '0' and <= '9' => c - '0',
			>= 'A' and <= 'Z' => c - 'A' + 10,
			>= 'a' and <= 'z' => c - 'a' + 36,
			_ => -1
		};
	}
}

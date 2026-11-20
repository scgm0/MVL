using System;
using System.Buffers;
using System.Numerics;

namespace MVL.Utils;

public static class Base36Converter {
	private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

	public static string ToBase36String(BigInteger value) {
		if (value.IsZero) {
			return "0";
		}

		if (value.Sign < 0) {
			throw new ArgumentOutOfRangeException(nameof(value), "不支持负数");
		}

		var length = (int)(value.GetBitLength() / 5) + 2;
		var buffer = ArrayPool<char>.Shared.Rent(length);
		try {
			var index = length;
			while (!value.IsZero) {
				var (quotient, remainder) = BigInteger.DivRem(value, 36);
				buffer[--index] = Digits[(int)remainder];
				value = quotient;
			}

			return new string(buffer, index, length - index);
		} finally {
			ArrayPool<char>.Shared.Return(buffer);
		}
	}

	public static BigInteger ParseBase36(ReadOnlySpan<char> value) {
		if (value.IsWhiteSpace()) {
			throw new ArgumentNullException(nameof(value));
		}

		BigInteger result = 0;
		foreach (var c in value) {
			var digit = CharToDigit(c);
			if (digit < 0) {
				throw new FormatException("输入字符串包含无效的Base36字符");
			}

			result = result * 36 + digit;
		}

		return result;
	}

	static private int CharToDigit(char c) {
		return c switch {
			>= '0' and <= '9' => c - '0',
			>= 'a' and <= 'z' => c - 'a' + 10,
			>= 'A' and <= 'Z' => c - 'A' + 10,
			_ => -1
		};
	}
}

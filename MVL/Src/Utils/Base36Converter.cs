using System;
using System.Numerics;
using System.Text;

namespace MVL.Utils;

public static class Base36Converter {
	private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

	public static string ToBase36String(BigInteger value) {
		if (value == 0) {
			return "0";
		}

		if (value < 0) {
			throw new ArgumentOutOfRangeException(nameof(value), "不支持负数");
		}

		var sb = new StringBuilder();
		while (value > 0) {
			sb.Insert(0, Digits[(int)(value % 36)]);
			value /= 36;
		}

		return sb.ToString();
	}

	public static BigInteger ParseBase36(string value) {
		if (string.IsNullOrWhiteSpace(value)) {
			throw new ArgumentNullException(nameof(value));
		}

		BigInteger result = 0;
		BigInteger power = 1;
		for (var i = value.Length - 1; i >= 0; i--) {
			var digit = Digits.IndexOf(char.ToLowerInvariant(value[i]));
			if (digit == -1) {
				throw new FormatException("输入字符串包含无效的Base36字符");
			}

			result += digit * power;
			power *= 36;
		}

		return result;
	}
}
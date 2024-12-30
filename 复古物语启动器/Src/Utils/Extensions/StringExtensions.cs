using System;
using System.Text;

namespace 复古物语启动器.Utils.Extensions;

public static class StringExtensions {
	[ThreadStatic]
	static private StringBuilder? _builder;

	static private readonly string[] AnsiColors = [
		"black",
		"dark_red",
		"dark_green",
		"yellow_green",
		"dark_blue",
		"dark_magenta",
		"dark_cyan",
		"dark_gray",
		"gray",
		"red",
		"green",
		"yellow",
		"blue",
		"magenta",
		"cyan",
		"white"
	];

	public static string ConvertAnsiToBbCode(this string str) {
		ArgumentNullException.ThrowIfNull(str);
		if (!str.Contains('\e')) {
			return str;
		}

		if (_builder == null) {
			_builder = new();
		} else {
			_builder.Clear();
		}

		var source = str.AsSpan();
		var start = 0;
		var end = 0;
		for (var index = 0; index < source.Length; index++) {
			var spanChar = source[index];
			if (spanChar != '\e') {
				end++;
				continue;
			}

			var bracketIndex = index + 1;
			if (bracketIndex >= source.Length || source[bracketIndex] != '[') {
				end++;
				continue;
			}

			var digitStartIndex = index + 2;
			if (digitStartIndex >= source.Length) {
				end++;
				continue;
			}

			var checkIndex = digitStartIndex;

			if (!char.IsDigit(source[checkIndex])) {
				end++;
				continue;
			}

			checkIndex++;

			if (checkIndex >= source.Length) {
				end++;
				continue;
			}

			while (char.IsDigit(source[checkIndex])) {
				checkIndex++;
				if (checkIndex >= source.Length) break;
			}

			var digitEndIndex = checkIndex;
			var bracketEndIndex = checkIndex;
			if (bracketEndIndex >= source.Length) {
				end++;
				continue;
			}

			if (source[bracketEndIndex] != 'm') {
				end++;
				continue;
			}

			var colorCode = int.Parse(source[digitStartIndex..digitEndIndex]);

			if (start != end) {
				_builder.Append(source[start..end]);
			}

			switch (colorCode) {
				case >= 30 and <= 37:
					_builder
						.Append("[color=")
						.Append(AnsiColors[colorCode - 30])
						.Append(']');
					break;
				case >= 90 and <= 97:
					_builder
						.Append("[color=")
						.Append(AnsiColors[colorCode - 82])
						.Append(']');
					break;
				case 0:
					_builder.Append("[/color]");
					break;
			}

			index = bracketEndIndex;
			start = index + 1;
			end = start;
		}

		if (start != end) {
			_builder.Append(source[start..end]);
		}

		return _builder.ToString();
	}
}
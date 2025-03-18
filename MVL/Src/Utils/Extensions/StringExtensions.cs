using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace MVL.Utils.Extensions;

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

	static private readonly List<int> Last = [];

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
					Last.Add(1);
					break;
				case >= 90 and <= 97:
					_builder
						.Append("[color=")
						.Append(AnsiColors[colorCode - 82])
						.Append(']');
					Last.Add(1);
					break;
				case >= 40 and <= 47:
					_builder
						.Append("[bgcolor=")
						.Append(AnsiColors[colorCode - 40])
						.Append(']');
					Last.Add(2);
					break;
				case >= 100 and <= 107:
					_builder
						.Append("[bgcolor=")
						.Append(AnsiColors[colorCode - 92])
						.Append(']');
					Last.Add(2);
					break;
				case 0:
					Last.ForEach(i => {
						switch (i) {
							case 1: _builder.Append("[/color]"); break;
							case 2: _builder.Append("[/bgcolor]"); break;
						}
					});

					Last.Clear();
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

	public static string NormalizePath(this string path) {
		if (string.IsNullOrEmpty(path)) return path;

		path = Path.Combine(OS.GetExecutablePath(), path);
		var fullPath = Path.GetFullPath(path.GetBaseDir());

		fullPath = Path.Combine(fullPath, Path.GetFileName(path)).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

		return Path.TrimEndingDirectorySeparator(fullPath);
	}
}
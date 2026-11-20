using System;
using System.Buffers;
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

	extension(string str) {
		public string ConvertAnsiToBbCode() {
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
					if (checkIndex >= source.Length) {
						break;
					}
				}

				var digitEndIndex = checkIndex;
				var bracketEndIndex = checkIndex;
				if (bracketEndIndex >= source.Length || source[bracketEndIndex] != 'm') {
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

		public string NormalizePath() {
			if (string.IsNullOrEmpty(str)) {
				return str;
			}

			var combinedPath = Path.Combine(OS.GetExecutablePath(), str);

			var fullPath = Path.GetFullPath(combinedPath);

			return fullPath;
		}
	}

	static private readonly SearchValues<string> InlineFormatTokens = SearchValues.Create(
		["**", "`", "[", "<https://", "<http://", "https://github.com/"], StringComparison.Ordinal);

	static private void ProcessInlineFormatting(StringBuilder sb, ReadOnlySpan<char> line) {
		while (!line.IsEmpty) {
			var tokenPos = line.IndexOfAny(InlineFormatTokens);
			if (tokenPos < 0) {
				sb.Append(line);
				return;
			}

			sb.Append(line[..tokenPos]);
			line = line[tokenPos..];

			if (line.StartsWith("**")) {
				var content = line[2..];
				var closingPos = content.IndexOf("**");
				if (closingPos != -1) {
					sb.Append("[b]").Append(content[..closingPos]).Append("[/b]");
					line = content[(closingPos + 2)..];
					continue;
				}

				sb.Append("**");
				line = content;
				continue;
			}

			if (line[0] == '`') {
				var content = line[1..];
				var closingPos = content.IndexOf('`');
				if (closingPos != -1) {
					sb.Append("[img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img][code][fgcolor=0007]")
						.Append(content[..closingPos])
						.Append("[/fgcolor][/code][img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img]");
					line = content[(closingPos + 1)..];
					continue;
				}

				sb.Append('`');
				line = content;
				continue;
			}

			if (line[0] == '[') {
				var closeBracket = line.IndexOf(']');
				if (closeBracket != -1 && line.Length > closeBracket + 1 && line[closeBracket + 1] == '(') {
					var afterParen = line[(closeBracket + 2)..];
					var closeParen = afterParen.IndexOf(')');
					if (closeParen != -1) {
						sb.Append("[color=#3c7fe1][url=").Append(afterParen[..closeParen]).Append(']');
						ProcessInlineFormatting(sb, line[1..closeBracket]);
						sb.Append("[/url][/color]");
						line = afterParen[(closeParen + 1)..];
						continue;
					}
				}

				sb.Append('[');
				line = line[1..];
				continue;
			}

			if (line.StartsWith("<https://") || line.StartsWith("<http://")) {
				var closeBracket = line.IndexOf('>');
				if (closeBracket != -1) {
					sb.Append("[color=#3c7fe1][url]").Append(line[1..closeBracket]).Append("[/url][/color]");
					line = line[(closeBracket + 1)..];
					continue;
				}

				sb.Append('<');
				line = line[1..];
				continue;
			}

			var urlEnd = 0;
			while (urlEnd < line.Length) {
				var c = line[urlEnd];
				if (char.IsWhiteSpace(c) || c is '<' or '>' or ')' or ']' or '(' or '[') {
					break;
				}

				urlEnd++;
			}

			var url = line[..urlEnd];
			if (url.IndexOf("/compare/", StringComparison.Ordinal) != -1) {
				var lastSlash = url.LastIndexOf('/');
				var versionText = url[(lastSlash + 1)..]; // 截取 compare/ 后的版本范围字符

				sb.Append("[url=").Append(url).Append(']').Append(versionText).Append("[/url]");
				line = line[urlEnd..];
				continue;
			}

			sb.Append('h');
			line = line[1..];
		}
	}

	extension(ReadOnlySpan<char> inputSpan) {
		public (string Chinese, string English) SplitAndConvert() {
			var englishMarkerIndex = inputSpan.IndexOf("English:");
			if (englishMarkerIndex == -1) {
				return (inputSpan[(inputSpan.IndexOf("中文:") + 3)..].Trim().ConvertMarkdownToBbcode(), string.Empty);
			}

			var chineseMdSpan =
				inputSpan.Slice(inputSpan.IndexOf("中文:") + 3, englishMarkerIndex - (inputSpan.IndexOf("中文:") + 3)).Trim();
			var englishMdSpan = inputSpan[(englishMarkerIndex + "English:".Length)..].Trim();

			return (chineseMdSpan.ToString(), englishMdSpan.ToString());
		}

		public string ConvertMarkdownToBbcode() {
			var sb = new StringBuilder();
			var inList = false;

			while (!inputSpan.IsEmpty) {
				var newLineIndex = inputSpan.IndexOf('\n');
				var line = newLineIndex == -1 ? inputSpan : inputSpan[..newLineIndex];
				inputSpan = newLineIndex == -1 ? ReadOnlySpan<char>.Empty : inputSpan[(newLineIndex + 1)..];

				line = line.Trim();

				if (line.IsEmpty) {
					if (inList) {
						sb.AppendLine("[/ol]");
						inList = false;
					}

					if (sb.Length > 0) {
						sb.AppendLine();
					}

					continue;
				}

				var isListItem = false;
				var listPrefixLength = 0;
				var dotSpaceIndex = line.IndexOf(". ");

				if (dotSpaceIndex > 0) {
					isListItem = true;
					for (var i = 0; i < dotSpaceIndex; i++) {
						if (!char.IsDigit(line[i])) {
							isListItem = false;
							break;
						}
					}

					if (isListItem) {
						listPrefixLength = dotSpaceIndex + 2;
					}
				}

				if (isListItem) {
					if (!inList) {
						sb.AppendLine("[ol]");
						inList = true;
					}

					sb.Append("[color=#fff]");
					ProcessInlineFormatting(sb, line[listPrefixLength..]);
					sb.Append("[/color]");
				} else {
					if (inList) {
						sb.AppendLine("[/ol]");
						inList = false;
					}

					ProcessInlineFormatting(sb, line);
				}

				if (!inputSpan.IsEmpty) {
					sb.AppendLine();
				}
			}

			if (inList) {
				sb.AppendLine("[/ol]");
			}

			return sb.ToString().Trim();
		}
	}
}
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

	static private void ProcessInlineFormatting(StringBuilder sb, ReadOnlySpan<char> line) {
		var remainingLine = line;
		while (!remainingLine.IsEmpty) {
			var boldPos = remainingLine.IndexOf("**");
			var urlPos = remainingLine.IndexOf("https://");
			var codePos = remainingLine.IndexOf('`');

			var firstPos = -1;

			if (boldPos != -1) firstPos = boldPos;
			if (urlPos != -1 && (firstPos == -1 || urlPos < firstPos)) firstPos = urlPos;
			if (codePos != -1 && (firstPos == -1 || codePos < firstPos)) firstPos = codePos;

			if (firstPos == -1) {
				sb.Append(remainingLine);
				break;
			}

			sb.Append(remainingLine[..firstPos]);

			if (firstPos == boldPos) {
				var contentSpan = remainingLine[(firstPos + 2)..];
				var closingPos = contentSpan.IndexOf("**");
				if (closingPos != -1) {
					sb.Append("[b]").Append(contentSpan[..closingPos]).Append("[/b]");
					remainingLine = contentSpan[(closingPos + 2)..];
				} else {
					sb.Append(remainingLine[..2]);
					remainingLine = remainingLine[2..];
				}
			} else if (firstPos == urlPos) {
				var urlSpan = remainingLine[firstPos..];
				var urlEnd = urlSpan.IndexOf(' ');
				if (urlEnd == -1) urlEnd = urlSpan.Length;

				sb.Append("[color=#3c7fe1][url]").Append(urlSpan[..urlEnd]).Append("[/url][/color]");
				remainingLine = urlSpan[urlEnd..];
			} else if (firstPos == codePos) {
				var contentSpan = remainingLine[(firstPos + 1)..];
				var closingPos = contentSpan.IndexOf('`');
				if (closingPos != -1) {
					sb.Append("[img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img][code][fgcolor=0007]").Append(contentSpan[..closingPos]).Append("[/fgcolor][/code][img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img]");
					remainingLine = contentSpan[(closingPos + 1)..];
				} else {
					sb.Append(remainingLine[..1]);
					remainingLine = remainingLine[1..];
				}
			}
		}
	}

	extension(ReadOnlySpan<char> inputSpan) {
		public (string chinese, string English) SplitAndConvert() {
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

					if (sb.Length > 0) sb.AppendLine();
					continue;
				}

				if (line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.' && line[2] == ' ') {
					if (!inList) {
						sb.AppendLine("[ol]");
						inList = true;
					}

					ProcessInlineFormatting(sb, line[3..]);
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
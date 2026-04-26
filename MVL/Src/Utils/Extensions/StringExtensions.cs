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

	private enum InlineFormatType {
		None = 0,
		Bold,
		Code,
		MarkdownLink,
		GitHubCompare,
		BracketLink
	}

	static private void ProcessInlineFormatting(StringBuilder sb, ReadOnlySpan<char> line) {
		var remainingLine = line;
		while (!remainingLine.IsEmpty) {
			var boldPos = remainingLine.IndexOf("**");
			var codePos = remainingLine.IndexOf('`');
			var mdLinkPos = remainingLine.IndexOf('[');
			var bracketUrlPos1 = remainingLine.IndexOf("<https://");
			var bracketUrlPos2 = remainingLine.IndexOf("<http://");

			var bracketUrlPos = GetValidMin(bracketUrlPos1, bracketUrlPos2);

			var ghComparePos = -1;
			var searchStart = 0;
			while (searchStart < remainingLine.Length) {
				var p = remainingLine[searchStart..].IndexOf("https://github.com/");
				if (p == -1) break;
				p += searchStart;

				var urlEnd = p;
				while (urlEnd < remainingLine.Length) {
					var c = remainingLine[urlEnd];
					if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == ')' || c == ']' || c == '(' || c == '[')
						break;
					urlEnd++;
				}

				var candidate = remainingLine[p..urlEnd];
				if (candidate.IndexOf("/compare/") != -1) {
					ghComparePos = p;
					break;
				}

				searchStart = urlEnd;
			}

			var firstPos = -1;
			var currentType = InlineFormatType.None;

			CheckPos(boldPos, InlineFormatType.Bold);
			CheckPos(codePos, InlineFormatType.Code);
			CheckPos(mdLinkPos, InlineFormatType.MarkdownLink);
			CheckPos(ghComparePos, InlineFormatType.GitHubCompare);
			CheckPos(bracketUrlPos, InlineFormatType.BracketLink);

			if (firstPos == -1) {
				sb.Append(remainingLine);
				break;
			}

			sb.Append(remainingLine[..firstPos]);
			remainingLine = remainingLine[firstPos..];

			switch (currentType) {
				case InlineFormatType.Bold: {
					var contentSpan = remainingLine[2..];
					var closingPos = contentSpan.IndexOf("**");
					if (closingPos != -1) {
						sb.Append("[b]").Append(contentSpan[..closingPos]).Append("[/b]");
						remainingLine = contentSpan[(closingPos + 2)..];
					} else {
						sb.Append(remainingLine[..2]);
						remainingLine = remainingLine[2..];
					}

					break;
				}
				case InlineFormatType.Code: {
					var contentSpan = remainingLine[1..];
					var closingPos = contentSpan.IndexOf('`');
					if (closingPos != -1) {
						sb.Append("[img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img][code][fgcolor=0007]")
							.Append(contentSpan[..closingPos])
							.Append("[/fgcolor][/code][img width=3 height=1 color=0000]uid://cg2dshwidpbbv[/img]");
						remainingLine = contentSpan[(closingPos + 1)..];
					} else {
						sb.Append(remainingLine[..1]);
						remainingLine = remainingLine[1..];
					}

					break;
				}
				case InlineFormatType.MarkdownLink: {
					var closeBracket = remainingLine.IndexOf(']');
					if (closeBracket != -1 && remainingLine.Length > closeBracket + 1 &&
						remainingLine[closeBracket + 1] == '(') {
						var afterParen = remainingLine[(closeBracket + 2)..];
						var closeParen = afterParen.IndexOf(')');
						if (closeParen != -1) {
							var text = remainingLine[1..closeBracket];
							var url = afterParen[..closeParen];

							sb.Append("[color=#3c7fe1][url=").Append(url).Append(']');
							ProcessInlineFormatting(sb, text);
							sb.Append("[/url][/color]");

							remainingLine = afterParen[(closeParen + 1)..];
							continue;
						}
					}

					sb.Append(remainingLine[..1]);
					remainingLine = remainingLine[1..];
					break;
				}
				case InlineFormatType.GitHubCompare: {
					var urlEnd = 0;
					while (urlEnd < remainingLine.Length) {
						var c = remainingLine[urlEnd];
						if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == ')' || c == ']' || c == '(' || c == '[')
							break;
						urlEnd++;
					}

					var urlSpan = remainingLine[..urlEnd];
					var lastSlash = urlSpan.LastIndexOf('/');
					var versionText = urlSpan[(lastSlash + 1)..]; // 截取 compare/ 后的版本范围字符

					sb.Append("[url=").Append(urlSpan).Append(']').Append(versionText).Append("[/url]");
					remainingLine = remainingLine[urlEnd..];
					break;
				}
				case InlineFormatType.BracketLink: {
					var closeBracket = remainingLine.IndexOf('>');
					if (closeBracket != -1) {
						var url = remainingLine[1..closeBracket];
						sb.Append("[color=#3c7fe1][url]").Append(url).Append("[/url][/color]");
						remainingLine = remainingLine[(closeBracket + 1)..];
					} else {
						sb.Append(remainingLine[..1]);
						remainingLine = remainingLine[1..];
					}

					break;
				}
			}

			continue;

			void CheckPos(int pos, InlineFormatType type) {
				if (pos != -1 && (firstPos == -1 || pos < firstPos)) {
					firstPos = pos;
					currentType = type;
				}
			}

			int GetValidMin(int a, int b) => a != -1 && b != -1 ? Math.Min(a, b) : Math.Max(a, b);
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

					if (sb.Length > 0) sb.AppendLine();
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

					ProcessInlineFormatting(sb, line[listPrefixLength..]);
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
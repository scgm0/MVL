using System;
using System.Collections.Generic;

namespace MVL.Utils;

public static class LaunchEnvironment {
	public static StringComparer NameComparer { get; } = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public static Dictionary<string, string?> Extract(ref string command) {
		var environment = new Dictionary<string, string?>(NameComparer);

		var commandIndex = command.IndexOf("%command%", StringComparison.Ordinal);
		var prefixLength = commandIndex >= 0 ? commandIndex : command.Length;
		var prefix = command.AsSpan(0, prefixLength);

		var index = 0;
		var failed = false;
		while (true) {
			SkipWhiteSpace(prefix, ref index);
			if (index >= prefix.Length) {
				break;
			}

			if (TryParsePosixAssignment(prefix, ref index, out var key, out var value)) {
				SkipWhiteSpace(prefix, ref index);
				if (index < prefix.Length && prefix[index] != '&' && !StartsAssignment(prefix[index..])) {
					failed = true;
					break;
				}

				if (index < prefix.Length && prefix[index] == '&') {
					if (index + 1 >= prefix.Length || prefix[index + 1] != '&') {
						failed = true;
						break;
					}

					index += 2;
				}

				environment[key] = value;
				continue;
			}

			if (TryParseSetAssignment(prefix, ref index, out key, out var setValue)) {
				environment[key] = setValue;
				continue;
			}

			failed = true;
			break;
		}

		if (failed) {
			environment.Clear();
			return environment;
		}

		if (index > 0) {
			command = string.Concat(prefix[index..].TrimStart(), command.AsSpan(prefixLength));
		}

		return environment;
	}

	static private bool TryParsePosixAssignment(
		ReadOnlySpan<char> text,
		ref int index,
		out string key,
		out string value) {
		key = string.Empty;
		value = string.Empty;
		var start = index;
		if (!TryReadName(text, ref index, out var name)) {
			index = start;
			return false;
		}

		if (index >= text.Length || text[index] != '=') {
			index = start;
			return false;
		}

		index++;
		if (!TryReadAssignmentValue(text, ref index, out value)) {
			index = start;
			return false;
		}

		key = name.ToString();
		return true;
	}

	static private bool TryParseSetAssignment(
		ReadOnlySpan<char> text,
		ref int index,
		out string key,
		out string? value) {
		key = string.Empty;
		value = null;
		var start = index;
		if (index + 3 > text.Length ||
			!text[index..].StartsWith("set", StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		index += 3;
		if (index >= text.Length || !char.IsWhiteSpace(text[index])) {
			index = start;
			return false;
		}

		SkipWhiteSpace(text, ref index);
		if (index >= text.Length) {
			index = start;
			return false;
		}

		string? parsedValue;
		if (text[index] == '"') {
			var close = text[(index + 1)..].IndexOf('"');
			if (close < 0) {
				index = start;
				return false;
			}

			var inner = text.Slice(index + 1, close);
			var equals = inner.IndexOf('=');
			if (equals <= 0 || !IsValidName(inner[..equals])) {
				index = start;
				return false;
			}

			key = inner[..equals].ToString();
			parsedValue = inner[(equals + 1)..].ToString();
			index += close + 2;
		} else {
			if (!TryReadName(text, ref index, out var name)) {
				index = start;
				return false;
			}

			if (index >= text.Length || text[index] != '=') {
				index = start;
				return false;
			}

			index++;
			var valueStart = index;
			while (index < text.Length &&
				!char.IsWhiteSpace(text[index]) &&
				text[index] is not ('&' or '|')) {
				index++;
			}

			if (index < text.Length && text[index] is '&' or '|') {
				index = start;
				return false;
			}

			key = name.ToString();
			parsedValue = text[valueStart..index].ToString();
		}

		if (parsedValue.Contains('%') || parsedValue.Contains('^')) {
			index = start;
			key = string.Empty;
			return false;
		}

		SkipWhiteSpace(text, ref index);
		if (index >= text.Length || text[index] != '&') {
			index = start;
			key = string.Empty;
			return false;
		}

		index += index + 1 < text.Length && text[index + 1] == '&' ? 2 : 1;
		value = parsedValue.Length == 0 ? null : parsedValue;
		return true;
	}

	static private bool TryReadAssignmentValue(ReadOnlySpan<char> text, ref int index, out string value) {
		value = string.Empty;
		if (index >= text.Length) {
			return true;
		}

		var quote = text[index];
		if (quote is '"' or '\'') {
			var close = text[(index + 1)..].IndexOf(quote);
			if (close < 0) {
				return false;
			}

			var content = text.Slice(index + 1, close);
			if (quote == '"' && ContainsExpansion(content)) {
				return false;
			}

			value = content.ToString();
			index += close + 2;
			return true;
		}

		var end = index;
		while (end < text.Length && !char.IsWhiteSpace(text[end]) && !IsAssignmentTerminator(text[end])) {
			end++;
		}

		if (end == index) {
			return false;
		}

		var raw = text[index..end];
		if (ContainsExpansion(raw)) {
			return false;
		}

		value = raw.ToString();
		index = end;
		return true;
	}

	static private bool ContainsExpansion(ReadOnlySpan<char> value) {
		return value.IndexOfAny('$', '`', '%') >= 0;
	}

	static private bool IsAssignmentTerminator(char value) {
		return value is '&' or '|' or ';' or '<' or '>' or '(' or ')';
	}

	static private bool StartsAssignment(ReadOnlySpan<char> text) {
		if (text.IsEmpty) {
			return false;
		}

		if (text.StartsWith("set", StringComparison.OrdinalIgnoreCase)) {
			return text.Length == 3 || char.IsWhiteSpace(text[3]);
		}

		var index = 0;
		return TryReadName(text, ref index, out _) && index < text.Length && text[index] == '=';
	}

	static private bool TryReadName(ReadOnlySpan<char> text, ref int index, out ReadOnlySpan<char> name) {
		name = default;
		if (index >= text.Length || !(char.IsAsciiLetter(text[index]) || text[index] == '_')) {
			return false;
		}

		var start = index;
		index++;
		while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] == '_')) {
			index++;
		}

		name = text[start..index];
		return true;
	}

	static private bool IsValidName(ReadOnlySpan<char> name) {
		if (name.IsEmpty || !(char.IsAsciiLetter(name[0]) || name[0] == '_')) {
			return false;
		}

		foreach (var c in name[1..]) {
			if (!char.IsAsciiLetterOrDigit(c) && c != '_') {
				return false;
			}
		}

		return true;
	}

	static private void SkipWhiteSpace(ReadOnlySpan<char> text, ref int index) {
		while (index < text.Length && char.IsWhiteSpace(text[index])) {
			index++;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace MVL.Utils;

[JsonConverter(typeof(LocalizedStringConverter))]
public struct LocalizedString(string value) {
	public string Value {
		get {
			if (field is null && Localizations != null && Localizations.Count != 0) {
				return Localizations.GetAt(0).Value;
			}

			return field ?? string.Empty;
		}
		init;
	} = value;

	public OrderedDictionary<string, string>? Localizations { get; set; }
	public static LocalizedString Empty { get; } = new(string.Empty);

	static private readonly string[] SimplifiedChineseKeys = [
		"zh_Hans", "zh-Hans", "zh_CN", "zh-CN", "zh_SG", "zh-SG", "zh"
	];

	static private readonly string[] TraditionalChineseKeys = [
		"zh_Hant", "zh-Hant", "zh_TW", "zh-TW", "zh_HK", "zh-HK", "zh_MO", "zh-MO", "zh"
	];

	public string GetText() {
		if (Localizations is not { Count: > 0 } localizations) {
			return Value;
		}

		var locale = TranslationServer.GetLocale().ToString();
		if (localizations.TryGetValue(locale, out var text)) {
			return text;
		}

		if (IsChineseLocale(locale)) {
			var traditional = IsTraditionalChinese(locale);
			var keys = traditional ? TraditionalChineseKeys : SimplifiedChineseKeys;

			foreach (var key in keys) {
				if (TryGetChineseLocalization(localizations, key, out var chineseText)) {
					return chineseText;
				}
			}

			var chineseBestScore = 0;
			var chineseBestText = Value;
			foreach (var (language, localizedText) in localizations) {
				if (!IsChineseLocale(language) || IsTraditionalChinese(language) != traditional) {
					continue;
				}

				var score = TranslationServer.CompareLocales(locale, language);
				if (score > chineseBestScore) {
					chineseBestScore = score;
					chineseBestText = localizedText;
				}
			}

			return chineseBestText;
		}

		var bestScore = 0;
		var bestText = Value;
		foreach (var (language, localizedText) in localizations) {
			var score = TranslationServer.CompareLocales(locale, language);
			if (score > bestScore) {
				bestScore = score;
				bestText = localizedText;
			}
		}

		return bestText;
	}

	static private bool IsChineseLocale(ReadOnlySpan<char> locale) {
		return locale.Length >= 2 && locale[..2].Equals("zh", StringComparison.OrdinalIgnoreCase) &&
			(locale.Length == 2 || locale[2] is '_' or '-');
	}

	static private bool TryGetChineseLocalization(OrderedDictionary<string, string> localizations, string key,
		out string value) {
		if (localizations.TryGetValue(key, out var found)) {
			value = found ?? string.Empty;
			return true;
		}

		foreach (var (language, text) in localizations) {
			if (language.Equals(key, StringComparison.OrdinalIgnoreCase)) {
				value = text;
				return true;
			}
		}

		value = string.Empty;
		return false;
	}

	static private bool IsTraditionalChinese(ReadOnlySpan<char> locale) {
		var rest = locale[2..];
		while (!rest.IsEmpty) {
			if (rest[0] is '_' or '-') {
				rest = rest[1..];
				continue;
			}

			var separatorIndex = rest.IndexOfAny('_', '-');
			var part = separatorIndex < 0 ? rest : rest[..separatorIndex];

			if (part.Equals("Hant", StringComparison.OrdinalIgnoreCase) ||
				part.Equals("TW", StringComparison.OrdinalIgnoreCase) ||
				part.Equals("HK", StringComparison.OrdinalIgnoreCase) ||
				part.Equals("MO", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}

			if (part.Equals("Hans", StringComparison.OrdinalIgnoreCase) ||
				part.Equals("CN", StringComparison.OrdinalIgnoreCase) ||
				part.Equals("SG", StringComparison.OrdinalIgnoreCase)) {
				return false;
			}

			if (separatorIndex < 0) {
				break;
			}

			rest = rest[(separatorIndex + 1)..];
		}

		return false;
	}
}

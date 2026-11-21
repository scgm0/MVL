using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using FileAccess = System.IO.FileAccess;

// ReSharper disable StaticMemberInGenericType

namespace MVL.Utils.Help;

public static class LocalizedStringJsonHelper {
	static private class MetadataCache<T> where T : notnull {
		static private volatile FrozenDictionary<string, JsonPropertyInfo>? _localizedProps;
		static private readonly Lock Lock = new();

		public static FrozenDictionary<string, JsonPropertyInfo> GetLocalizedProps(JsonTypeInfo<T> typeInfo) {
			var props = _localizedProps;
			if (props != null) {
				return props;
			}

			lock (Lock) {
				props = _localizedProps;
				if (props != null) {
					return props;
				}

				var dict = new Dictionary<string, JsonPropertyInfo>();
				foreach (var prop in typeInfo.Properties) {
					if (prop.PropertyType == typeof(LocalizedString) && prop is { Get: not null, Set: not null }) {
						dict[prop.Name] = prop;
					}
				}

				props = dict.ToFrozenDictionary();
				_localizedProps = props;
				return props;
			}
		}
	}

	public static void SaveWithOrderedLocalizations<T>(T obj, string filePath, JsonTypeInfo<T> typeInfo) where T : notnull {
		var propsMap = MetadataCache<T>.GetLocalizedProps(typeInfo);
		var writerOptions = CreateWriterOptions(typeInfo.Options);

		using var doc = JsonSerializer.SerializeToDocument(obj, typeInfo);
		using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
		using var writer = new Utf8JsonWriter(fs, writerOptions);

		writer.WriteStartObject();

		foreach (var prop in doc.RootElement.EnumerateObject()) {
			prop.WriteTo(writer);

			if (propsMap.TryGetValue(prop.Name, out var pInfo)) {
				WriteLocalizations(writer, prop.Name, (LocalizedString)pInfo.Get!.Invoke(obj)!);
			}
		}

		writer.WriteEndObject();
	}

	static private JsonWriterOptions CreateWriterOptions(JsonSerializerOptions options) {
		return new JsonWriterOptions {
			Indented = options.WriteIndented,
			Encoder = options.Encoder,
			SkipValidation = false,
			NewLine = options.NewLine
		};
	}

	static private void WriteLocalizations(Utf8JsonWriter writer, string propertyName, LocalizedString localizedString) {
		if (localizedString.Localizations is not { Count: > 0 } localizations) {
			return;
		}

		Span<char> stackBuffer = stackalloc char[256];

		foreach (var kvp in localizations) {
			var totalLen = propertyName.Length + kvp.Key.Length + 2;

			char[]? pooledArray = null;
			scoped Span<char> span;

			if (totalLen <= stackBuffer.Length) {
				span = stackBuffer[..totalLen];
			} else {
				pooledArray = ArrayPool<char>.Shared.Rent(totalLen);
				span = pooledArray.AsSpan(0, totalLen);
			}

			propertyName.AsSpan().CopyTo(span);
			span[propertyName.Length] = '[';
			kvp.Key.CopyTo(span[(propertyName.Length + 1)..]);
			span[^1] = ']';

			writer.WriteString(span, kvp.Value);

			if (pooledArray != null) {
				ArrayPool<char>.Shared.Return(pooledArray);
			}
		}
	}

	public static void RestoreLocalizationsFromExtensionData<T>(
		T obj,
		JsonTypeInfo<T> typeInfo,
		IDictionary<string, JsonElement>? extensionData,
		bool keepExtensionData = false) where T : notnull {
		if (extensionData == null || extensionData.Count == 0) {
			return;
		}

		var propsMap = MetadataCache<T>.GetLocalizedProps(typeInfo);
		if (propsMap.Count == 0) {
			return;
		}

		var altLookup = propsMap.GetAlternateLookup<ReadOnlySpan<char>>();
		List<string>? keysToRemove = null;

		foreach (var (key, value) in extensionData) {
			if (value.ValueKind != JsonValueKind.String) {
				continue;
			}

			var openBracket = key.IndexOf('[');
			if (openBracket <= 0 || key[^1] != ']') {
				continue;
			}

			var prefixSpan = key.AsSpan(0, openBracket);

			if (!altLookup.TryGetValue(prefixSpan, out var prop) || prop is not { Get: not null, Set: not null }) {
				continue;
			}

			var lang = key.Substring(openBracket + 1, key.Length - openBracket - 2);
			var val = value.GetString() ?? string.Empty;

			var ls = (LocalizedString)prop.Get(obj)!;
			(ls.Localizations ??= new())[lang] = val;
			prop.Set(obj, ls);

			keysToRemove ??= [];
			keysToRemove.Add(key);
		}

		if (keepExtensionData || keysToRemove == null) {
			return;
		}

		foreach (var key in keysToRemove) {
			extensionData.Remove(key);
		}
	}
}
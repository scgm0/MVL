using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MVL.Utils;

[JsonConverter(typeof(LocalizedStringConverter))]
public struct LocalizedString(string value) {
	public string Value {
		get {
			if (field is null && Localizations != null && Localizations.Count != 0) {
				return Localizations.Values.First();
			}

			return field ?? string.Empty;
		}
		init;
	} = value;

	public Dictionary<string, string>? Localizations { get; set; }
	public static LocalizedString Empty { get; } = new(string.Empty);
}
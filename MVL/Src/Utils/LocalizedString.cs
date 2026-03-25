using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MVL.Utils;

[JsonConverter(typeof(LocalizedStringConverter))]
public struct LocalizedString(string value) {
	public string Value {
		get {
			if (string.IsNullOrEmpty(field) && Localizations != null && Localizations.Count != 0) {
				return Localizations.Values.First();
			}

			return field;
		}
		init;
	} = value;

	public Dictionary<string, string>? Localizations { get; set; }
	public override string ToString() { return Value; }
	public static LocalizedString Empty { get; } = new(string.Empty);
}
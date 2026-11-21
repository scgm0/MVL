using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MVL.Utils.Help;

namespace MVL.Utils.Multiplayer;

public class NodeSubscription : IJsonOnDeserialized {
	public LocalizedString Name { get; set; }
	public required SharedNode[] Nodes { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement>? ExtensionData { get; set; }

	public void RestoreLocalizations() {
		LocalizedStringJsonHelper.RestoreLocalizationsFromExtensionData(this,
			SourceGenerationContext.Default.NodeSubscription, ExtensionData);
	}

	void IJsonOnDeserialized.OnDeserialized() { RestoreLocalizations(); }
}

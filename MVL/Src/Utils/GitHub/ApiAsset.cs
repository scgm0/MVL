using System.Text.Json.Serialization;

namespace MVL.Utils.GitHub;

public struct ApiAsset {
	public string Name { get; set; }

	[JsonPropertyName("browser_download_url")]
	public string BrowserDownloadUrl { get; set; }
}
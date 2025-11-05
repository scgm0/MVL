using System.Text.Json.Serialization;

namespace MVL.Utils.GitHub;

public struct ApiRelease {
	[JsonPropertyName("tag_name")]
	public string TagName { get; set; }

	public string Body { get; set; }

	public ApiAsset[] Assets { get; set; }
}
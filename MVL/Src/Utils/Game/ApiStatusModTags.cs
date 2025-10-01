namespace MVL.Utils.Game;

public struct ApiStatusModTags {
	public required string StatusCode { get; init; }
	public ApiModTag[]? Tags { get; init; }
}
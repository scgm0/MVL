namespace MVL.Utils.Game;

public struct ApiStatusAuthors {
	public required string StatusCode { get; init; }
	public ApiAuthor?[]? Authors { get; init; }
}
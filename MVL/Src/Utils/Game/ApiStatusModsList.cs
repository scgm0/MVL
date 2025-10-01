namespace MVL.Utils.Game;

public struct ApiStatusModsList {
	public required string StatusCode { get; init; }
	public ApiModSummary[]? Mods { get; init; }
}
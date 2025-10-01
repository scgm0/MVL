namespace MVL.Utils.Game;

public struct ApiStatusModInfo {
	public required string StatusCode { get; init; }
	public ApiModInfo? Mod { get; init; }
}
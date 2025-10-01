namespace MVL.Utils.Game;

public struct ApiStatusGameVersions {
	public required string StatusCode { get; init; }
	public ApiGameVersion[]? GameVersions { get; init; }
}
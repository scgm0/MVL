namespace MVL.Utils.Game;

public class ApiStatusGameVersions {
	public required string StatusCode { get; init; }
	public ApiGameVersion[] GameVersions { get; init; } = [];
}
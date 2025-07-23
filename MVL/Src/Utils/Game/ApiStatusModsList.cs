namespace MVL.Utils.Game;

public class ApiStatusModsList {
	public required string StatusCode { get; init; }
	public ApiModSummary[]? Mods { get; init; }
}
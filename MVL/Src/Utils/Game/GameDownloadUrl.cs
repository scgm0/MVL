namespace MVL.Utils.Game;

public readonly record struct GameDownloadUrl() {
	public required string? Cdn { get; init; } = string.Empty;
	public required string Local { get; init; } = string.Empty;
}
namespace MVL.Utils.Game;

public readonly record struct GameDownloadInfo() {
	public required string FileName { get; init; } = string.Empty;
	public required string FileSize { get; init; } = string.Empty;
	public required string Md5 { get; init; } = string.Empty;
	public required GameDownloadUrl Urls { get; init; } = default;
	public int Latest { get; init; } = 0;
}
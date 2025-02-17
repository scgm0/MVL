namespace MVL.Utils.Game;

public readonly record struct GameRelease {
	public required GameDownloadInfo Windows { get; init; }
	public required GameDownloadInfo Linux { get; init; }
}
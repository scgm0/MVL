namespace 复古物语启动器.Utils.Game;

public record ReleaseInfo {
	public string Path { get; set; } = string.Empty;
	public GameVersion Version { get; set; }
}
namespace MVL.Utils.Game;

public record ReleaseInfo {
	public string Path { get; set; } = string.Empty;

	public GameVersion Version { get; set; }

	public string Name { get; set; } = string.Empty;
}
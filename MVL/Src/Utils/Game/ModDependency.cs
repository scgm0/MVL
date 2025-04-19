namespace MVL.Utils.Game;
public record ModDependency {
	public required string ModId { get; init; }
	public required string Version { get; init; }
}
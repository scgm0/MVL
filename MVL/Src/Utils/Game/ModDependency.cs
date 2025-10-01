namespace MVL.Utils.Game;
public record struct ModDependency {
	public required string ModId { get; init; }
	public required string Version { get; init; }
}
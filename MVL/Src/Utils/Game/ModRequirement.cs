namespace MVL.Utils.Game;

public record struct ModRequirement {
	public required int? ModId { get; init; }
	public required string ModStrId { get; init; }
	public required string Version { get; init; }
}
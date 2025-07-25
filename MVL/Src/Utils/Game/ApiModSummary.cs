using System;

namespace MVL.Utils.Game;

public record ApiModSummary {
	public int ModId { get; init; }
	public int AssetId { get; init; }
	public int Downloads { get; init; }
	public int Follows { get; init; }
	public int TrendingPoints { get; init; }
	public int Comments { get; init; }
	public required string Name { get; init; }
	public required string Summary { get; init; }
	public required string[] ModIdStrs { get; init; }
	public required string Author { get; init; }
	public string? UrlAlias { get; init; }
	public required string Side { get; init; }
	public required string Type { get; init; }
	public string? Logo { get; init; }
	public required string[] Tags { get; init; }
	public DateTimeOffset LastReleased { get; init; }
}
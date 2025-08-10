using System;

namespace MVL.Utils.Game;

public record ApiModInfo {
	public int ModId { get; init; }
	public int AssetId { get; init; }
	public string Name { get; init; } = "";
	public string Text { get; init; } = "";
	public string Author { get; init; } = "";
	public string? UrlAlias { get; init; }
	public string? LogoFileName { get; init; }
	public string? LogoFile { get; init; }
	public string? HomePageUrl { get; init; }
	public string? SourceCodeUrl { get; init; }
	public string? TrailerVideoUrl { get; init; }
	public string? IssueTrackerUrl { get; init; }
	public string? WikiUrl { get; init; }
	public int Downloads { get; init; }
	public int Follows { get; init; }
	public int TrendingPoints { get; init; }
	public int Comments { get; init; }
	public string Side { get; init; } = "";
	public string Type { get; init; } = "";
	public DateTimeOffset Created { get; init; }
	public DateTimeOffset LastModified { get; init; }
	public string[] Tags { get; init; } = [];
	public ApiModRelease[] Releases { get; set; } = [];
}
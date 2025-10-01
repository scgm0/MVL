using System;

namespace MVL.Utils.Game;

public record struct ApiModInfo() {
	public int ModId { get; init; } = 0;
	public int AssetId { get; init; } = 0;
	public string Name { get; init; } = "";
	public string Text { get; init; } = "";
	public string Author { get; init; } = "";
	public string? UrlAlias { get; init; } = null;
	public string? LogoFileName { get; init; } = null;
	public string? LogoFile { get; init; } = null;
	public string? HomePageUrl { get; init; } = null;
	public string? SourceCodeUrl { get; init; } = null;
	public string? TrailerVideoUrl { get; init; } = null;
	public string? IssueTrackerUrl { get; init; } = null;
	public string? WikiUrl { get; init; } = null;
	public int Downloads { get; init; } = 0;
	public int Follows { get; init; } = 0;
	public int TrendingPoints { get; init; } = 0;
	public int Comments { get; init; } = 0;
	public string Side { get; init; } = "";
	public string Type { get; init; } = "";
	public DateTimeOffset Created { get; init; } = default;
	public DateTimeOffset LastModified { get; init; } = default;
	public string[] Tags { get; init; } = [];
	public ApiModRelease[] Releases { get; set; } = [];
}
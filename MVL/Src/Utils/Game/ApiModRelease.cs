using System;

namespace MVL.Utils.Game;

public record struct ApiModRelease {
	public ApiModRelease() { }
	public int ReleaseId { get; init; } = 0;
	public string MainFile { get; init; } = "";
	public string FileName { get; init; } = "";
	public int? FileId { get; init; } = null;
	public int Downloads { get; init; } = 0;
	public string[] Tags { get; init; } = [];
	public string ModIdStr { get; init; } = "";
	public string ModVersion { get; init; } = "1.0.0";
	public DateTimeOffset Created { get; init; } = default;
}
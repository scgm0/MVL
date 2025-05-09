using System;

namespace MVL.Utils.Game;

public record ApiModRelease {
	public int ReleaseId { get; init; }
	public string MainFile { get; init; } = "";
	public string FileName { get; init; } = "";
	public int FileId { get; init; }
	public int Downloads { get; init; }
	public string[] Tags { get; init; } = [];
	public string ModIdStr { get; init; } = "";
	public string ModVersion { get; init; } = "1.0.0";
	public DateTimeOffset Created { get; init; }
}
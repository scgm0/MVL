using System;

namespace MVL.Utils.Downloader;

public readonly record struct DownloadProgress(
	long BytesDownloaded,
	long TotalBytes,
	long BytesDelta,
	TimeSpan ElapsedDelta
) {
	public double Percentage => TotalBytes > 0 ? BytesDownloaded * 100.0 / TotalBytes : 0;

	public double SpeedBytesPerSecond =>
		ElapsedDelta.TotalSeconds > 0 ? Math.Max(BytesDelta, 0) / ElapsedDelta.TotalSeconds : 0;
}
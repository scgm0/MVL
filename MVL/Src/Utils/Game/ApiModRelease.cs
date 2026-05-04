using System;
using System.IO;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MVL.UI;
using MVL.Utils.Downloader;
using HttpClient = System.Net.Http.HttpClient;

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

	[JsonConverter(typeof(SqlDateTimeOffsetConverter))]
	public DateTimeOffset Created { get; init; } = default;

	public async Task DownloadMainFileAsync(
		string filePath,
		Action<DownloadProgress>? onProgress = null,
		CancellationToken cancellationToken = default) {
		using var downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = downloadTmp.GetCurrentDir();
		var tmpFile = Path.Combine(downloadDir, FileName);
		Log.Info($"开始下载: {FileName}...");
		using var download = new LightDownloader(new() {
			ChunkCount = Main.BaseConfig.DownloadThreads,
			ParallelCount = Main.BaseConfig.DownloadThreads,
			Proxy = string.IsNullOrWhiteSpace(Main.BaseConfig.ProxyAddress)
				? HttpClient.DefaultProxy
				: new WebProxy(Main.BaseConfig.ProxyAddress)
		});
		download.ProgressChanged += onProgress;
		await download.DownloadAsync(MainFile,
			tmpFile,
			cancellationToken);
		File.Move(tmpFile, filePath, true);
		Log.Info($"下载完成: {FileName}");
	}
}
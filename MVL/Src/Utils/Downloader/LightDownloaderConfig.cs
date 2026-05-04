using System;
using System.Net;

namespace MVL.Utils.Downloader;

public sealed class LightDownloaderConfig {
	public int ChunkCount { get; init; } = Environment.ProcessorCount * 2;
	public int ParallelCount { get; init; } = Environment.ProcessorCount;
	public int BufferSize { get; init; } = 65536;
	public int MaxRetries { get; init; } = 10;
	public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);
	public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);
	public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(10);
	public string UserAgent { get; init; } = "LightDownloader/1.0";
	public IWebProxy? Proxy { get; init; }
}
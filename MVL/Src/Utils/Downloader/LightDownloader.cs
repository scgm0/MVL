using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MVL.Utils.Downloader;

public sealed class LightDownloader : IDisposable {
	private readonly HttpClient _httpClient;
	private readonly LightDownloaderConfig _config;
	private bool _disposed;
	private long _completedBytes;

	private long _latestBytesDelta;
	private long _latestElapsedTicks;

	public event Action<DownloadProgress>? ProgressChanged;

	public LightDownloader(LightDownloaderConfig? config = null) {
		_config = config ?? new();

		var handler = new SocketsHttpHandler {
			AutomaticDecompression = DecompressionMethods.All,
			ConnectTimeout = _config.ConnectTimeout,
			PooledConnectionLifetime = _config.PooledConnectionLifetime,
			EnableMultipleHttp2Connections = true,
			EnableMultipleHttp3Connections = true,
			MaxConnectionsPerServer = _config.ParallelCount * 2,
			Proxy = _config.Proxy,
			UseProxy = _config.Proxy != null,
		};

		_httpClient = new(handler) {
			Timeout = _config.Timeout,
		};

		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_config.UserAgent);
		_httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
	}

	public async Task DownloadAsync(
		string url,
		string destinationPath,
		CancellationToken cancellationToken = default
	) {
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
			throw new ArgumentException("无效的URL格式", nameof(url));
		}

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);

		var (fileLength, supportsRange) = await GetFileInfoAsync(uri, cancellationToken);

		_completedBytes = 0;
		Interlocked.Exchange(ref _latestBytesDelta, 0);
		Interlocked.Exchange(ref _latestElapsedTicks, 0);

		if (!supportsRange || fileLength <= 0 || _config.ChunkCount <= 0) {
			await DownloadSingleAsync(uri, destinationPath, fileLength, cancellationToken);
		} else {
			await DownloadMultiAsync(uri, destinationPath, fileLength, cancellationToken);
		}
	}

	private async Task<(long Length, bool SupportsRange)> GetFileInfoAsync(Uri uri, CancellationToken cancellationToken) {
		try {
			using var request = new HttpRequestMessage(HttpMethod.Head, uri);
			using var response =
				await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

			if (response.IsSuccessStatusCode) {
				var supportsRange = response.Headers.AcceptRanges.ToString()
					.Contains("bytes", StringComparison.OrdinalIgnoreCase);
				var length = response.Content.Headers.ContentLength ?? -1;
				return (length, supportsRange);
			}
		} catch (OperationCanceledException) {
			throw;
		} catch {
			// ignored
		}

		try {
			using var request = new HttpRequestMessage(HttpMethod.Get, uri);
			using var response =
				await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

			if (response.IsSuccessStatusCode) {
				var supportsRange = response.Headers.AcceptRanges.ToString()
						.Contains("bytes", StringComparison.OrdinalIgnoreCase)
					|| response.StatusCode == HttpStatusCode.PartialContent;
				var length = response.Content.Headers.ContentLength ?? -1;
				return (length, supportsRange);
			}
		} catch (OperationCanceledException) {
			throw;
		} catch {
			// ignore
		}

		return (-1, false);
	}

	private async Task DownloadSingleAsync(
		Uri uri,
		string destinationPath,
		long fileLength,
		CancellationToken cancellationToken
	) {
		using var response = await _httpClient.GetAsync(
			uri,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();

		await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

		var handle = File.OpenHandle(
			destinationPath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.None,
			FileOptions.Asynchronous | FileOptions.SequentialScan,
			preallocationSize: fileLength > 0 ? fileLength : 0
		);
		try {
			var buffer = ArrayPool<byte>.Shared.Rent(_config.BufferSize);
			var stopwatch = Stopwatch.StartNew();
			var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			Task? speedLoopTask = null;

			try {
				long fileOffset = 0;
				int read;

				speedLoopTask = StartSpeedMonitorLoopAsync(stopwatch, fileLength, progressCts.Token);

				while ((read = await contentStream.ReadAsync(
						buffer.AsMemory(0, _config.BufferSize),
						cancellationToken
					)) > 0) {
					await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), fileOffset, cancellationToken);
					fileOffset += read;
					Interlocked.Add(ref _completedBytes, read);

					ReportProgress(fileLength);
				}

				ReportProgress(fileLength, isFinal: true);
			} finally {
				await progressCts.CancelAsync();
				if (speedLoopTask != null) {
					try {
						await speedLoopTask;
					} catch {
						// ignored
					}
				}

				ArrayPool<byte>.Shared.Return(buffer);
				stopwatch.Stop();
			}
		} finally {
			handle.Dispose();
		}
	}

	private async Task DownloadMultiAsync(
		Uri uri,
		string destinationPath,
		long fileLength,
		CancellationToken cancellationToken
	) {
		var chunks = CreateChunks(fileLength, _config.ChunkCount);
		var stopwatch = Stopwatch.StartNew();

		var handle = File.OpenHandle(
			destinationPath,
			FileMode.Create,
			FileAccess.ReadWrite,
			FileShare.None,
			FileOptions.Asynchronous | FileOptions.RandomAccess,
			preallocationSize: fileLength
		);
		try {
			RandomAccess.SetLength(handle, fileLength);

			var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var speedLoopTask = StartSpeedMonitorLoopAsync(stopwatch, fileLength, progressCts.Token);

			try {
				await Parallel.ForEachAsync(
					chunks,
					new ParallelOptions {
						MaxDegreeOfParallelism = Math.Min(_config.ParallelCount, chunks.Length),
						CancellationToken = cancellationToken,
					},
					async (chunk, ct) => {
						await DownloadChunkWithRetryAsync(
							chunk.Index,
							uri,
							handle,
							chunk.Start,
							chunk.End,
							fileLength,
							ct
						);
					}
				);

				ReportProgress(fileLength, isFinal: true);
			} finally {
				await progressCts.CancelAsync();
				try {
					await speedLoopTask;
				} catch {
					/* ignore */
				}
			}
		} finally {
			stopwatch.Stop();
			handle.Dispose();
		}
	}

	private async Task DownloadChunkWithRetryAsync(
		int index,
		Uri uri,
		SafeFileHandle handle,
		long start,
		long end,
		long totalBytes,
		CancellationToken cancellationToken
	) {
		var currentStart = start;

		for (var attempt = 0; attempt <= _config.MaxRetries; attempt++) {
			try {
				cancellationToken.ThrowIfCancellationRequested();

				if (currentStart > end) {
					return;
				}

				await DownloadChunkAsync(
					uri,
					handle,
					currentStart,
					end,
					read => {
						Interlocked.Add(ref _completedBytes, read);
						currentStart += read;
						ReportProgress(totalBytes);
					},
					cancellationToken
				);
				return;
			} catch (OperationCanceledException) {
				throw;
			} catch (Exception ex) when (IsTransientError(ex) && attempt < _config.MaxRetries) {
				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) +
					TimeSpan.FromMilliseconds(Random.Shared.Next(100, 1000));
				await Task.Delay(delay, cancellationToken);
			}
		}
	}

	private async Task DownloadChunkAsync(
		Uri uri,
		SafeFileHandle handle,
		long start,
		long end,
		Action<long>? onBytesRead,
		CancellationToken cancellationToken
	) {
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		request.Headers.Range = new(start, end);

		using var response = await _httpClient.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken
		);
		response.EnsureSuccessStatusCode();

		await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

		var buffer = ArrayPool<byte>.Shared.Rent(_config.BufferSize);
		try {
			var fileOffset = start;
			int read;
			while ((read = await contentStream.ReadAsync(
					buffer.AsMemory(0, _config.BufferSize),
					cancellationToken
				)) > 0) {
				await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), fileOffset, cancellationToken);
				fileOffset += read;

				onBytesRead?.Invoke(read);
			}
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private async Task StartSpeedMonitorLoopAsync(Stopwatch stopwatch, long totalBytes, CancellationToken token) {
		long lastBytes = 0;
		long lastTicks = 0;

		while (!token.IsCancellationRequested) {
			try {
				await Task.Delay(200, token);
			} catch (OperationCanceledException) {
				break;
			}

			var currentBytes = Interlocked.Read(ref _completedBytes);
			var currentTicks = stopwatch.Elapsed.Ticks;

			var deltaBytes = currentBytes - lastBytes;
			var deltaTicks = currentTicks - lastTicks;

			Interlocked.Exchange(ref _latestBytesDelta, deltaBytes);
			Interlocked.Exchange(ref _latestElapsedTicks, deltaTicks);

			ReportProgress(totalBytes);

			lastBytes = currentBytes;
			lastTicks = currentTicks;
		}
	}

	private void ReportProgress(long totalBytes, bool isFinal = false) {
		if (ProgressChanged == null) {
			return;
		}

		var currentBytes = Interlocked.Read(ref _completedBytes);

		if (isFinal && totalBytes <= 0) {
			totalBytes = currentBytes;
		}

		if (totalBytes > 0 && currentBytes > totalBytes) {
			currentBytes = totalBytes;
		}

		var bytesDelta = Interlocked.Read(ref _latestBytesDelta);
		var ticks = Interlocked.Read(ref _latestElapsedTicks);

		ProgressChanged.Invoke(new(
			currentBytes,
			totalBytes,
			bytesDelta,
			TimeSpan.FromTicks(ticks)
		));
	}

	static private (int Index, long Start, long End)[] CreateChunks(long totalLength, int chunkCount) {
		var chunks = new (int Index, long Start, long End)[chunkCount];
		var chunkSize = totalLength / chunkCount;

		for (var i = 0; i < chunkCount; i++) {
			var start = i * chunkSize;
			var end = i == chunkCount - 1
				? totalLength - 1
				: (i + 1) * chunkSize - 1;
			chunks[i] = (i, start, end);
		}

		return chunks;
	}

	static private bool IsTransientError(Exception ex) {
		if (ex is HttpRequestException httpEx) {
			if (httpEx.StatusCode.HasValue) {
				return httpEx.StatusCode switch {
					HttpStatusCode.PreconditionRequired => true,
					HttpStatusCode.TooManyRequests => true,
					HttpStatusCode.RequestTimeout => true,
					HttpStatusCode.BadGateway => true,
					HttpStatusCode.ServiceUnavailable => true,
					HttpStatusCode.GatewayTimeout => true,
					_ => false
				};
			}

			return httpEx.HttpRequestError switch {
				HttpRequestError.ConnectionError => true,
				HttpRequestError.ResponseEnded => true,
				HttpRequestError.HttpProtocolError => true,
				HttpRequestError.SecureConnectionError => true,
				HttpRequestError.NameResolutionError => true,
				HttpRequestError.Unknown => true,
				_ => false
			};
		}

		return ex is IOException or SocketException;
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_httpClient.Dispose();
		_disposed = true;
	}
}
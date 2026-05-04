using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MVL.Utils.Downloader;

public sealed class LightDownloader : IDisposable {
	private readonly HttpClient _httpClient;
	private readonly LightDownloaderConfig _config;
	private bool _disposed;
	private long _completedBytes;
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

		var uri = new Uri(url);
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);

		var (fileLength, supportsRange) = await GetFileInfoAsync(uri, cancellationToken);

		var progress = new Progress<DownloadProgress>(p => ProgressChanged?.Invoke(p));
		if (!supportsRange || fileLength <= 0 || _config.ChunkCount <= 0) {
			await DownloadSingleAsync(uri, destinationPath, fileLength, progress, cancellationToken);
		} else {
			await DownloadMultiAsync(uri, destinationPath, fileLength, progress, cancellationToken);
		}
	}

	private async Task<(long Length, bool SupportsRange)> GetFileInfoAsync(Uri uri, CancellationToken cancellationToken) {
		try {
			using var request = new HttpRequestMessage(HttpMethod.Head, uri);
			using var response =
				await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

			if (response.IsSuccessStatusCode) {
				var supportsRange = response.Headers.AcceptRanges.Contains("bytes");
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
				var supportsRange = response.Headers.AcceptRanges.Contains("bytes");
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
		IProgress<DownloadProgress>? progress,
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
			preallocationSize: fileLength
		);
		try {
			var buffer = ArrayPool<byte>.Shared.Rent(_config.BufferSize);
			var stopwatch = Stopwatch.StartNew();
			Task? progressTask = null;
			var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			_completedBytes = 0;
			try {
				long fileOffset = 0;
				int read;

				if (progress != null && fileLength > 0) {
					progressTask = ProgressLoopAsync(
						progress,
						stopwatch,
						() => _completedBytes,
						fileLength,
						progressCts.Token
					);
				}

				while ((read = await contentStream.ReadAsync(
						buffer.AsMemory(0, _config.BufferSize),
						cancellationToken
					)) > 0) {
					await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), fileOffset, cancellationToken);
					fileOffset += read;
					_completedBytes += read;
				}

				if (fileLength <= 0) {
					progress?.Report(new(1, 1, 0, TimeSpan.Zero));
				} else {
					var finalBytes = Math.Min(_completedBytes, fileLength);
					progress?.Report(new(finalBytes, fileLength, 0, TimeSpan.Zero));
				}
			} finally {
				await progressCts.CancelAsync();
				if (progressTask != null) {
					try {
						await progressTask;
					} catch {
						/* ignore */
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
		IProgress<DownloadProgress>? progress,
		CancellationToken cancellationToken
	) {
		_completedBytes = 0;
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

			Task? progressTask = null;
			var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (progress != null) {
				progressTask = ProgressLoopAsync(
					progress,
					stopwatch,
					() => Interlocked.Read(ref _completedBytes),
					fileLength,
					progressCts.Token
				);
			}

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
						ct
					);
				}
			);

			progress?.Report(new(fileLength, fileLength, 0, TimeSpan.Zero));
			await progressCts.CancelAsync();
			if (progressTask != null) {
				try {
					await progressTask;
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
		CancellationToken cancellationToken
	) {
		long attemptBytes = 0;
		for (var attempt = 0; attempt <= _config.MaxRetries; attempt++) {
			try {
				cancellationToken.ThrowIfCancellationRequested();
				await DownloadChunkAsync(uri,
					handle,
					start,
					end,
					read => {
						Interlocked.Add(ref _completedBytes, read);
						attemptBytes += read;
					},
					cancellationToken);
				return;
			} catch (OperationCanceledException) {
				throw;
			} catch (HttpRequestException ex) when (IsTransientError(ex) && attempt < _config.MaxRetries) {
				if (attemptBytes > 0) {
					Interlocked.Add(ref _completedBytes, -attemptBytes);
					attemptBytes = 0;
				}

				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
				await Task.Delay(delay, cancellationToken);
			} catch (IOException) when (attempt < _config.MaxRetries) {
				if (attemptBytes > 0) {
					Interlocked.Add(ref _completedBytes, -attemptBytes);
					attemptBytes = 0;
				}

				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
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

	static private async Task ProgressLoopAsync(
		IProgress<DownloadProgress> progress,
		Stopwatch stopwatch,
		Func<long> getBytes,
		long totalBytes,
		CancellationToken token
	) {
		long lastBytes = 0;
		var lastElapsed = TimeSpan.Zero;
		while (!token.IsCancellationRequested) {
			try {
				await Task.Delay(200, token);
			} catch (OperationCanceledException) {
				break;
			}

			token.ThrowIfCancellationRequested();
			var currentBytes = Math.Min(getBytes(), totalBytes);
			var currentElapsed = stopwatch.Elapsed;
			var deltaBytes = currentBytes - lastBytes;
			var deltaElapsed = currentElapsed - lastElapsed;
			progress.Report(new(currentBytes, totalBytes, deltaBytes, deltaElapsed));
			lastBytes = currentBytes;
			lastElapsed = currentElapsed;
		}
	}

	static private bool IsTransientError(HttpRequestException ex) {
		return ex.StatusCode switch {
			HttpStatusCode.PreconditionRequired => true,
			HttpStatusCode.TooManyRequests => true,
			HttpStatusCode.RequestTimeout => true,
			HttpStatusCode.BadGateway => true,
			HttpStatusCode.ServiceUnavailable => true,
			HttpStatusCode.GatewayTimeout => true,
			_ => false
		};
	}

	public void Dispose() {
		if (_disposed) {
			return;
		}

		_httpClient.Dispose();
		_disposed = true;
	}
}
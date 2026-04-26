using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Downloader;
using Flurl.Http;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using MVL.Utils.Help;
using Environment = System.Environment;
using FileAccess = System.IO.FileAccess;
using HttpClient = System.Net.Http.HttpClient;

namespace MVL.UI.Window;

public partial class GameDownloadWindow : BaseWindow {
	[Export]
	private ButtonGroup? _buttonGroup;

	[Export]
	private LineEdit? _releaseName;

	[Export]
	private LineEdit? _releasePath;

	[Export]
	private Button? _folderButton;

	[Export]
	private Label? _tooltip;

	[Export]
	private FileDialog? _fileDialog;

	[Export]
	private PackedScene? _downloadItemScene;

	[Export]
	private Control? _contentContainer;

	[Export]
	private Control? _downloadListContainer;

	[Export]
	private Control? _loadingControl;

	[Export]
	private ProgressBar? _progressBar;

	[Export]
	private Label? _progressLabel;

	[Export]
	private Button? _importButton;

	public event Action<string>? InstallGame;

	private Dictionary<GameVersion, GameRelease>? _releases;

	private IDownload? _download;
	private DirAccess? _downloadTmp;
	private CancellationTokenSource? _cancellation;
	private string? _lastException;

	public override void _Ready() {
		base._Ready();
		NullExceptionHelper.NotNull(_buttonGroup,
			_releaseName,
			_releasePath,
			_folderButton,
			_tooltip,
			_fileDialog,
			_downloadItemScene,
			_contentContainer,
			_downloadListContainer,
			_loadingControl,
			_progressBar,
			_importButton);

		_loadingControl.Show();
		_contentContainer.Hide();
		_progressBar.Hide();

		_releasePath.Text = Path.Combine(Main.BaseConfig.ReleaseFolder, _releaseName.Text).NormalizePath();
		_fileDialog.CurrentPath = Main.BaseConfig.ReleaseFolder;
		_fileDialog.CurrentDir = Main.BaseConfig.ReleaseFolder;

		_releaseName.TextChanged += ReleaseNameOnTextChanged;
		_folderButton.Pressed += _fileDialog.Show;
		_fileDialog.DirSelected += FileDialogOnDirSelected;
		_releasePath.TextChanged += ReleasePathOnTextChanged;
		_buttonGroup.Pressed += ButtonGroupOnPressed;
		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;
		_importButton.Pressed += ImportButtonOnPressed;

		ValidateInputs();
	}

	private void ImportButtonOnPressed() {
		var fileWindow = new FileDialog {
			Access = FileDialog.AccessEnum.Filesystem,
			CurrentPath = Main.BaseConfig.ReleaseFolder,
			CurrentDir = Main.BaseConfig.ReleaseFolder,
			FileMode = FileDialog.FileModeEnum.OpenDir,
			ShowHiddenFiles = true,
			UseNativeDialog = true
		};
		fileWindow.Canceled += fileWindow.QueueFree;
		fileWindow.DirSelected += dir => {
			fileWindow.QueueFree();
			InstallGame?.Invoke(dir);
			CancelButtonOnPressed();
		};
		AddChild(fileWindow);
		fileWindow.Show();
	}

	private void ReleaseNameOnTextChanged(string text) { ValidateInputs(); }

	private void FileDialogOnDirSelected(string path) {
		_releasePath!.Text = path;
		ValidateInputs();
	}

	private void ReleasePathOnTextChanged(string text) { ValidateInputs(); }

	private void ValidateInputs() {
		var name = _releaseName!.Text;
		var path = _releasePath!.Text.NormalizePath();
		_importButton!.Disabled = false;
		CancelButton!.Disabled = false;

		if (_lastException != null) {
			OkButton!.Disabled = true;
			_tooltip!.Text = _lastException;
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (string.IsNullOrEmpty(name)) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "请输入名称";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "名称包含非法字符";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (string.IsNullOrEmpty(path)) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "请输入路径";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "路径包含非法字符";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (!Directory.Exists(path)) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "路径不存在";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (_buttonGroup!.GetPressedButton() is null) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "请选择版本";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		_tooltip!.Text = Directory.Exists(Path.Combine(path, name)) ? string.Format(Tr("'{0}'已存在，确定覆盖它吗？"), name) : "将自动创建文件夹";
		_tooltip.Modulate = Directory.Exists(Path.Combine(path, name)) ? Colors.Yellow : Colors.White;
		OkButton!.Disabled = false;
	}

	override protected async void CancelButtonOnPressed() {
		_download?.Pause();
		if (_cancellation != null) {
			await _cancellation.CancelAsync();
		}

		await Hide();
		EmitSignalCancel();
		_download?.Stop();
	}

	public override void _ExitTree() {
		_downloadTmp?.Dispose();
		_downloadTmp = null;
		_cancellation?.Dispose();
		_cancellation = null;
	}

	private async void OkButtonOnPressed() {
		_loadingControl!.Show();
		_contentContainer!.Hide();
		_progressBar!.Show();

		TitleLabel!.Text = "下载中...";
		OkButton!.Disabled = true;
		_importButton!.Disabled = true;
		_progressBar.Value = 0;

		var item = _buttonGroup!.GetPressedButton().GetOwner<InstalledGameItem>();
#if GODOT_WINDOWS
		var downloadInfo = _releases![item.GameVersion].Windows;
#elif GODOT_LINUXBSD
		var downloadInfo = _releases![item.GameVersion].Linux;
#endif
		var url = downloadInfo.Urls.Cdn ?? downloadInfo.Urls.Local;
		_downloadTmp?.Dispose();
		_downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = _downloadTmp.GetCurrentDir();

		UpdateProgress(0, "");
		_cancellation = new();
		_download = DownloadBuilder.New()
			.WithUrl(url)
			.WithDirectory(downloadDir)
			.WithFileName(downloadInfo.FileName)
			.WithConfiguration(new() {
				ParallelDownload = true,
				ChunkCount = Main.BaseConfig.DownloadThreads,
				ParallelCount = Main.BaseConfig.DownloadThreads,
				RequestConfiguration = new() {
					Proxy = string.IsNullOrWhiteSpace(Main.BaseConfig.ProxyAddress)
						? HttpClient.DefaultProxy
						: new WebProxy(Main.BaseConfig.ProxyAddress)
				}
			})
			.Build();

		_download.DownloadProgressChanged += (_, args) => {
			var (fmtSpeed, unit) = Tools.GetSizeAndUnit((ulong)args.BytesPerSecondSpeed);
			var text = $"{fmtSpeed:F2} {unit}/s";
			UpdateProgress(args.ProgressPercentage, text);
		};

		_download.DownloadFileCompleted += (_, args) => {
			_cancellation.Dispose();
			_cancellation = null;
			switch (args) {
				case { Cancelled: true, Error: OperationCanceledException }: {
					Log.Info($"下载取消 {downloadInfo.FileName}");
					break;
				}
				case { Cancelled: false, Error: null }: {
					Log.Info($"下载完成 {downloadInfo.FileName}");
					var path = _releasePath!.Text.NormalizePath();
					var name = _releaseName!.Text;

					Dispatcher.SynchronizationContext.Post(_ => {
							ExtractGame(downloadDir.PathJoin(downloadInfo.FileName), path, name);
						},
						null);
					break;
				}
				case { Cancelled: false, Error: not null }: {
					Log.Error("下载失败", args.Error);
					break;
				}
			}

			_download.Dispose();
			_download = null;
		};

		await _download.StartAsync(_cancellation.Token);
	}

	private void UpdateProgress(double p, string? n) {
		if ((ulong)Environment.CurrentManagedThreadId == Tools.MainThreadId) {
			_progressBar!.Value = p;
			_progressLabel!.Text = n;
		} else {
			Dispatcher.SynchronizationContext.Post(_ => {
					_progressBar!.Value = p;
					_progressLabel!.Text = n;
				},
				null);
		}
	}

	private async void ExtractGame(string filePath, string outputDir, string name) {
		TitleLabel!.Text = "提取中...";

		var assetDir = Path.Combine(outputDir, name, "assets");
		if (Directory.Exists(assetDir)) {
			foreach (var fileSystemEntry in Directory.GetFileSystemEntries(assetDir,
				"version-*.txt",
				SearchOption.TopDirectoryOnly)) {
				File.Delete(Path.Combine(assetDir, fileSystemEntry));
			}
		}

		_cancellation?.Dispose();
		_cancellation = new();
		var tmpPath = Path.Combine(filePath.GetBaseDir(), "vintagestory");
		Log.Info("开始提取游戏");
		try {
#if GODOT_WINDOWS
			await ExtractInnoSetupAsync(filePath,
				tmpPath,
				UpdateProgress,
				_cancellation.Token);
#elif GODOT_LINUXBSD
			await ExtractTarGzAsync(filePath,
				tmpPath,
				UpdateProgress,
				_cancellation.Token);
#endif
			_cancellation.Token.ThrowIfCancellationRequested();

			TitleLabel!.Text = "移动中...";
			CancelButton!.Disabled = true;
			var finalDestDir = Path.Combine(outputDir, name).NormalizePath();
			await DirMoveAsync(tmpPath,
				finalDestDir,
				true,
				UpdateProgress);
		} catch (OperationCanceledException) {
			Log.Info("取消提取游戏");
			return;
		} catch (Exception e) {
			Log.Error("提取游戏失败", e);
			_lastException = "提取游戏失败";
			ValidateInputs();
			return;
		}

		await Hide();
		InstallGame?.Invoke(Path.Combine(outputDir, name));
	}

	private void ButtonGroupOnPressed(BaseButton button) {
		if (string.IsNullOrEmpty(_releaseName?.Text)) {
			var item = button.GetOwner<InstalledGameItem>();
			_releaseName?.Text = item.GameVersion.ShortGameVersion;
		}

		ValidateInputs();
	}

	public async void UpdateDownloadList(string releaseUrl) {
		OkButton!.Disabled = true;
		_importButton!.Disabled = true;
		foreach (var child in _downloadListContainer!.GetChildren()) {
			child.QueueFree();
		}

		_loadingControl!.Show();
		_contentContainer!.Hide();
		_cancellation = new();

		await Task.Run(async () => { await GetReleases(releaseUrl); }, _cancellation.Token);

		if (_cancellation is null || _cancellation.IsCancellationRequested) {
			return;
		}

		ValidateInputs();
		_loadingControl.Hide();
		_contentContainer.Show();
	}

	private async Task GetReleases(string releaseUrl) {
		try {
			_lastException = null;
			await using var stream = await releaseUrl.GetStreamAsync(cancellationToken: _cancellation!.Token);
			_releases = await JsonSerializer.DeserializeAsync(stream,
				SourceGenerationContext.Default.DictionaryGameVersionGameRelease);

			if (_cancellation is not null && !_cancellation.IsCancellationRequested && _releases is not null) {
				var i = 1;
				var minVersion = new GameVersion("1.18.8");
				foreach (var group in _releases.Where(kv => GameVersion.ComparerVersion(kv.Key, minVersion) > -1)
					.GroupBy(r => r.Key.OverallVersion)) {
					var container = new FoldableContainer();
					container.Folded = true;
					container.Title = group.Key;
					container.Modulate = Colors.Transparent;

					var vbox = new VBoxContainer();
					vbox.AddThemeConstantOverride(StringNames.Separation, 10);

					foreach (var (gameVersion, gameRelease) in group) {
						Log.Trace($"{gameVersion} {gameRelease}");

						var item = _downloadItemScene!.Instantiate<InstalledGameItem>();
						item.GameVersion = gameVersion;
#if GODOT_WINDOWS
						item.GamePath = gameRelease.Windows.FileName;
#elif GODOT_LINUXBSD
						item.GamePath = gameRelease.Linux.FileName;
#endif
						item.SingleSelect = true;
						vbox.AddChild(item);
					}

					container.AddChild(vbox);
					using var tween = container.CreateTween();
					tween.TweenProperty(container, "modulate:a", 1f, 0.2f).SetDelay(i * 0.1);
					tween.Parallel().TweenProperty(container, "scale:x", 1f, 0.2f).From(0f).SetDelay(i * 0.1);
					i++;
					_downloadListContainer!.CallDeferred(Node.MethodName.AddChild, container);
				}
			}
		} catch (FlurlHttpException e) {
			Log.Error(e);
			_lastException = "网络请求失败，请重试";
		}
	}

	public static async Task ExtractTarGzAsync(
		string filePath,
		string tempDir,
		Action<double, string?>? extractProgress = null,
		CancellationToken cancellationToken = default) {
		extractProgress?.Invoke(0, "正在扫描文件总数...");

		const int bufferSize = 131072;
		const FileOptions fileOpts = FileOptions.Asynchronous | FileOptions.SequentialScan;

		var totalFiles = 0;

		await using (FileStream fsScan = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOpts))
		await using (GZipStream gzScan = new(fsScan, CompressionMode.Decompress, leaveOpen: false))
		await using (TarReader scanReader = new(gzScan, leaveOpen: false)) {
			while (await scanReader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is
				{ } scanEntry) {
				cancellationToken.ThrowIfCancellationRequested();
				if (scanEntry.EntryType == TarEntryType.RegularFile) {
					totalFiles++;
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (totalFiles == 0) {
			throw new InvalidOperationException("未找到可提取文件");
		}

		var extractedFiles = 0;
		var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		HashSet<string> createdDirs = new(pathComparer) { tempDir };
		var dirLookup = createdDirs.GetAlternateLookup<ReadOnlySpan<char>>();

		await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, fileOpts);
		await using GZipStream gz = new(fs, CompressionMode.Decompress, leaveOpen: false);
		await using TarReader reader = new(gz, leaveOpen: false);

		while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry) {
			cancellationToken.ThrowIfCancellationRequested();

			var nameSpan = entry.Name.AsSpan();
			var firstSlashIndex = nameSpan.IndexOf('/');

			var relativeNameSpan = firstSlashIndex >= 0
				? nameSpan[(firstSlashIndex + 1)..]
				: nameSpan;

			if (relativeNameSpan.IsWhiteSpace()) {
				continue;
			}

			var destPath = Path.Join(tempDir.AsSpan(), relativeNameSpan);

			switch (entry.EntryType) {
				case TarEntryType.RegularFile: {
					var lastSeparator = destPath.LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (lastSeparator > 0) {
						var dirSpan = destPath.AsSpan(0, lastSeparator);

						if (!dirLookup.Contains(dirSpan)) {
							var destDir = new string(dirSpan);
							createdDirs.Add(destDir);
							Directory.CreateDirectory(destDir);
						}
					}

					await entry.ExtractToFileAsync(destPath, overwrite: true, cancellationToken).ConfigureAwait(false);

					extractedFiles++;

					if (extractProgress != null) {
						var currentPercent = (int)((double)extractedFiles / totalFiles * 100);
						extractProgress.Invoke(currentPercent, $"{extractedFiles} / {totalFiles}");
					}

					break;
				}
				case TarEntryType.Directory: {
					if (createdDirs.Add(destPath)) {
						Directory.CreateDirectory(destPath);
					}

					break;
				}
			}
		}

		extractProgress?.Invoke(100, $"{totalFiles} / {totalFiles}");
	}

	public static async Task ExtractInnoSetupAsync(
		string filePath,
		string appDir,
		Action<double, string?>? extractProgress = null,
		CancellationToken cancellationToken = default) {
		using var tmp = DirAccess.CreateTemp("InnoExtract");
		var tmpRunPath = tmp.GetCurrentDir();
		const string innoExtractPath = "res://Misc/InnoUnp-2/innounp.exe";
		var innoExtract = tmpRunPath.PathJoin("innounp.exe");
		tmp.Copy(innoExtractPath, innoExtract.NormalizePath());

		extractProgress?.Invoke(0, "正在扫描文件总数...");
		var totalFiles = 0;

		using (Process countProcess = new()) {
			countProcess.StartInfo = new() {
				FileName = innoExtract,
				Arguments = $"-s -b -h \"{filePath}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true
			};

			countProcess.Start();

			try {
				var reader = countProcess.StandardOutput;
				while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line) {
					cancellationToken.ThrowIfCancellationRequested();
					if (line.AsSpan().TrimStart().StartsWith("{app}")) {
						totalFiles++;
					}
				}

				await countProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
			} finally {
				countProcess.Kill();
			}

			switch (countProcess.ExitCode) {
				case 0: break;
				case 1: throw new NotSupportedException("InnoSetup版本不受支持。");
				case 2: throw new InvalidDataException("安装文件已损坏或不兼容。");
				case 3: throw new("innounp发生内部或未知错误。");
				default:
					throw new($"innounp异常退出，状态码: {countProcess.ExitCode}");
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (totalFiles == 0) {
			throw new InvalidOperationException("未找到可提取文件");
		}

		var extractedFiles = 0;

		using Process extractProcess = new();
		extractProcess.StartInfo = new() {
			FileName = innoExtract,
			Arguments = $"-x -y -b -h -c{{app}} -d\"{appDir}\" \"{filePath}\"",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		extractProcess.Start();

		try {
			var errReader = extractProcess.StandardError;
			var errorReadTask = Task.Run(async () => {
					while (await errReader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } errLine) {
						if (!string.IsNullOrWhiteSpace(errLine)) {
							Log.Error(errLine);
						}
					}
				},
				cancellationToken);

			var outReader = extractProcess.StandardOutput;
			while (await outReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line) {
				cancellationToken.ThrowIfCancellationRequested();
				if (string.IsNullOrWhiteSpace(line) || !line.AsSpan().TrimStart().EndsWith("extracted")) {
					continue;
				}

				extractedFiles++;

				if (extractProgress != null) {
					var currentPercent = (int)((double)extractedFiles / totalFiles * 100);
					extractProgress.Invoke(currentPercent, $"{extractedFiles} / {totalFiles}");
				}
			}

			await Task.WhenAll(extractProcess.WaitForExitAsync(cancellationToken), errorReadTask).ConfigureAwait(false);
		} finally {
			extractProcess.Kill(true);
		}

		cancellationToken.ThrowIfCancellationRequested();

		switch (extractProcess.ExitCode) {
			case 0: break;
			case 1: throw new NotSupportedException("InnoSetup版本不受支持。");
			case 2: throw new InvalidDataException("安装文件已损坏或不兼容。");
			case 3: throw new("innounp发生内部或未知错误。");
			default:
				throw new($"innounp异常退出，状态码: {extractProcess.ExitCode}");
		}

		extractProgress?.Invoke(100, $"{totalFiles} / {totalFiles}");
	}


	public static async Task DirMoveAsync(
		string sourceDirName,
		string destDirName,
		bool overwrite = true,
		Action<double, string?>? progress = null) {
		if (!Directory.Exists(destDirName)) {
			Directory.CreateDirectory(destDirName);
		}

		var files = Directory.GetFiles(sourceDirName, "*", SearchOption.AllDirectories);
		var totalFiles = files.Length;

		if (totalFiles == 0) {
			if (Directory.Exists(sourceDirName)) {
				Directory.Delete(sourceDirName, true);
			}

			progress?.Invoke(100, "0 / 0");
			return;
		}

		progress?.Invoke(0, $"0 / {totalFiles}");

		var normalizedSource = Path.TrimEndingDirectorySeparator(sourceDirName);
		var sourcePrefixLength = normalizedSource.Length + 1;

		var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		HashSet<string> createdDirs = new(pathComparer) { destDirName };
		var dirLookup = createdDirs.GetAlternateLookup<ReadOnlySpan<char>>();

		await Task.Run(() => {
			for (var i = 0; i < totalFiles; i++) {
				var file = files[i];

				var relativeSpan = file.AsSpan(sourcePrefixLength);

				var destFile = Path.Join(destDirName.AsSpan(), relativeSpan);

				var lastSeparatorIndex = destFile.LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				if (lastSeparatorIndex > 0) {
					var destDirSpan = destFile.AsSpan(0, lastSeparatorIndex);

					if (!dirLookup.Contains(destDirSpan)) {
						var destDir = new string(destDirSpan);
						createdDirs.Add(destDir);
						Directory.CreateDirectory(destDir);
					}
				}

				File.Move(file, destFile, overwrite);

				if (progress != null) {
					var currentPercent = (int)((double)(i + 1) / totalFiles * 100);
					progress.Invoke(currentPercent, $"{i + 1} / {totalFiles}");
				}
			}

			if (Directory.Exists(sourceDirName)) {
				Directory.Delete(sourceDirName, true);
			}
		});
	}
}
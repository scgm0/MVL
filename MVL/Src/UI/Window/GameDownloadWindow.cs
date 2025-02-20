using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Downloader;
using Flurl.Http;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class GameDownloadWindow : BaseWindow {
	[Export]
	private ButtonGroup? _buttonGroup;

	[Export]
	private PackedScene? _foldableContainerScene;

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

	[Signal]
	public delegate void InstallGameEventHandler(string gamePath);

	private Dictionary<GameVersion, GameRelease>? _releases;

	private IDownload? _download;
	private DirAccess _downloadTmp = DirAccess.CreateTemp("MVL_Download");

	public override void _Ready() {
		base._Ready();
		NullExceptionHelper.NotNull(_buttonGroup,
			_foldableContainerScene,
			_releaseName,
			_releasePath,
			_folderButton,
			_tooltip,
			_fileDialog,
			_downloadItemScene,
			_contentContainer,
			_downloadListContainer,
			_loadingControl,
			_progressBar);

		_loadingControl.Show();
		_contentContainer.Hide();
		_progressBar.Hide();

		_releasePath.Text = Path.Combine(Paths.ReleaseFolder, _releaseName.Text).NormalizePath();
		_fileDialog.CurrentPath = Paths.ReleaseFolder;
		_fileDialog.CurrentDir = Paths.ReleaseFolder;

		_releaseName.TextChanged += ReleaseNameOnTextChanged;
		_folderButton.Pressed += _fileDialog.Show;
		_fileDialog.DirSelected += FileDialogOnDirSelected;
		_releasePath.TextChanged += ReleasePathOnTextChanged;
		_buttonGroup.Pressed += ButtonGroupOnPressed;
		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;

		ValidateInputs();
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
		if (string.IsNullOrEmpty(name)) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "请输入名称";
			_tooltip.Modulate = Colors.Red;
			return;
		}

		if (string.IsNullOrEmpty(path)) {
			OkButton!.Disabled = true;
			_tooltip!.Text = "请输入路径";
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

		_tooltip!.Text = Directory.Exists(Path.Combine(path, name)) ? $"{name}已存在，确定覆盖它吗？" : "将自动创建文件夹";
		_tooltip.Modulate = Directory.Exists(Path.Combine(path, name)) ? Colors.Yellow : Colors.White;
		OkButton!.Disabled = false;
	}

	override protected async void CancelButtonOnPressed() {
		_download?.Pause();
		await Hide();
		EmitSignalCancel();
		_download?.Stop();
	}

	public new async Task Hide() {
		await base.Hide();
		_downloadTmp.Dispose();
	}

	private async void OkButtonOnPressed() {
		_loadingControl!.Show();
		_contentContainer!.Hide();
		_progressBar!.Show();

		TitleLabel!.Text = "下载中...";
		OkButton!.Disabled = true;
		_progressBar.Value = 0;

		var item = _buttonGroup!.GetPressedButton().GetParent<InstalledGameItem>();
#if GODOT_WINDOWS
		var downloadInfo = _releases![item.GameVersion].Windows;
#elif GODOT_LINUXBSD
		var downloadInfo = _releases![item.GameVersion].Linux;
#endif
		var url = downloadInfo.Urls.Cdn ?? downloadInfo.Urls.Local;
		var downloadDir = _downloadTmp.GetCurrentDir();

		_download = DownloadBuilder.New()
			.WithUrl(url)
			.WithDirectory(downloadDir)
			.WithFileName(downloadInfo.FileName)
			.WithConfiguration(new() {
				ParallelDownload = true,
				ChunkCount = 8,
				ParallelCount = 8,
				RequestConfiguration = new() {
					Proxy = string.IsNullOrEmpty(Main.BaseConfig.ProxyUrl) ? null : new WebProxy(Main.BaseConfig.ProxyUrl)
				}
			})
			.Build();

		_download.DownloadProgressChanged += (_, args) => {
			CallDeferred(nameof(UpdateProgress), args.ProgressPercentage, args.BytesPerSecondSpeed);
		};

		_download.DownloadFileCompleted += (_, args) => {
			if (!args.Cancelled && args.Error is null) {
				var path = _releasePath!.Text;
				var name = _releaseName!.Text;

				CallDeferred(nameof(ExtractGame),
				[
					downloadDir.PathJoin(downloadInfo.FileName),
					path,
					name
				]);
			}

			_download.Dispose();
			_download = null;
		};
		await _download.StartAsync();
	}

	private async void ExtractGame(string filePath, string outputDir, string name) {
		TitleLabel!.Text = "提取中...";
		_progressBar!.Hide();
#if GODOT_WINDOWS
		await ExtractInnoSetupAsync(filePath, outputDir, name);
#elif GODOT_LINUXBSD
		await ExtractTarGzAsync(filePath, outputDir, name);
#endif
		await Hide();
		EmitSignalInstallGame(Path.Combine(outputDir, name));
	}

	private void UpdateProgress(int percentage, double speed) {
		_progressBar!.Value = percentage;
		var (fmtSpeed, unit) = FormatSpeed(speed);
		_progressBar.GetNode<Label>("Label").Text = $"{fmtSpeed:0.00} {unit}/s";
	}

	private void ButtonGroupOnPressed(BaseButton button) { ValidateInputs(); }

	public async void UpdateDownloadList(string releaseUrl) {
		OkButton!.Disabled = true;
		foreach (var child in _downloadListContainer!.GetChildren()) {
			child.QueueFree();
		}

		_loadingControl!.Show();
		_contentContainer!.Hide();

		await Task.Run(async () => {
			var text = await releaseUrl.GetStringAsync();
			_releases = JsonSerializer.Deserialize(text, SourceGenerationContext.Default.DictionaryGameVersionGameRelease);

			if (_releases is not null) {
				var i = 1;
				foreach (var group in _releases.GroupBy(r => r.Key.OverallVersion)) {
					var container = _foldableContainerScene!.Instantiate<FoldableContainer>();
					container.Title = group.Key;
					container.Modulate = Colors.Transparent;

					foreach (var (gameVersion, gameRelease) in group) {
						GD.PrintS(gameVersion, gameRelease);

						var item = _downloadItemScene!.Instantiate<InstalledGameItem>();
						item.GameVersion = gameVersion;
#if GODOT_WINDOWS
						item.GamePath = gameRelease.Windows.FileName;
#elif GODOT_LINUXBSD
						item.GamePath = gameRelease.Linux.FileName;
#endif
						item.SingleSelect = true;
						container.AddChild(item);
					}

					var tween = container.CreateTween();
					tween.TweenProperty(container, "modulate:a", 1f, 0.2f).SetDelay(i * 0.1);
					tween.Parallel().TweenProperty(container, "scale:x", 1f, 0.2f).From(0f).SetDelay(i * 0.1);
					i++;
					_downloadListContainer!.CallDeferred(Node.MethodName.AddChild, container);
				}
			}
		});

		_loadingControl.Hide();
		_contentContainer.Show();
	}

	static private (double speed, string unit) FormatSpeed(double bytesPerSecond) {
		string[] units = ["B", "KB", "MB", "GB"];
		var unitIndex = 0;
		var speed = bytesPerSecond;

		while (speed >= 1024 && unitIndex < units.Length - 1) {
			speed /= 1024;
			unitIndex++;
		}

		return (speed, units[unitIndex]);
	}

	public static async Task ExtractTarGzAsync(string filePath, string outputDir, string name) {
		await Task.Run(() => {
			using FileStream fs = new(filePath, FileMode.Open, System.IO.FileAccess.Read);
			using GZipStream gz = new(fs, CompressionMode.Decompress, leaveOpen: true);
			using var reader = new TarReader(gz, leaveOpen: true);

			string? subDir = null;
			while (reader.GetNextEntry() is { } entry) {
				GD.Print(entry.Name);
				subDir ??= entry.Name;
				var path = Path.Combine(outputDir, entry.Name).NormalizePath();
				path = path.Replace(Path.Combine(outputDir, subDir).NormalizePath(),
					Path.Combine(outputDir, name).NormalizePath());
				switch (entry.EntryType) {
					case TarEntryType.RegularFile: entry.ExtractToFile(path, true); break;
					case TarEntryType.Directory: Directory.CreateDirectory(path); break;
				}
			}
		});
	}

	public static async Task ExtractInnoSetupAsync(string filePath, string outputDir, string name) {
		await Task.Run(async () => {
			using var tmp = DirAccess.CreateTemp("InnoExtract");
			var tmpRunPath = tmp.GetCurrentDir();
			const string innoExtractPath = "res://Misc/InnoExtract/innoextract.exe";
			var innoExtract = tmpRunPath.PathJoin("innoextract.exe");
			tmp.Copy(innoExtractPath, innoExtract.NormalizePath());
			using var process = new Process();
			process.EnableRaisingEvents = true;
			process.StartInfo.FileName = $"{innoExtract}";
			process.StartInfo.Arguments =
				$"--include app -d \"{filePath.GetBaseDir().NormalizePath()}\" \"{filePath.NormalizePath()}\"";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.CreateNoWindow = true;
			process.OutputDataReceived += (_, args) => GD.Print(args.Data);
			process.ErrorDataReceived += (_, args) => GD.PrintErr(args.Data);
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			await process.WaitForExitAsync();
			DirMove(filePath.GetBaseDir().PathJoin("app").NormalizePath(), outputDir.PathJoin(name).NormalizePath());
		});
	}

	public static void DirMove(string sourceDirName, string destDirName, bool overwrite = true) {
		if (!Directory.Exists(destDirName)) {
			Directory.CreateDirectory(destDirName);
		}

		foreach (var file in Directory.EnumerateFiles(sourceDirName, "*", SearchOption.AllDirectories)) {
			var destFile = Path.Combine(destDirName, Path.GetRelativePath(sourceDirName, file));
			var destDir = Path.GetDirectoryName(destFile);
			var sameVolume = string.Equals(Path.GetPathRoot(file),
				Path.GetPathRoot(destFile),
				StringComparison.OrdinalIgnoreCase);

			if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) {
				Directory.CreateDirectory(destDir);
			}

			if (sameVolume) {
				File.Move(file, destFile, overwrite);
			} else {
				File.Copy(file, destFile, overwrite);
				File.Delete(file);
			}
		}

		Directory.Delete(sourceDirName, true);
	}
}
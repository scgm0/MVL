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
	private PackedScene? _downloadItemScene;

	[Export]
	private Control? _downloadListContainer;

	[Export]
	private Control? _loadingControl;

	[Export]
	private ProgressBar? _progressBar;

	[Signal]
	public delegate void ImportEventHandler(string gamePath);

	private Dictionary<GameVersion, GameRelease>? _releases;

	private IDownload? _download;
	private DirAccess _downloadTmp = DirAccess.CreateTemp("MVL_Download");

	public override void _Ready() {
		base._Ready();
		_buttonGroup.NotNull();
		_foldableContainerScene.NotNull();
		_downloadItemScene.NotNull();
		_downloadListContainer.NotNull();
		_loadingControl.NotNull();
		_progressBar.NotNull();
		_buttonGroup.Pressed += ButtonGroupOnPressed;
		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;
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
		TitleLabel!.Text = "下载中...";
		OkButton!.Disabled = true;
		_loadingControl!.Show();
		_downloadListContainer!.Hide();
		_progressBar!.Show();
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
					Proxy = new WebProxy(Main.BaseConfig.ProxyUrl)
				}
			})
			.Build();
		_download.DownloadProgressChanged += (_, args) => {
			CallDeferred(nameof(UpdateProgress), args.ProgressPercentage, args.BytesPerSecondSpeed);
		};
		_download.DownloadFileCompleted += (_, args) => {
			if (!args.Cancelled && args.Error is null) {
				CallDeferred(nameof(ExtractGame),
				[
					downloadDir.PathJoin(downloadInfo.FileName),
					OS.GetSystemDir(OS.SystemDir.Downloads),
					"复古物语"
				]);
			}

			_download.Dispose();
			_download = null;
		};
		await _download.StartAsync();
	}

	private async void ExtractGame(string filePath, string outputDir, string name) {
		TitleLabel!.Text = "解压中...";
		_progressBar!.Hide();
#if GODOT_WINDOWS
		await ExtractInnoSetupAsync(filePath, outputDir, name);
#elif GODOT_LINUXBSD
		await ExtractTarGzAsync(filePath, outputDir, name);
#endif
		await Hide();
		EmitSignalImport(Path.Combine(outputDir, name));
	}

	private void UpdateProgress(int percentage, double speed) {
		_progressBar!.Value = percentage;
		var (fmtSpeed, unit) = FormatSpeed(speed);
		_progressBar.GetNode<Label>("Label").Text = $"{fmtSpeed:0.00} {unit}/s";
	}

	private void ButtonGroupOnPressed(BaseButton button) { OkButton!.Disabled = false; }

	public async void UpdateDownloadList(string releaseUrl) {
		OkButton!.Disabled = true;
		foreach (var child in _downloadListContainer!.GetChildren()) {
			child.QueueFree();
		}

		_loadingControl!.Show();
		_downloadListContainer!.Hide();
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

				_downloadListContainer!.AddChild(container);
				var tween = container.CreateTween();
				tween.TweenProperty(container, "modulate:a", 1f, 0.2f).SetDelay(i * 0.1);
				tween.Parallel().TweenProperty(container, "scale:x", 1f, 0.2f).From(0f).SetDelay(i * 0.1);
				i++;
			}
		}

		_loadingControl.Hide();
		_downloadListContainer.Show();
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
			Directory.Move(filePath.GetBaseDir().PathJoin("app").NormalizePath(), outputDir.PathJoin(name).NormalizePath());
		});
	}
}
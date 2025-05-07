using Godot;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Downloader;
using MVL.UI.Window;
using MVL.Utils.Game;
using MVL.Utils.Help;
using Range = Godot.Range;

namespace MVL.UI.Item;

public partial class ApiModReleaseItem : PanelContainer {

	[Export]
	private Label? _modName;

	[Export]
	private Label? _version;

	[Export]
	private Button? _downloadButton;

	[Export]
	private CheckBox? _checkBox;

	[Export]
	private HFlowContainer? _tagsContainer;

	[Export]
	private ProgressBar? _progressBar;

	private bool _isChecked;

	public ModInfo? ModInfo { get; set; }
	public ApiModRelease? ApiModRelease { get; set; }
	public ApiModReleasesWindow? Window { get; set; }

	public bool IsChecked {
		get => _checkBox?.ButtonPressed ?? _isChecked;
		set {
			_isChecked = value;
			if (!IsNodeReady()) {
				return;
			}

			_downloadButton!.Hide();
			_checkBox!.Show();
			_checkBox!.ButtonPressed = _isChecked;
		}
	}

	public override void _Ready() {
		_modName.NotNull();
		_version.NotNull();
		_downloadButton.NotNull();
		_tagsContainer.NotNull();
		_progressBar.NotNull();
		ApiModRelease.NotNull();
		Window.NotNull();

		if (_isChecked) {
			_downloadButton!.Hide();
			_checkBox!.Show();
			_checkBox!.ButtonPressed = true;
		}

		_downloadButton.Pressed += DownloadButtonOnPressed;
		UpdateInfo();
	}

	private async void DownloadButtonOnPressed() {
		await Download();
	}

	public void UpdateInfo() {
		_modName!.Text = ModInfo!.Name;
		_version!.Text = ApiModRelease!.ModVersion;

		try {
			_downloadButton!.Disabled = SemVer.Parse(ApiModRelease.ModVersion) == SemVer.Parse(ModInfo.Version);
			_checkBox!.Disabled = _downloadButton!.Disabled;
		} catch (Exception e) {
			GD.PrintErr(e);
		}

		_downloadButton!.Modulate = Colors.DarkRed;
		_downloadButton.TooltipText = "兼容的游戏版本高于整合包游戏版本，不应下载";
		_checkBox!.Modulate = Colors.DarkRed;
		_checkBox.TooltipText = "兼容的游戏版本高于整合包游戏版本，不应下载";

		foreach (var child in _tagsContainer!.GetChildren()) {
			child.QueueFree();
		}

		foreach (var tag in ApiModRelease.Tags) {
			var label = new Label { Text = tag };
			label.Modulate = Colors.DarkRed;
			label.ThemeTypeVariation = "ModReleaseTag";
			_tagsContainer!.AddChild(label);

			if (GameVersion.ComparerVersion(ModInfo.ModpackConfig!.Version!.Value, new(tag)) == 0) {
				_downloadButton.Modulate = Colors.Green;
				_downloadButton.TooltipText = "兼容的游戏版本包含整合包游戏版本，可以使用";
				_checkBox!.Modulate = Colors.Green;
				_checkBox.TooltipText = "兼容的游戏版本包含整合包游戏版本，可以使用";
				label.Modulate = Colors.Green;
			} else if (GameVersion.ComparerVersion(ModInfo.ModpackConfig!.Version!.Value, new(tag)) > 0) {
				label.Modulate = Colors.DarkSeaGreen;
				if (_downloadButton.Modulate != Colors.DarkRed) {
					continue;
				}

				_downloadButton.Modulate = Colors.DarkSeaGreen;
				_downloadButton.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能过时";
				_checkBox!.Modulate = Colors.DarkSeaGreen;
				_checkBox.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能过时";
			}
		}
	}

	public async Task Download() {
		_downloadButton!.Disabled = true;
		_checkBox!.Disabled = _downloadButton!.Disabled;
		_progressBar!.Show();
		GD.Print($"下载 {ApiModRelease!.FileName}...");

		using var downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = downloadTmp.GetCurrentDir();
		var download = DownloadBuilder.New()
			.WithUrl(ApiModRelease!.MainFile)
			.WithDirectory(downloadDir)
			.WithFileName(ApiModRelease.FileName)
			.WithConfiguration(new() {
				ParallelDownload = true,
				ChunkCount = Main.BaseConfig.DownloadThreads,
				ParallelCount = Main.BaseConfig.DownloadThreads,
				RequestConfiguration = new() {
					Proxy = string.IsNullOrEmpty(Main.BaseConfig.ProxyAddress)
						? null
						: new WebProxy(Main.BaseConfig.ProxyAddress)
				}
			})
			.Build();
		download.DownloadProgressChanged += (_, args) => {
			_progressBar.CallDeferred(Range.MethodName.SetValue, args.ProgressPercentage);
		};
		await download.StartAsync();
		download.Dispose();

		if (!IsInstanceValid(this)) {
			return;
		}

		var path = Path.Combine(ModInfo!.ModPath.GetBaseDir(), ApiModRelease.FileName);
		File.Move(Path.Combine(downloadDir, ApiModRelease.FileName), path);
		File.Delete(ModInfo!.ModPath);

		var mod = ModInfo.FromZip(path);
		if (mod == null) {
			_progressBar.Hide();
			_downloadButton.Disabled = false;
			return;
		}

		mod.ModpackConfig = ModInfo!.ModpackConfig;
		mod.ModpackConfig!.Mods[mod.ModId] = mod;
		ModInfo = mod;
		Window!.UpdateApiModInfo(this);

		_progressBar.Hide();
		_downloadButton.Disabled = false;
	}
}
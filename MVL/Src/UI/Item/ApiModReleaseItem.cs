using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CSemVer;
using Downloader;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;
using HttpClient = System.Net.Http.HttpClient;
using Range = Godot.Range;

namespace MVL.UI.Item;

public partial class ApiModReleaseItem : PanelContainer {

	[Export]
	private RichTextLabel? _modName;

	[Export]
	private Label? _version;

	[Export]
	private Button? _dateLabel;

	[Export]
	private Button? _downloadCountLabel;

	[Export]
	private Button? _downloadButton;

	[Export]
	private CheckBox? _checkBox;

	[Export]
	private HBoxContainer? _tagsContainer;

	[Export]
	private ProgressBar? _progressBar;

	[Export]
	private Label? _progressLabel;

	private bool _isChecked;
	private IDownload? _download;

	public ModInfo? ModInfo { get; set; }
	public ModpackConfig? ModpackConfig { get; set; }
	public ApiModInfo? ApiModInfo { get; set; }
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
		_modName.MetaClicked += Tools.RichTextOpenUrl;
		UpdateInfo();
	}

	public void Disable() {
		_downloadButton!.Disabled = true;
		_checkBox!.Disabled = true;
	}

	public override void _ExitTree() { _download?.Stop(); }

	private async void DownloadButtonOnPressed() { await Download(); }

	public void UpdateInfo() {
		var url = ApiModInfo!.Value.UrlAlias is null
			? $"https://mods.vintagestory.at/show/mod/{ApiModInfo!.Value.AssetId}#tab-files"
			: $"https://mods.vintagestory.at/{ApiModInfo.Value.UrlAlias}#tab-files";
		_modName!.Text = $"[url={url}]{ApiModInfo!.Value.Name}[/url]";
		_modName.TooltipText = ApiModRelease!.Value.FileName;
		_version!.Text = _isChecked && ModInfo is not null
			? $"{ModInfo.Version} > {ApiModRelease!.Value.ModVersion}"
			: ApiModRelease!.Value.ModVersion;
		_dateLabel!.Text = ApiModRelease.Value.Created.ToString("yyyy-MM-dd");
		_downloadCountLabel!.Text = ApiModRelease.Value.Downloads.ToString();

		if (string.IsNullOrEmpty(ApiModRelease.Value.MainFile)) {
			_downloadButton!.Disabled = true;
			_checkBox!.Disabled = true;
			_downloadButton.TooltipText = "无可下载文件";
			_checkBox.TooltipText = "无可下载文件";
			_checkBox!.ButtonPressed = false;
			_downloadButton.Modulate = Colors.DarkRed;
			_checkBox.Modulate = Colors.DarkRed;

			var label = new Label { Text = "无可下载文件" };
			label.Modulate = Colors.DarkRed;
			label.ThemeTypeVariation = "ModReleaseTag";
			_tagsContainer!.AddChild(label);
			return;
		}

		try {
			if (ModInfo != null) {
				_downloadButton!.Disabled = SVersion.Parse(ApiModRelease.Value.ModVersion) == SVersion.Parse(ModInfo.Version);
			}

			_checkBox!.Disabled = _downloadButton!.Disabled;
		} catch (Exception e) {
			Log.Error(e);
		}

		_downloadButton!.Modulate = Colors.DarkRed;
		_downloadButton.TooltipText = "兼容的游戏版本高于整合包游戏版本，游戏不会加载";
		_checkBox!.Modulate = Colors.DarkRed;
		_checkBox.TooltipText = "兼容的游戏版本高于整合包游戏版本，游戏不会加载";

		foreach (var child in _tagsContainer!.GetChildren()) {
			child.QueueFree();
		}

		foreach (var tag in ApiModRelease.Value.Tags.Reverse()) {
			var label = new Label { Text = tag };
			label.Modulate = Colors.DarkRed;
			label.ThemeTypeVariation = "ModReleaseTag";
			_tagsContainer!.AddChild(label);

			if (GameVersion.ComparerVersion(ModpackConfig!.Version!.Value, new(tag)) == 0) {
				_downloadButton.Modulate = Colors.Green;
				_downloadButton.TooltipText = "兼容的游戏版本包含整合包游戏版本，能够安全使用";
				_checkBox!.Modulate = Colors.Green;
				_checkBox.TooltipText = "兼容的游戏版本包含整合包游戏版本，能够安全使用";
				label.Modulate = Colors.Green;
			} else if (GameVersion.ComparerVersion(ModpackConfig!.Version!.Value, new(tag)) > 0) {
				label.Modulate = Colors.DarkSeaGreen;
				if (_downloadButton.Modulate != Colors.DarkRed) {
					continue;
				}

				_downloadButton.Modulate = Colors.DarkSeaGreen;
				_downloadButton.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能已经过时";
				_checkBox!.Modulate = Colors.DarkSeaGreen;
				_checkBox.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能已经过时";
			}
		}
	}

	public async Task Download() {
		foreach (var child in GetParent().GetChildren()) {
			var item = child as ApiModReleaseItem;
			item?.Disable();
		}

		_downloadButton!.Disabled = true;
		_checkBox!.Disabled = _downloadButton!.Disabled;
		_downloadButton.Hide();
		_checkBox!.Hide();
		_progressLabel!.Show();
		_progressBar!.Show();
		Log.Info($"开始下载 {ApiModRelease!.Value.FileName}...");

		using var downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = downloadTmp.GetCurrentDir();
		_download = DownloadBuilder.New()
			.WithUrl(ApiModRelease.Value.MainFile)
			.WithDirectory(downloadDir)
			.WithFileName(ApiModRelease.Value.FileName)
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
			if (!IsInstanceValid(this)) {
				return;
			}

			_progressBar.CallDeferred(Range.MethodName.SetValue, args.ProgressPercentage);
			_progressLabel.CallDeferred(Label.MethodName.SetText, $"{args.ProgressPercentage:F1}%");
		};

		await _download.StartAsync();
		_download.Dispose();
		_download = null;

		if (!IsInstanceValid(this)) {
			return;
		}

		var path = Path.Combine(ModpackConfig!.Path!, "Mods", ApiModRelease.Value.FileName);
		File.Move(Path.Combine(downloadDir, ApiModRelease.Value.FileName), path);
		if (ModInfo != null) {
			File.Delete(ModInfo.ModPath);
		}

		var mod = ModInfo.FromZip(path);
		if (mod != null) {
			mod.ModpackConfig = ModpackConfig;
			mod.ModpackConfig!.Mods[mod.ModId] = mod;
			ModInfo = mod;

			Window!.UpdateApiModInfo(this);
		}

		_progressBar.Hide();
	}
}
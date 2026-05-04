using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSemVer;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

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

	public ModInfo? ModInfo { get; set; }
	public ModpackConfig? ModpackConfig { get; set; }
	public ApiModInfo? ApiModInfo { get; set; }
	public ApiModRelease? ApiModRelease { get; set; }
	public ApiModReleasesWindow? Window { get; set; }
	public CancellationTokenSource? CancellationTokenSource { get; set; }

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
		_dateLabel!.Text = ApiModRelease.Value.Created.LocalDateTime.ToString("yyyy-MM-dd");
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

			var gameVersion = ModpackConfig?.GameVersion;
			if (gameVersion != null) {
				if (GameVersion.ComparerVersion(gameVersion.Value, new(tag)) == 0) {
					_downloadButton.Modulate = Colors.Green;
					_downloadButton.TooltipText = "兼容的游戏版本包含整合包游戏版本，能够安全使用";
					_checkBox!.Modulate = Colors.Green;
					_checkBox.TooltipText = "兼容的游戏版本包含整合包游戏版本，能够安全使用";
					label.Modulate = Colors.Green;
				} else if (GameVersion.ComparerVersion(gameVersion.Value, new(tag)) > 0 ||
					GameVersion.ComparerVersion(gameVersion.Value, new(tag)) > 0) {
					label.Modulate = Colors.DarkSeaGreen;
					if (_downloadButton.Modulate != Colors.DarkRed) {
						continue;
					}

					_downloadButton.Modulate = Colors.DarkSeaGreen;
					_downloadButton.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能已经过时";
					_checkBox!.Modulate = Colors.DarkSeaGreen;
					_checkBox.TooltipText = "兼容的游戏版本低于整合包游戏版本，可能已经过时";
				}
			} else {
				label.Modulate = Colors.Yellow;
				_downloadButton.Modulate = Colors.Yellow;
				_downloadButton.TooltipText = "整合包的游戏版本无效，无法判断是否兼容";
				_checkBox!.Modulate = Colors.Yellow;
				_checkBox.TooltipText = "整合包的游戏版本无效，无法判断是否兼容";
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

		var path = Path.Combine(ModpackConfig!.Path!, "Mods", ApiModRelease!.Value.FileName);
		try {
			await ApiModRelease.Value.DownloadMainFileAsync(path,progress => {
				_progressBar.SetValue(progress.Percentage);
				_progressLabel.SetText($"{progress.Percentage:F1}%");
			}, CancellationTokenSource?.Token ?? CancellationToken.None);
		} catch (OperationCanceledException) {
			Log.Info($"取消下载: {ApiModRelease!.Value.FileName}");
			return;
		} catch (Exception e) {
			Log.Error($"下载失败: {ApiModRelease!.Value.FileName}", e);
			if (IsInstanceValid(this)) {
				_progressBar.Hide();
				_progressLabel.SetText("下载失败");
			}

			return;
		}

		if (ModInfo != null && File.Exists(ModInfo.ModPath) &&
			!path.Equals(ModInfo.ModPath, StringComparison.OrdinalIgnoreCase)) {
			File.Delete(ModInfo.ModPath);
			Log.Debug($"删除旧文件: {ModInfo.ModPath.GetFile()}");
		}

		var mod = await ModInfo.FromZip(path);
		if (mod != null) {
			mod.ModpackConfig = ModpackConfig;
			mod.ModpackConfig!.Mods[mod.ModId] = mod;
			ModInfo = mod;

			Window!.UpdateApiModInfo(this);
		}

		if (IsInstanceValid(this)) {
			_progressBar.Hide();
		}
	}
}
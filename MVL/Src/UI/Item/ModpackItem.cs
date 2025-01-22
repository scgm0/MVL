using System.IO;
using System.Linq;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class ModpackItem : PanelContainer {
	[Export]
	private PackedScene? _listGameScene;

	[Export]
	private Label? _modpackName;

	[Export]
	private LinkButton? _modpackPath;

	[Export]
	private Label? _modCount;

	[Export]
	private Button? _versionButton;

	[Export]
	private Button? _releaseButton;

	[Export]
	private Button? _playButton;

	public ModpackConfig? ModpackConfig { get; set; }

	public override void _Ready() {
		NullExceptionHelper.NotNull(
			_modpackName,
			_modpackPath,
			_modCount,
			_versionButton,
			_releaseButton,
			_playButton,
			ModpackConfig
		);
		_modpackName.Text = ModpackConfig.Name;
		_modpackPath.Text = ModpackConfig.Path;
		_modpackPath.Uri = ModpackConfig.Path;
		_versionButton.Text = ModpackConfig.Version?.ShortGameVersion ?? "选择版本";
		if (ModpackConfig.Version is not null) {
			_releaseButton.Visible = true;
			if (ModpackConfig.ReleasePath is not null) {
				var releaseInfo = Main.ReleaseInfos[ModpackConfig.ReleasePath];
				_releaseButton.Text = releaseInfo.Name;
				_releaseButton.TooltipText = releaseInfo.Path;
			}
		}

		var modsPath = ModpackConfig.Path!.PathJoin("Mods");
		if (Directory.Exists(modsPath)) {
			_modCount.Text = $"模组数量: {Directory.GetFileSystemEntries(modsPath).Length}";
		}

		_versionButton.Pressed += VersionButtonOnPressed;
		_playButton.Pressed += PlayButtonOnPressed;
	}

	private void VersionButtonOnPressed() {
		var list = Main.ReleaseInfos.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);
		var versionSelect = _listGameScene!.Instantiate<InstalledGamesImport>();
		versionSelect.SingleSelect = true;
		versionSelect.Import += paths => {
			if (paths.Length == 0) {
				return;
			}

			var path = paths[0];
			var info = Main.ReleaseInfos[path];
			ModpackConfig!.Version = info.Version;
			ModpackConfig!.ReleasePath = info.Path;
			_versionButton!.Text = info.Version.ShortGameVersion;
			_releaseButton!.Text = info.Name;
			_releaseButton.TooltipText = info.Path;
			ModpackConfig.Save(ModpackConfig);
		};
		Main.Instance?.AddChild(versionSelect);
		_ = versionSelect.ShowInstalledGames(list);
		versionSelect.Hidden += versionSelect.QueueFree;
	}

	private async void PlayButtonOnPressed() {
		if (Main.GameProcess is null) {
			var icon = (IconTexture2D)_playButton!.Icon;
			icon.IconName = "stop";
			_playButton.Modulate = Colors.Red;
			await Main.StartGame(ModpackConfig!.ReleasePath!, ModpackConfig.Path!);
			icon.IconName = "play";
			_playButton.Modulate = Colors.White;
		} else {
			Main.GameProcess.Kill();
		}
	}
}
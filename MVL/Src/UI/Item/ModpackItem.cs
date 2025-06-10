using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class ModpackItem : PanelContainer {
	[Export]
	private PackedScene? _listModScene;

	[Export]
	private PackedScene? _listGameScene;

	[Export]
	private Label? _modpackName;

	[Export]
	private LinkButton? _modpackPath;

	[Export]
	private Button? _modCount;

	[Export]
	private Button? _versionButton;

	[Export]
	private Button? _releaseButton;

	[Export]
	private Button? _playButton;

	public ModpackConfig? ModpackConfig { get; set; }

	public override async void _Ready() {
		NullExceptionHelper.NotNull(
			_listModScene,
			_listGameScene,
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

		if (ModpackConfig.Version is null) {
			_playButton.Disabled = true;
			_playButton.TooltipText = "请选择版本后再启动游戏";
		}

		if (Main.CurrentModpack == ModpackConfig) {
			var icon = (IconTexture2D)_playButton!.Icon;
			icon.IconName = "stop";
			_playButton.Modulate = Colors.Red;
			Main.GameExitEvent += MainOnGameExitEvent;
		}

		

		_versionButton.Pressed += VersionButtonOnPressed;
		_playButton.Pressed += PlayButtonOnPressed;

		await UpdateMods();
		_modCount.Pressed += ModCountOnPressed;
	}

	public async Task UpdateMods() {
		await Task.Run(ModpackConfig!.UpdateMods);
		_modCount!.Text = string.Format(Tr("模组数量: {0}"), ModpackConfig.Mods.Count);
	}

	private async void ModCountOnPressed() {
		var list = _listModScene!.Instantiate<ModpackModManagementWindow>();
		list.ModpackItem = this;
		list.Hidden += list.QueueFree;
		Main.Instance?.AddChild(list);
		await list.Show();
		list.ShowList();
	}

	private void MainOnGameExitEvent() {
		Dispatcher.SynchronizationContext.Post(_ => {
				var icon = (IconTexture2D)_playButton!.Icon;
				icon.IconName = "play";
				_playButton.Modulate = Colors.White;
			},
			null);
	}

	public override void _ExitTree() {
		if (Main.CurrentModpack != ModpackConfig) {
			return;
		}

		Main.GameExitEvent -= MainOnGameExitEvent;
	}

	private void VersionButtonOnPressed() {
		var list = Main.ReleaseInfos.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);
		var versionSelect = _listGameScene!.Instantiate<InstalledGamesImport>();
		versionSelect.Visible = false;
		versionSelect.SingleSelect = true;
		versionSelect.Import += paths => {
			if (paths.Length == 0) {
				return;
			}

			var path = paths[0];
			var info = Main.ReleaseInfos[path];
			ModpackConfig!.Version = info.Version;
			ModpackConfig.ReleasePath = info.Path;
			_versionButton!.Text = info.Version.ShortGameVersion;
			_releaseButton!.Visible = true;
			_releaseButton.Text = info.Name;
			_releaseButton.TooltipText = info.Path;
			_playButton!.Disabled = false;
			_playButton.TooltipText = "";
			ModpackConfig.Save(ModpackConfig);
		};
		Main.Instance?.AddChild(versionSelect);
		_ = versionSelect.ShowInstalledGames(list);
		versionSelect.Hidden += versionSelect.QueueFree;
	}

	private void PlayButtonOnPressed() {
		if (Main.CurrentModpack is null) {
			var icon = (IconTexture2D)_playButton!.Icon;
			icon.IconName = "stop";
			_playButton.Modulate = Colors.Red;
			Main.GameExitEvent += MainOnGameExitEvent;
			Main.Instance?.StartGame(ModpackConfig!);
		} else {
			Main.CurrentGameProcess?.Kill(true);
		}
	}

	private void CurrentGameProcessOnExited() {
		Main.GameExitEvent -= MainOnGameExitEvent;
		Dispatcher.SynchronizationContext.Post(_ => {
				var icon = (IconTexture2D)_playButton!.Icon;
				icon.IconName = "play";
				_playButton.Modulate = Colors.White;
			},
			null);
	}
}
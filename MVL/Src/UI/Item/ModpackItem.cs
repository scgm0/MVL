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
	private Label? _modpackAuthor;

	[Export]
	private Label? _modpackVersion;

	[Export]
	private TextureRect? _modpackIconTexture;

	[Export]
	private Label? _modpackSummary;

	[Export]
	private Button? _modCount;

	[Export]
	private Button? _versionButton;

	[Export]
	private Button? _settingButton;

	[Export]
	private Button? _playButton;

	public ModpackConfig? ModpackConfig { get; set; }

	public override async void _Ready() {
		_listModScene.NotNull();
		_listGameScene.NotNull();
		_modpackName.NotNull();
		_modpackVersion.NotNull();
		_modpackIconTexture.NotNull();
		_modpackSummary.NotNull();
		_modCount.NotNull();
		_versionButton.NotNull();
		_settingButton.NotNull();
		_playButton.NotNull();
		ModpackConfig.NotNull();

		_modpackName!.SetTranslationDomain(ModpackConfig!.Path!);
		_modpackSummary!.SetTranslationDomain(ModpackConfig.Path!);

		ModpackConfig.ModsUpdated += ModpackConfigOnModsUpdated;
		ModpackConfig.LocalizedTextUpdated += OnModpackConfigOnLocalizedTextUpdated;
		ModpackConfig.ModpackIconUpdated += ModpackConfigOnModpackIconUpdated;
		_versionButton.Pressed += VersionButtonOnPressed;
		_settingButton.Pressed += SettingButtonOnPressed;
		_playButton.Pressed += PlayButtonOnPressed;
		_modCount.Pressed += ModCountOnPressed;

		await UpdateUI();
		await ModpackConfig.UpdateModsAsync();
	}

	public override void _ExitTree() {
		base._ExitTree();
		ModpackConfig!.ModsUpdated -= ModpackConfigOnModsUpdated;
		ModpackConfig.LocalizedTextUpdated -= OnModpackConfigOnLocalizedTextUpdated;
		ModpackConfig.ModpackIconUpdated -= ModpackConfigOnModpackIconUpdated;

		if (Main.CurrentModpack != ModpackConfig) {
			return;
		}

		Main.GameExitEvent -= MainOnGameExitEvent;
	}

	private async void ModpackConfigOnModpackIconUpdated() {
		if (!IsInstanceValid(this)) {
			ModpackConfig!.ModpackIconUpdated -= ModpackConfigOnModpackIconUpdated;
			return;
		}

		_modpackIconTexture!.Texture = await ModpackConfig!.GetModpackIconAsync();
	}

	private void OnModpackConfigOnLocalizedTextUpdated() {
		if (!IsInstanceValid(this)) {
			ModpackConfig!.LocalizedTextUpdated -= OnModpackConfigOnLocalizedTextUpdated;
			return;
		}

		UpdateModpackText();
	}

	private async void SettingButtonOnPressed() {
		if (ModpackConfig == null) {
			return;
		}

		var window = Main.Instance!.OpenModpackSettingWindow(ModpackConfig);
		window.Hidden += window.QueueFree;
		window.Hidden += () => _ = UpdateUI();
		await window.Show();
	}

	public void UpdateModpackText() {
		_modpackName!.Text = ModpackConfig!.ModpackName.Value;
		_modpackName.TooltipText = ModpackConfig.ModpackName.Value;

		if (!string.IsNullOrEmpty(ModpackConfig.ModpackSummary.Value)) {
			_modpackSummary!.Text = ModpackConfig.ModpackSummary.Value;
		}

		_modpackName!.Notification((int)NotificationTranslationChanged);
		_modpackSummary!.Notification((int)NotificationTranslationChanged);
	}

	public async Task UpdateUI() {
		UpdateModpackText();

		if (ModpackConfig!.ModpackAuthors.Count == 0) {
			_modpackAuthor!.Text = $"{Tr("整合包作者:")} {Tr("未知")}";
		} else if (ModpackConfig.ModpackAuthors.Count == 1) {
			_modpackAuthor!.Text = $"{Tr("整合包作者:")} {ModpackConfig.ModpackAuthors[0]}";
		} else {
			_modpackAuthor!.Text = $"{Tr("整合包作者:")}{string.Join(", ", ModpackConfig.ModpackAuthors)}";
		}

		_modpackVersion!.Text = $"v{ModpackConfig.ModpackVersion}";
		_versionButton!.Text = ModpackConfig.GameVersion?.ShortGameVersion ?? "选择版本";

		if (ModpackConfig.GameVersion is null) {
			_playButton!.Disabled = true;
			_playButton.TooltipText = "请先为整合包选择游戏版本";
		}

		if (Main.CurrentModpack == ModpackConfig) {
			var icon = (IconTexture2D)_playButton!.Icon;
			icon.IconName = "stop";
			_playButton.Modulate = Colors.Red;
			Main.GameExitEvent += MainOnGameExitEvent;
		}

		_modpackIconTexture!.Texture = await ModpackConfig.GetModpackIconAsync();
	}

	public void ModpackConfigOnModsUpdated(ModpackConfig modpackConfig) {
		if (!IsInstanceValid(this)) {
			modpackConfig.ModsUpdated -= ModpackConfigOnModsUpdated;
			return;
		}

		_modCount!.Text = string.Format(Tr("模组数量: {0}"), modpackConfig.Mods.Count);
	}

	private async void ModCountOnPressed() {
		var list = _listModScene!.Instantiate<ModpackModManagementWindow>();
		list.ModpackConfig = ModpackConfig;
		list.Hidden += list.QueueFree;
		Main.Instance?.AddChild(list);
		await list.Show();
		await ModpackConfig!.UpdateModsAsync();
	}

	private void MainOnGameExitEvent() {
		Dispatcher.SynchronizationContext.Post(_ => {
				if (_playButton is null || !IsInstanceValid(_playButton)) {
					return;
				}

				var icon = (IconTexture2D)_playButton.Icon;
				icon.IconName = "play";
				_playButton.Modulate = Colors.White;
			},
			null);
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
			ModpackConfig!.GameVersion = info.Version;
			ModpackConfig.ReleasePath = info.Path;
			_versionButton!.Text = info.Version.ShortGameVersion;
			_playButton!.Disabled = false;
			_playButton.TooltipText = "";
			ModpackConfig.Save();
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
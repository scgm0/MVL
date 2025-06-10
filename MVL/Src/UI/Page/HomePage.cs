using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class HomePage : MenuPage {
	[Export]
	private PackedScene? _selectModpackItemScene;

	[Export]
	private ButtonGroup? _buttonGroup;

	[Export]
	private Button? _playButton;

	[Export]
	private Label? _modPackNameLabel;

	[Export]
	private Label? _gameVersionLabel;

	[Export]
	private Label? _releaseNameLabel;

	[Export]
	private Button? _selectModpackButton;

	[Export]
	private Button? _modpackInfoControl;

	[Export]
	private PanelContainer? _modpackInfoPanel;

	[Export]
	private ScrollContainer? _modpackInfoScrollContainer;

	[Export]
	private VBoxContainer? _modpackInfoVBoxContainer;

	[Export]
	private AnimationPlayer? _animationPlayer;

	public override async void _Ready() {
		_selectModpackItemScene.NotNull();
		_buttonGroup.NotNull();
		_playButton.NotNull();
		_modPackNameLabel.NotNull();
		_gameVersionLabel.NotNull();
		_releaseNameLabel.NotNull();
		_selectModpackButton.NotNull();
		_modpackInfoControl.NotNull();
		_modpackInfoPanel.NotNull();
		_modpackInfoScrollContainer.NotNull();
		_modpackInfoVBoxContainer.NotNull();
		_animationPlayer.NotNull();

		_modpackInfoControl.Pressed += HideSelectModpackPanel;
		_selectModpackButton.Pressed += ShowSelectModpackPanel;
		VisibilityChanged += async () => await UpdateInfo();
		_buttonGroup.Pressed += async button => {
			await ToSignal(button, BaseButton.SignalName.Pressed);
			await UpdateInfo();
		};
		_playButton.Pressed += StartButtonOnPressed;
		UI.Main.GameExitEvent += MainOnGameExitEvent;

		await UpdateInfo();
	}

	private void MainOnGameExitEvent() {
		Dispatcher.SynchronizationContext.Post(async void (_) => { await UpdateInfo(); }, null);
	}

	private async void StartButtonOnPressed() {
		if (UI.Main.CurrentModpack is null) {
			var modpackConfig = UI.Main.ModpackConfigs[UI.Main.BaseConfig.CurrentModpack];
			_ = UI.Main.Instance?.StartGame(modpackConfig);
		} else {
			UI.Main.CurrentGameProcess?.Kill(true);
		}

		await UpdateInfo();
	}

	private void ShowSelectModpackPanel() {
		_modpackInfoScrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);
		_modpackInfoScrollContainer!.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_modpackInfoControl!.Modulate = Colors.Transparent;
		_modpackInfoControl.Show();

		SelectModpackItem? selectedItem = null;
		var maxHeight = 0f;
		for (var index = 0; index < UI.Main.BaseConfig.Modpack.Count; index++) {
			var path = UI.Main.BaseConfig.Modpack[index];
			var modpackConfig = UI.Main.ModpackConfigs[path];
			var item = _selectModpackItemScene!.Instantiate<SelectModpackItem>();
			item.ModpackConfig = modpackConfig;
			_modpackInfoVBoxContainer!.AddChild(item);
			if (path == UI.Main.BaseConfig.CurrentModpack) {
				selectedItem = item;
			}

			if (index == 4) {
				maxHeight = _modpackInfoPanel!.GetCombinedMinimumSize().Y;
			}
		}

		_modpackInfoPanel!.Size = _modpackInfoPanel!.GetCombinedMinimumSize();
		_modpackInfoScrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;

		if (maxHeight > 0 && _modpackInfoPanel!.Size.Y > maxHeight) {
			_modpackInfoScrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
			_modpackInfoPanel!.Size = _modpackInfoPanel!.Size with { Y = maxHeight };
		}

		_modpackInfoPanel!.GlobalPosition =
			_modpackInfoPanel!.GlobalPosition with {
				X = _selectModpackButton!.GlobalPosition.X,
				Y = _selectModpackButton.GlobalPosition.Y - _modpackInfoPanel.Size.Y - 3
			};

		var offsetTop = _modpackInfoPanel.OffsetTop;
		_modpackInfoPanel.OffsetTop += _modpackInfoPanel.Size.Y;
		_modpackInfoControl!.Modulate = Colors.White;

		var animation = _animationPlayer!.GetAnimationLibrary("").GetAnimation(StringNames.Show);
		animation.TrackSetKeyValue(0, 0, _modpackInfoPanel.OffsetTop);
		animation.TrackSetKeyValue(0, 1, offsetTop);
		animation.Length = Mathf.Min(_modpackInfoVBoxContainer!.GetChildCount(), 5) * 0.05f;
		animation.TrackSetKeyTime(0, 1, animation.Length);
		_animationPlayer.Play(StringNames.Show);
		if (selectedItem != null) {
			_modpackInfoScrollContainer!.Call(StringNames.EnsureControlVisible, selectedItem);
		}
	}

	private async void HideSelectModpackPanel() {
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		_selectModpackButton!.ButtonPressed = false;
		_modpackInfoControl!.Hide();
		foreach (var child in _modpackInfoVBoxContainer!.GetChildren()) {
			child.QueueFree();
		}
	}

	private async Task UpdateInfo() {
		await Task.Run(() => {
			UI.Main.CheckReleaseInfo();
			UI.Main.CheckModpackConfig();
		});
		_playButton!.Modulate = Colors.White;
		_playButton!.Text = "启动游戏";
		_selectModpackButton!.Modulate = Colors.White;
		_selectModpackButton.Disabled = false;
		if (UI.Main.ModpackConfigs.TryGetValue(UI.Main.BaseConfig.CurrentModpack, out var modpackConfig)) {
			_playButton!.Disabled = false;
			_modPackNameLabel!.Text = modpackConfig.Name;
			_gameVersionLabel!.Text = modpackConfig.Version?.ShortGameVersion;
			if (modpackConfig.ReleasePath is not null) {
				var releaseInfo = UI.Main.ReleaseInfos[modpackConfig.ReleasePath];
				_releaseNameLabel!.Text = releaseInfo.Name;
			}

			_selectModpackButton.Text = "";
		} else {
			_playButton!.Disabled = true;
			_modPackNameLabel!.Text = "";
			_gameVersionLabel!.Text = "";
			_releaseNameLabel!.Text = "";
			_selectModpackButton.Text = "请先选择一个整合包";
			UI.Main.BaseConfig.CurrentModpack = string.Empty;
			BaseConfig.Save(UI.Main.BaseConfig);
			if (UI.Main.ModpackConfigs.Count == 0) {
				_selectModpackButton.Text = "请先在整合界面添加一个整合包";
				_selectModpackButton.Disabled = true;
				_selectModpackButton.Modulate = Colors.Red;
			}
		}

		if (UI.Main.CurrentModpack is null) return;
		_playButton!.Disabled = false;
		_playButton!.Text = "停止游戏";
		_playButton!.Modulate = Colors.Red;
	}
}
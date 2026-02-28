using System;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Other;

public partial class SelectModpackButton : Button {
	[Export]
	private PackedScene? _selectModpackItemScene;

	[Export]
	private HBoxContainer? _hBoxContainer;

	[Export]
	private Label? _name;

	[Export]
	private Label? _version;

	[Export]
	private Button? _bg;

	[Export]
	private PanelContainer? _panelContainer;

	[Export]
	private ScrollContainer? _scrollContainer;

	[Export]
	private VBoxContainer? _vboxContainer;

	private float _maxHeight;
	private readonly ButtonGroup _buttonGroup = new();
	private Tween? _tween;

	public ModpackConfig? ModpackConfig {
		get;
		set {
			if (field == value) {
				return;
			}

			field = value;
			UpdateInfo();
			SelectionChanged?.Invoke();
		}
	}

	public event Action? SelectionChanged;

	public override void _Ready() {
		_selectModpackItemScene.NotNull();
		_hBoxContainer.NotNull();
		_name.NotNull();
		_version.NotNull();
		_bg.NotNull();
		_panelContainer.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		VisibilityChanged += OnVisibilityChanged;
		Pressed += OnPressed;
		_bg.Pressed += BgOnPressed;
		_bg.VisibilityChanged += BgOnVisibilityChanged;
	}

	private void BgOnVisibilityChanged() {
		var rid = _bg!.GetCanvasItem();
		if (_bg.Visible) {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, true, _bg.GetGlobalRect());
		} else {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, false, new());
		}
	}

	private async void BgOnPressed() {
		_bg!.Disabled = true;
		_tween?.Kill();
		_tween?.Dispose();
		_tween = CreateTween();
		_tween.TweenProperty(_panelContainer, "size:y", 0, Mathf.Min(_vboxContainer!.GetChildCount(), 5) * 0.02f);
		await ToSignal(_tween, Tween.SignalName.Finished);

		_tween.Dispose();
		_tween = null;
		_bg!.Hide();

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		_bg.Disabled = false;
	}

	private async void OnPressed() {
		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);
		_scrollContainer!.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_bg!.Modulate = Colors.Transparent;
		_bg!.Show();

		var rect = GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X, Y = 0 };
		_panelContainer.GlobalPosition = rect.Position;

		var maxWidth = _vboxContainer!.GetCombinedMinimumSize().X;
		if (maxWidth > _panelContainer!.Size.X) {
			_panelContainer!.GlobalPosition = _panelContainer.GlobalPosition with {
				X = _panelContainer.GlobalPosition.X - (maxWidth - _panelContainer!.Size.X) / 2
			};
		} else {
			maxWidth = _panelContainer!.Size.X;
		}

		_maxHeight = 0f;
		SelectModpackItem? selectedItem = null;
		for (var index = 0; index < Main.BaseConfig.Modpack.Count; index++) {
			var path = Main.BaseConfig.Modpack[index];
			var modpackConfig = Main.ModpackConfigs[path];
			var item = _selectModpackItemScene!.Instantiate<SelectModpackItem>();
			item.ButtonGroup = _buttonGroup;
			item.ModpackConfig = modpackConfig;
			item.ButtonPressed += () => { ModpackConfig = modpackConfig; };

			if (path == ModpackConfig!.Path) {
				selectedItem = item;
				item.Selected = true;
			}

			_vboxContainer!.AddChild(item);

			if (index <= 4) {
				_maxHeight = _panelContainer!.GetCombinedMinimumSize().Y;
			}
		}

		_scrollContainer!.VerticalScrollMode = Main.BaseConfig.Modpack.Count > 5
			? ScrollContainer.ScrollMode.Auto
			: ScrollContainer.ScrollMode.ShowNever;
		_panelContainer.Size = _panelContainer.Size with { X = maxWidth, Y = 0 };
		_bg.Modulate = Colors.White;
		_tween = CreateTween();
		_tween.TweenProperty(_panelContainer, "size:y", _maxHeight, Mathf.Min(_vboxContainer!.GetChildCount(), 5) * 0.02f);

		await ToSignal(_tween, Tween.SignalName.Finished);
		if (selectedItem is not null) {
			_scrollContainer!.Call(StringNames.EnsureControlVisible, selectedItem);
		}
	}

	private void OnVisibilityChanged() {
		if (Visible) {
			UpdateInfo();
		}
	}

	private void UpdateInfo() {
		if (ModpackConfig is null) {
			if (Main.ModpackConfigs.TryGetValue(Main.BaseConfig.CurrentModpack, out var modpackConfig)) {
				ModpackConfig = modpackConfig;
			} else {
				Disabled = true;
				Text = "请先在整合界面添加一个整合包";
				_hBoxContainer!.Hide();
				return;
			}
		}

		_name!.Text = ModpackConfig.ModpackName;
		if (ModpackConfig.ReleaseInfo is { } releaseInfo) {
			_version!.Text = $"{releaseInfo.Name} ({releaseInfo.Version.ShortGameVersion})";
		} else {
			_version!.Text = $"{ModpackConfig.GameVersion?.ShortGameVersion}";
		}

		_hBoxContainer!.Show();
	}
}
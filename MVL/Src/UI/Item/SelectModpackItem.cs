using System;
using System.IO;
using Godot;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class SelectModpackItem : PanelContainer {
	[Export]
	private TextureRect? _modpackIconTexture;

	[Export]
	private Label? _modpackNameLabel;

	[Export]
	private Label? _releaseNameLabel;

	[Export]
	private Label? _releaseVersionLabel;

	[Export]
	private Button? _selectButton;

	public ButtonGroup? ButtonGroup { get; set; }
	public ModpackConfig? ModpackConfig { get; set; }
	public bool Selected { get; set; }
	public event Action? ButtonPressed;
	private bool _onlyDown;

	public override async void _Ready() {
		_modpackIconTexture.NotNull();
		_modpackNameLabel.NotNull();
		_releaseNameLabel.NotNull();
		_releaseVersionLabel.NotNull();
		_selectButton.NotNull();
		ModpackConfig.NotNull();

		_modpackNameLabel.Text = ModpackConfig.ModpackName;
		_modpackNameLabel.TooltipText = ModpackConfig.ModpackName;

		if (ModpackConfig.GameVersion is { } gameVersion) {
			_releaseVersionLabel.Text = gameVersion.ShortGameVersion;
			_releaseVersionLabel.TooltipText = gameVersion.ShortGameVersion;
		}

		if (ModpackConfig.ReleaseInfo is { } releaseInfo) {
			_releaseNameLabel.Text = releaseInfo.Name;
			_releaseNameLabel.TooltipText = releaseInfo.Name;
		}

		_selectButton.ButtonGroup = ButtonGroup;
		_selectButton.ButtonPressed = Selected;
		_selectButton.Pressed += SelectButtonOnPressed;

		using var modpackIcon = await ModpackConfig.GetModpackIconAsync();
		if (modpackIcon is not null) {
			_modpackIconTexture.Texture = modpackIcon;
		}
	}

	public override void _GuiInput(InputEvent @event) {
		switch (_onlyDown) {
			case false
				when @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }: {
				_onlyDown = true;
				break;
			}
			case true when @event is InputEventMouse: {
				_onlyDown = false;
				if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) {
					_selectButton?.ButtonPressed = true;
					_selectButton?.EmitSignal(BaseButton.SignalName.Pressed);
				}

				break;
			}
		}

		@event.Dispose();
	}

	private void SelectButtonOnPressed() { ButtonPressed?.Invoke(); }
}
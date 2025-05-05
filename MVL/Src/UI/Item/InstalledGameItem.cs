using System;
using Godot;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class InstalledGameItem : MarginContainer {
	[Export]
	private Label? _gameVersion;

	[Export]
	private Label? _gamePath;

	[Export]
	private CheckBox? _checkBox;

	[Export]
	private Color _stableColor;

	[Export]
	private Color _unStableColor;

	public GameVersion GameVersion {
		get;
		set {
			field = value;
			if (_gameVersion != null) {
				_gameVersion.Text = value.ShortGameVersion;
			}
		}
	}

	public string? GamePath {
		get;
		set {
			field = value;
			if (_gamePath != null) {
				_gamePath.Text = value;
			}
		}
	}

	public bool Check {
		get => _checkBox?.ButtonPressed ?? field;
		set {
			field = value;
			if (_checkBox != null) {
				_checkBox.ButtonPressed = value;
			}
		}
	}

	public bool SingleSelect { get; set; }

	public override void _Ready() {
		NullExceptionHelper.NotNull(_gameVersion, _gamePath, _checkBox, GameVersion, GamePath, Check);
		_gameVersion.Text = GameVersion.ShortGameVersion;
		switch (GameVersion.Branch) {
			case EnumGameBranch.Stable:
				_gameVersion.TooltipText = "稳定版";
				_gameVersion.AddThemeColorOverride("font_color", _stableColor);
				break;
			case EnumGameBranch.Unstable:
				_gameVersion.TooltipText = "测试版";
				_gameVersion.AddThemeColorOverride("font_color", _unStableColor);
				break;
			default: throw new ArgumentOutOfRangeException();
		}

		_gamePath.Text = GamePath;
		_gamePath.TooltipText = GamePath;
		_checkBox.ButtonPressed = Check;
		if (!SingleSelect) {
			_checkBox.ButtonGroup = null;
		}
	}
}
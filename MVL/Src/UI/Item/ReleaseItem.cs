using System;
using System.Diagnostics.CodeAnalysis;
using Godot;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class ReleaseItem : PanelContainer {
	[Export]
	private TextureRect? _icon;

	[Export]
	private Label? _name;

	[Export]
	private Label? _version;

	[Export]
	private Label? _label;

	[Export]
	private Button? _button;

	public ReleaseInfo? ReleaseInfo { get; set; }

	[field: AllowNull, MaybeNull]
	public Texture2D ReleaseIcon {
		get => field ?? _icon!.Texture;
		set {
			if (_icon == null) {
				return;
			}

			field = value;
			_icon.Texture = value;
		}
	}

	public override void _Ready() {
		_icon.NotNull();
		_name.NotNull();
		_version.NotNull();
		_label.NotNull();
		_button.NotNull();
		ReleaseInfo.NotNull();
		TooltipText = ReleaseInfo.Path;
		_name.Text = ReleaseInfo.Name;
		_version.Text = ReleaseInfo.Version.ShortGameVersion;
		ReleaseIcon = ReleaseIcon;
		switch (ReleaseInfo.Version.Branch) {
			case EnumGameBranch.Stable:
				_label!.Hide();
				break;
			case EnumGameBranch.Unstable:
				_label!.Show();
				break;
			default: throw new ArgumentOutOfRangeException();
		}

		_button.Pressed += ButtonOnPressed;
	}

	private void ButtonOnPressed() {
		OS.ShellOpen(ReleaseInfo!.Path);
	}
}
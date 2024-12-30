using System;
using System.Diagnostics.CodeAnalysis;
using Godot;
using 复古物语启动器.Utils.Extensions;
using 复古物语启动器.Utils.Game;
using 复古物语启动器.Utils.Help;

namespace 复古物语启动器.UI.Item;

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

	public string ReleaseName {
		get;
		set {
			if (_name == null) {
				return;
			}

			field = value;
			_name.Text = value;
		}
	} = "";

	public GameVersion ReleaseVersion {
		get;
		set {
			if (_version == null) {
				return;
			}

			field = value;
			_version.Text = value.ShortGameVersion;
			switch (value.Branch) {
				case EnumGameBranch.Stable:
					_label?.Hide();
					break;
				case EnumGameBranch.Unstable:
					_label?.Show();
					break;
				default: throw new ArgumentOutOfRangeException();
			}
		}
	}

	public string ReleasePath { get => TooltipText; set => TooltipText = value; }

	public override void _Ready() {
		_icon.NotNull();
		_name.NotNull();
		_version.NotNull();
		_label.NotNull();
		_button.NotNull();
		ReleaseIcon = ReleaseIcon;
		ReleaseName = ReleaseName;
		ReleaseVersion = ReleaseVersion;
		_button.Pressed += ButtonOnPressed;
	}

	private async void ButtonOnPressed() {
		GetWindow().Minimize();
		await Main.StartGame(ReleasePath, "/home/scgm/.config/VintagestoryData");
	}
}
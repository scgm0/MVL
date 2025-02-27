using Godot;
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

	public ModpackConfig? ModpackConfig { get; set; }

	public override void _Ready() {
		_modpackIconTexture.NotNull();
		_modpackNameLabel.NotNull();
		_releaseNameLabel.NotNull();
		_releaseVersionLabel.NotNull();
		_selectButton.NotNull();

		_modpackNameLabel.Text = ModpackConfig!.Name;
		if (ModpackConfig.ReleasePath is not null) {
			var releaseInfo = Main.ReleaseInfos[ModpackConfig.ReleasePath];
			_releaseNameLabel.Text = releaseInfo.Name;
			_releaseVersionLabel.Text = releaseInfo.Version.ShortGameVersion;
		}

		_selectButton.ButtonPressed = ModpackConfig.Path == Main.BaseConfig.CurrentModpack;
		_selectButton.Pressed += SelectButtonOnPressed;
	}

	private void SelectButtonOnPressed() {
		Main.BaseConfig.CurrentModpack = ModpackConfig!.Path!;
		BaseConfig.Save(Main.BaseConfig);
	}
}
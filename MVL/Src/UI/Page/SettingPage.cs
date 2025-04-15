using System.IO;
using Flurl.Http;
using Godot;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Extensions;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class SettingPage : MenuPage {
	[Export]
	private OptionButton? _displayLanguageOptionButton;

	[Export]
	private SpinBox? _displayScaleSpinbox;

	[Export]
	private LineEdit? _proxyAddressLineEdit;

	[Export]
	private SpinBox? _downloadThreadSpinbox;

	[Export]
	private LineEdit? _modpackFolderLineEdit;

	[Export]
	private Button? _modpackFolderButton;

	[Export]
	private LineEdit? _releaseFolderLineEdit;

	[Export]
	private Button? _releaseFolderButton;

	private string[] _languages = TranslationServer.GetLoadedLocales();

	public override void _Ready() {
		base._Ready();
		_displayLanguageOptionButton.NotNull();
		_displayScaleSpinbox.NotNull();
		_proxyAddressLineEdit.NotNull();
		_downloadThreadSpinbox.NotNull();
		_modpackFolderLineEdit.NotNull();
		_modpackFolderButton.NotNull();
		_releaseFolderLineEdit.NotNull();
		_releaseFolderButton.NotNull();

		_displayScaleSpinbox.Value = UI.Main.BaseConfig.DisplayScale * 100;
		_proxyAddressLineEdit.Text = UI.Main.BaseConfig.ProxyAddress;
		_downloadThreadSpinbox.Value = UI.Main.BaseConfig.DownloadThreads;
		_modpackFolderLineEdit.Text = UI.Main.BaseConfig.ModpackFolder;
		_releaseFolderLineEdit.Text = UI.Main.BaseConfig.ReleaseFolder;

		_displayLanguageOptionButton.ItemSelected += LanguageOptionButtonOnItemSelected;
		_displayScaleSpinbox.ValueChanged += DisplayScaleSpinboxOnValueChanged;
		_proxyAddressLineEdit.EditingToggled += ProxyAddressLineEditOnEditingToggled;
		_downloadThreadSpinbox.ValueChanged += DownloadThreadSpinboxOnValueChanged;
		_modpackFolderLineEdit.EditingToggled += ModpackFolderLineEditOnEditingToggled;
		_releaseFolderLineEdit.EditingToggled += ReleaseFolderLineEditOnEditingToggled;
		_modpackFolderButton.Pressed += ModpackFolderButtonOnPressed;
		_releaseFolderButton.Pressed += ReleaseFolderButtonOnPressed;

		var size = UI.Main.SceneTree.Root.Size;
		UI.Main.SceneTree.Root.Size = new(Mathf.CeilToInt(size.X * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(size.Y * UI.Main.BaseConfig.DisplayScale));
		TranslationServer.SetLocale(UI.Main.BaseConfig.DisplayLanguage);

		DisplayScaleSpinboxOnValueChanged(_displayScaleSpinbox.Value);
		UpdateLanguage();
		UI.Main.SceneTree.Root.Position -= (UI.Main.SceneTree.Root.Size - size) / 2;
	}

	private void ReleaseFolderButtonOnPressed() {
		var window = new FileDialog();
		window.Access = FileDialog.AccessEnum.Filesystem;
		window.CurrentPath = UI.Main.BaseConfig.ReleaseFolder;
		window.FileMode = FileDialog.FileModeEnum.OpenDir;
		window.UseNativeDialog = true;
		window.DirSelected += dir => {
			_releaseFolderLineEdit!.Text = dir;
			ReleaseFolderLineEditOnEditingToggled(false);
			window.QueueFree();
		};
		window.Canceled += window.QueueFree;
		AddChild(window);
		window.Show();
	}

	private void ModpackFolderButtonOnPressed() {
		var window = new FileDialog();
		window.Access = FileDialog.AccessEnum.Filesystem;
		window.CurrentPath = UI.Main.BaseConfig.ModpackFolder;
		window.FileMode = FileDialog.FileModeEnum.OpenDir;
		window.UseNativeDialog = true;
		window.DirSelected += dir => {
			_modpackFolderLineEdit!.Text = dir;
			ModpackFolderLineEditOnEditingToggled(false);
			window.QueueFree();
		};
		window.Canceled += window.QueueFree;
		AddChild(window);
		window.Show();
	}

	private void ReleaseFolderLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		var path = _releaseFolderLineEdit!.Text.NormalizePath();
		if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
			path = Paths.ReleaseFolder;
		}

		UI.Main.BaseConfig.ReleaseFolder = _releaseFolderLineEdit!.Text = path;
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void ModpackFolderLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		var path = _modpackFolderLineEdit!.Text.NormalizePath();
		if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
			path = Paths.ModpackFolder;
		}

		UI.Main.BaseConfig.ModpackFolder = _modpackFolderLineEdit!.Text = path;
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void DownloadThreadSpinboxOnValueChanged(double value) {
		UI.Main.BaseConfig.DownloadThreads = (int)_downloadThreadSpinbox!.Value;
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void ProxyAddressLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		UI.Main.BaseConfig.ProxyAddress = _proxyAddressLineEdit!.Text;
		FlurlHttp.Clients.Clear();
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void DisplayScaleSpinboxOnValueChanged(double value) {
		UI.Main.BaseConfig.DisplayScale = _displayScaleSpinbox!.Value / 100;
		UI.Main.SceneTree.Root.MinSize = new(Mathf.CeilToInt(1122 * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(618 * UI.Main.BaseConfig.DisplayScale));

		UI.Main.Instance?.WindowMaterial?.SetShaderParameter(StringNames.Radius, 10 * UI.Main.BaseConfig.DisplayScale);
		UI.Main.Instance?.RootOnSizeChanged();

		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void LanguageOptionButtonOnItemSelected(long index) {
		UI.Main.BaseConfig.DisplayLanguage = _languages[(int)index];
		TranslationServer.SetLocale(UI.Main.BaseConfig.DisplayLanguage);
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	public void UpdateLanguage() {
		_displayLanguageOptionButton!.Clear();
		var i = 0;
		foreach (var locale in _languages) {
			_displayLanguageOptionButton.AddItem(TranslationServer.GetLocaleName(locale), i);
			if (locale == UI.Main.BaseConfig.DisplayLanguage) {
				_displayLanguageOptionButton.Select(i);
			}

			i++;
		}
	}
}
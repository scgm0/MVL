using System.IO;
using Flurl.Http;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Extensions;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class SettingPage : MenuPage {
	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private PackedScene? _autoUpdaterWindowScene;

	[Export]
	private OptionButton? _displayLanguageOptionButton;

	[Export]
	private SpinBox? _displayScaleSpinbox;

	[Export]
	private OptionButton? _renderingDriverOptionButton;

	[Export]
	private CheckButton? _menuExpandCheckButton;

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

	[Export]
	private Button? _getLatestReleaseButton;

	private string[] _languages = TranslationServer.GetLoadedLocales();

	private ConfigFile _configFile = new();

	private readonly (string InternalName, string DisplayName)[] _renderingDrivers = [
#if GODOT_WINDOWS
		("d3d12", "Direct3D 12"),
#endif
		("vulkan", "Vulkan"),
		("opengl3", "OpenGL 3"),
#if GODOT_WINDOWS
		("opengl3_angle", "OpenGL ANGLE"),
#elif GODOT_LINUXBSD
		("opengl3_es", "OpenGL ES"),
#endif
	];

#if GODOT_WINDOWS
	private string _renderingDriverKey = "rendering_device/driver.windows";
#elif GODOT_LINUXBSD
	private string _renderingDriverKey = "rendering_device/driver.linuxbsd";
#endif

	public override void _Ready() {
		base._Ready();
		_displayLanguageOptionButton.NotNull();
		_displayScaleSpinbox.NotNull();
		_renderingDriverOptionButton.NotNull();
		_menuExpandCheckButton.NotNull();
		_proxyAddressLineEdit.NotNull();
		_downloadThreadSpinbox.NotNull();
		_modpackFolderLineEdit.NotNull();
		_modpackFolderButton.NotNull();
		_releaseFolderLineEdit.NotNull();
		_releaseFolderButton.NotNull();
		_getLatestReleaseButton.NotNull();

		_displayScaleSpinbox.Value = UI.Main.BaseConfig.DisplayScale * 100;
		_menuExpandCheckButton.ButtonPressed = UI.Main.BaseConfig.MenuExpand;
		_proxyAddressLineEdit.Text = UI.Main.BaseConfig.ProxyAddress;
		_downloadThreadSpinbox.Value = UI.Main.BaseConfig.DownloadThreads;
		_modpackFolderLineEdit.Text = UI.Main.BaseConfig.ModpackFolder;
		_releaseFolderLineEdit.Text = UI.Main.BaseConfig.ReleaseFolder;

		_displayLanguageOptionButton.ItemSelected += LanguageOptionButtonOnItemSelected;
		_displayScaleSpinbox.ValueChanged += DisplayScaleSpinboxOnValueChanged;
		_renderingDriverOptionButton.ItemSelected += RenderingDriverOptionButtonOnItemSelected;
		_menuExpandCheckButton.Toggled += MenuExpandCheckButtonOnToggled;
		_proxyAddressLineEdit.EditingToggled += ProxyAddressLineEditOnEditingToggled;
		_downloadThreadSpinbox.ValueChanged += DownloadThreadSpinboxOnValueChanged;
		_modpackFolderLineEdit.EditingToggled += ModpackFolderLineEditOnEditingToggled;
		_releaseFolderLineEdit.EditingToggled += ReleaseFolderLineEditOnEditingToggled;
		_modpackFolderButton.Pressed += ModpackFolderButtonOnPressed;
		_releaseFolderButton.Pressed += ReleaseFolderButtonOnPressed;
		_getLatestReleaseButton.Pressed += GetLatestReleaseButtonOnPressed;

		var size = UI.Main.SceneTree.Root.Size = new(1162, 658);
		UI.Main.SceneTree.Root.Size = new(Mathf.CeilToInt(size.X * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(size.Y * UI.Main.BaseConfig.DisplayScale));

		DisplayScaleSpinboxOnValueChanged(_displayScaleSpinbox.Value);
		UpdateLanguage();
		UpdateRenderingDriver();
		UI.Main.SceneTree.Root.MoveToCenter();

		_configFile.SetValue("rendering",
			_renderingDriverKey,
			RenderingServer.GetCurrentRenderingDriverName());
		_configFile.Save(Paths.OverrideConfigPath);
	}

	private async void GetLatestReleaseButtonOnPressed() {
		var autoUpdaterWindow = _autoUpdaterWindowScene!.Instantiate<AutoUpdaterWindow>();
		UI.Main.Instance!.AddChild(autoUpdaterWindow);
		await autoUpdaterWindow.GetLatestRelease();
	}

	private void MenuExpandCheckButtonOnToggled(bool toggledOn) {
		UI.Main.BaseConfig.MenuExpand = toggledOn;
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void RenderingDriverOptionButtonOnItemSelected(long index) {
		if (index < 0 || index >= _renderingDrivers.Length) {
			return;
		}

		var currentDriver =
			_configFile.GetValue("rendering", _renderingDriverKey, RenderingServer.GetCurrentRenderingDriverName());
		var (driverKey, driverDisplayName) = _renderingDrivers[index];

		_configFile.SetValue("rendering", _renderingDriverKey, driverKey);
		_configFile.Save(Paths.OverrideConfigPath);

		Log.Debug($"已更改渲染驱动: {currentDriver} -> {driverKey}");

		var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
		confirmationWindow.Message = string.Format(Tr("已将渲染驱动更改为 [color=#3c7fe1]{0}[/color]\n需要重启才能生效，是否立即重启？"), driverDisplayName);

		confirmationWindow.Confirm += () => {
			OS.SetRestartOnExit(true);
			UI.Main.SceneTree.Quit();
		};
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		UI.Main.Instance!.AddChild(confirmationWindow);
		_ = confirmationWindow.Show();
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
		Log.Debug($"更改语言为: {UI.Main.BaseConfig.DisplayLanguage}");
	}

	public void UpdateLanguage() {
		var language = TranslationServer.HasTranslationForLocale(UI.Main.BaseConfig.DisplayLanguage, false)
			? TranslationServer.FindTranslations(UI.Main.BaseConfig.DisplayLanguage, false)[0].Locale
			: TranslationServer.GetLocale();
		TranslationServer.SetLocale(language);

		_displayLanguageOptionButton!.Clear();
		var i = 0;
		foreach (var locale in _languages) {
			_displayLanguageOptionButton.AddItem(TranslationServer.GetLocaleName(locale), i);
			if (locale == language) {
				_displayLanguageOptionButton.Select(i);
			}

			i++;
		}

		if (language != UI.Main.BaseConfig.DisplayLanguage) {
			LanguageOptionButtonOnItemSelected(_displayLanguageOptionButton.Selected);
		}
	}

	public void UpdateRenderingDriver() {
		_renderingDriverOptionButton!.Clear();

		var currentDriver = RenderingServer.GetCurrentRenderingDriverName();

		for (var i = 0; i < _renderingDrivers.Length; i++) {
			var (internalName, displayName) = _renderingDrivers[i];

			_renderingDriverOptionButton.AddItem(displayName, i);

			_renderingDriverOptionButton.SetItemMetadata(i, internalName);

			if (internalName == currentDriver) {
				_renderingDriverOptionButton.Select(i);
			}
		}
	}
}
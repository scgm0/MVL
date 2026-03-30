using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Flurl.Http;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.GitHub;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class SettingPage : MenuPage {
	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private PackedScene? _launcherDownloadWindowScene;

	[Export]
	private Translation? _zhTranslation;

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
	private CheckButton? _useThirdPartyGameDownloadCheckButton;

	[Export]
	private LineEdit? _thirdPartyGameLinkLineEdit;

	[Export]
	private Button? _thirdPartyGameLinkCheckButton;

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
	private RichTextLabel? _localTranslationFolderLabel;

	[Export]
	private Button? _localTranslationReloadButton;

	[Export]
	private OptionButton? _gitHubProxyOptionButton;

	[Export]
	private CheckButton? _autoCheckVersionCheckButton;

	[Export]
	private LinkButton? _lastVersionLinkButton;

	private string[] _languages = [];

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

	private readonly List<Translation> _localTranslations = [];

	private ApiRelease? _lastVersion;

	public override void _Ready() {
		base._Ready();
		_displayLanguageOptionButton.NotNull();
		_displayScaleSpinbox.NotNull();
		_renderingDriverOptionButton.NotNull();
		_menuExpandCheckButton.NotNull();
		_proxyAddressLineEdit.NotNull();
		_useThirdPartyGameDownloadCheckButton.NotNull();
		_thirdPartyGameLinkLineEdit.NotNull();
		_thirdPartyGameLinkCheckButton.NotNull();
		_downloadThreadSpinbox.NotNull();
		_modpackFolderLineEdit.NotNull();
		_modpackFolderButton.NotNull();
		_releaseFolderLineEdit.NotNull();
		_releaseFolderButton.NotNull();
		_localTranslationFolderLabel.NotNull();
		_localTranslationReloadButton.NotNull();
		_gitHubProxyOptionButton.NotNull();
		_autoCheckVersionCheckButton.NotNull();
		_lastVersionLinkButton.NotNull();

		_displayScaleSpinbox.Value = UI.Main.BaseConfig.DisplayScale * 100;
		_menuExpandCheckButton.ButtonPressed = UI.Main.BaseConfig.MenuExpand;
		_proxyAddressLineEdit.Text = UI.Main.BaseConfig.ProxyAddress;
		_useThirdPartyGameDownloadCheckButton.ButtonPressed = UI.Main.BaseConfig.UseThirdPartyGameDownload;
		_thirdPartyGameLinkLineEdit.Text = UI.Main.BaseConfig.ThirdPartyGameLink;
		_thirdPartyGameLinkLineEdit.Editable = _useThirdPartyGameDownloadCheckButton.ButtonPressed;
		UpdateThirdPartyGameLinkCheckButtonState();
		_downloadThreadSpinbox.Value = UI.Main.BaseConfig.DownloadThreads;
		_modpackFolderLineEdit.Text = UI.Main.BaseConfig.ModpackFolder;
		_releaseFolderLineEdit.Text = UI.Main.BaseConfig.ReleaseFolder;
		_localTranslationFolderLabel.Text = $"[url]{Paths.TranslationFolder}[/url]";

		_displayLanguageOptionButton.ItemSelected += LanguageOptionButtonOnItemSelected;
		_displayScaleSpinbox.ValueChanged += DisplayScaleSpinboxOnValueChanged;
		_renderingDriverOptionButton.ItemSelected += RenderingDriverOptionButtonOnItemSelected;
		_menuExpandCheckButton.Toggled += MenuExpandCheckButtonOnToggled;
		_proxyAddressLineEdit.EditingToggled += ProxyAddressLineEditOnEditingToggled;
		_useThirdPartyGameDownloadCheckButton.Toggled += UseThirdPartyGameDownloadCheckButtonOnToggled;
		_thirdPartyGameLinkLineEdit.EditingToggled += ThirdPartyGameLinkLineEditOnEditingToggled;
		_thirdPartyGameLinkLineEdit.TextChanged += ThirdPartyGameLinkLineEditOnTextChanged;
		_thirdPartyGameLinkCheckButton.Pressed += ThirdPartyGameLinkCheckButtonOnPressed;
		_downloadThreadSpinbox.ValueChanged += DownloadThreadSpinboxOnValueChanged;
		_modpackFolderLineEdit.EditingToggled += ModpackFolderLineEditOnEditingToggled;
		_releaseFolderLineEdit.EditingToggled += ReleaseFolderLineEditOnEditingToggled;
		_modpackFolderButton.Pressed += ModpackFolderButtonOnPressed;
		_releaseFolderButton.Pressed += ReleaseFolderButtonOnPressed;
		_localTranslationFolderLabel.MetaClicked += Tools.RichTextOpenUrl;
		_localTranslationReloadButton.Pressed += UpdateLanguage;
		_gitHubProxyOptionButton.ItemSelected += GitHubProxyOptionButtonOnItemSelected;
		_autoCheckVersionCheckButton.Toggled += AutoCheckVersionCheckButtonOnToggled;
		_lastVersionLinkButton.Pressed += LastVersionLinkButtonOnPressed;

		var size = Tools.SceneTree.Root.Size = new(1162, 658);
		Tools.SceneTree.Root.Size = new(Mathf.CeilToInt(size.X * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(size.Y * UI.Main.BaseConfig.DisplayScale));

		DisplayScaleSpinboxOnValueChanged(_displayScaleSpinbox.Value);
		LoadDefaultZHTranslation();
		UpdateLanguage();
		UpdateRenderingDriver();
		UpdateLastVersion();
		Tools.SceneTree.Root.MoveToCenter();

		_configFile.SetValue("rendering",
			_renderingDriverKey,
			RenderingServer.GetCurrentRenderingDriverName());
		_configFile.Save(Paths.OverrideConfigPath);
	}

	public override void _ExitTree() {
		TranslationServer.RemoveTranslation(_zhTranslation);
		_zhTranslation?.Dispose();
		_zhTranslation = null;
	}

	private void MenuExpandCheckButtonOnToggled(bool toggledOn) {
		UI.Main.BaseConfig.MenuExpand = toggledOn;
		UI.Main.BaseConfig.Save();
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
		confirmationWindow.Message =
			string.Format(Tr("已将渲染驱动更改为 [color=#3c7fe1]{0}[/color]\n需要重启才能生效，是否立即重启？"), driverDisplayName);

		confirmationWindow.Confirm += () => {
			OS.SetRestartOnExit(true);
			Tools.SceneTree.Quit();
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
		UI.Main.BaseConfig.Save();
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
		UI.Main.BaseConfig.Save();
	}

	private void DownloadThreadSpinboxOnValueChanged(double value) {
		UI.Main.BaseConfig.DownloadThreads = (int)_downloadThreadSpinbox!.Value;
		UI.Main.BaseConfig.Save();
	}

	private void ProxyAddressLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		UI.Main.BaseConfig.ProxyAddress = _proxyAddressLineEdit!.Text;
		FlurlHttp.Clients.Clear();
		UI.Main.BaseConfig.Save();
	}

	private void UseThirdPartyGameDownloadCheckButtonOnToggled(bool toggledOn) {
		UI.Main.BaseConfig.UseThirdPartyGameDownload = toggledOn;
		_thirdPartyGameLinkLineEdit!.Editable = toggledOn;
		UpdateThirdPartyGameLinkCheckButtonState();
		UI.Main.BaseConfig.Save();
	}

	private void ThirdPartyGameLinkLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		UI.Main.BaseConfig.ThirdPartyGameLink = _thirdPartyGameLinkLineEdit!.Text.Trim();
		UI.Main.BaseConfig.Save();
	}

	private void ThirdPartyGameLinkLineEditOnTextChanged(string _) { UpdateThirdPartyGameLinkCheckButtonState(); }

	private void UpdateThirdPartyGameLinkCheckButtonState() {
		_thirdPartyGameLinkCheckButton!.Disabled = !_useThirdPartyGameDownloadCheckButton!.ButtonPressed ||
			string.IsNullOrWhiteSpace(_thirdPartyGameLinkLineEdit!.Text);
	}

	private void ShowThirdPartyGameLinkCheckResult(string message) {
		var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
		confirmationWindow.Message = message;
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		confirmationWindow.Confirm += async () => { await confirmationWindow.Hide(); };
		UI.Main.Instance?.AddChild(confirmationWindow);
		_ = confirmationWindow.Show();
	}

	private async void ThirdPartyGameLinkCheckButtonOnPressed() {
		var url = _thirdPartyGameLinkLineEdit!.Text.Trim();

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
			uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) {
			ShowThirdPartyGameLinkCheckResult("链接格式错误，请输入有效的[b]http[/b]或[b]https[/b]链接");
			return;
		}

		_thirdPartyGameLinkCheckButton!.Text = "检测中...";
		_thirdPartyGameLinkCheckButton.Disabled = true;

		try {
			Log.Debug($"检测第三方游戏链接: {url}");
			await using var stream = await url.WithTimeout(10).GetStreamAsync();
			var releases = await JsonSerializer.DeserializeAsync(
				stream,
				SourceGenerationContext.Default.DictionaryGameVersionGameRelease
			);
			if (releases == null || releases.Count == 0) {
				Log.Warn($"检测失败，返回内容为空: {url}");
				ShowThirdPartyGameLinkCheckResult("返回内容解析成功，但版本列表为空");
				return;
			}

			ShowThirdPartyGameLinkCheckResult("检测成功，链接可访问且返回了有效内容");
		} catch (TimeoutException e) {
			Log.Error($"链接超时: {url}", e);
			ShowThirdPartyGameLinkCheckResult("链接超时，请检查网络连接或稍后重试");
		} catch (Exception e) {
			Log.Error($"解析失败: {url}", e);
			ShowThirdPartyGameLinkCheckResult("返回内容解析失败，接口格式不符合");
		} finally {
			_thirdPartyGameLinkCheckButton.Text = "检测";
			UpdateThirdPartyGameLinkCheckButtonState();
		}
	}

	private void DisplayScaleSpinboxOnValueChanged(double value) {
		UI.Main.BaseConfig.DisplayScale = _displayScaleSpinbox!.Value / 100;
		Tools.SceneTree.Root.MinSize = new(Mathf.CeilToInt(1122 * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(618 * UI.Main.BaseConfig.DisplayScale));

		UI.Main.Instance?.WindowMaterial?.SetShaderParameter(StringNames.OuterOutlineThickness,
			UI.Main.BaseConfig.DisplayScale);
		UI.Main.Instance?.RootOnSizeChanged();

		UI.Main.BaseConfig.Save();
	}

	private void LanguageOptionButtonOnItemSelected(long index) {
		UI.Main.BaseConfig.DisplayLanguage = _languages[(int)index];
		TranslationServer.SetLocale(UI.Main.BaseConfig.DisplayLanguage);
		UI.Main.BaseConfig.Save();
		Log.Debug($"更改语言为: {UI.Main.BaseConfig.DisplayLanguage}");
	}

	public void UpdateLanguage() {
		DirAccess.CopyAbsolute("res://Assets/Translation/MVL/mvl.pot", Path.Join(Paths.TranslationFolder, "mvl.pot"));

		foreach (var localTranslation in _localTranslations.ToList()) {
			TranslationServer.RemoveTranslation(localTranslation);
			_localTranslations.Remove(localTranslation);
			localTranslation.Dispose();
		}

		foreach (var poFile in Directory.GetFiles(Paths.TranslationFolder, "*.po")) {
			try {
				if (!ResourceLoader.Exists(poFile)) {
					continue;
				}

				var translation = ResourceLoader.Load<Translation?>(poFile, null, ResourceLoader.CacheMode.Ignore);
				if (translation is null) {
					continue;
				}

				TranslationServer.AddTranslation(translation);
				_localTranslations.Add(translation);
				Log.Debug($"加载本地翻译: {poFile.GetFile()}({translation.Locale})");
			} catch (Exception e) {
				Log.Error($"加载本地翻译失败: {poFile.GetFile()}", e);
			}
		}

		var language = TranslationServer.HasTranslationForLocale(UI.Main.BaseConfig.DisplayLanguage, false)
			? TranslationServer.FindTranslations(UI.Main.BaseConfig.DisplayLanguage, false)
				.OrderByDescending(t => TranslationServer.CompareLocales(t.Locale, UI.Main.BaseConfig.DisplayLanguage)).First()
				.Locale
			: TranslationServer.GetLocale();
		TranslationServer.SetLocale(language);

		_displayLanguageOptionButton!.Clear();

		_languages = TranslationServer.GetLoadedLocales();
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
		} else {
			Tools.SceneTree.Notification((int)MainLoop.NotificationTranslationChanged);
			Log.Info("已重载翻译");
		}
	}

	public void LoadDefaultZHTranslation() { TranslationServer.AddTranslation(_zhTranslation); }

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

	private void GitHubProxyOptionButtonOnItemSelected(long index) {
		var e = (GhProxyEnum)index;
		UI.Main.BaseConfig.GitHubProxy = e;
		UI.Main.BaseConfig.Save();
		Log.Debug($"更改GitHub代理为: {e}");
	}

	private void AutoCheckVersionCheckButtonOnToggled(bool toggled) {
		UI.Main.BaseConfig.AutoCheckVersion = toggled;
		UI.Main.BaseConfig.Save();
	}

	private void LastVersionLinkButtonOnPressed() { GetLastVersion(); }

	private async void GetLastVersion(bool onlyCheck = false) {
		var window = _launcherDownloadWindowScene!.Instantiate<LauncherDownloadWindow>();
		window.OnGetLatestRelease += release => {
			_lastVersion = release;
			_lastVersionLinkButton!.Text = release.TagName;
		};
		UI.Main.Instance?.AddChild(window);
		if (_lastVersion != null) {
			window.GetLatestRelease(_lastVersion);
			await window.Show();
		} else {
			await window.Show();
			window.GetLatestRelease(null, onlyCheck);
		}
	}

	public void UpdateLastVersion() {
		_gitHubProxyOptionButton!.Clear();
		var array = Enum.GetValuesAsUnderlyingType<GhProxyEnum>().Cast<GhProxyEnum>();
		foreach (var e in array) {
			_gitHubProxyOptionButton.AddItem(e.ToString(), (int)e);
		}

		_gitHubProxyOptionButton.Select((int)UI.Main.BaseConfig.GitHubProxy);
		_autoCheckVersionCheckButton!.ButtonPressed = UI.Main.BaseConfig.AutoCheckVersion;

		if (UI.Main.BaseConfig.AutoCheckVersion) {
			Dispatcher.SynchronizationContext.Post(_ => { GetLastVersion(true); }, null);
		}
	}
}
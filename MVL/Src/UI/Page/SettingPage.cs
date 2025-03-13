using Godot;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class SettingPage : MenuPage {
	[Export]
	private FontFile[]? _fonts;

	[Export]
	private OptionButton? _languageOptionButton;

	[Export]
	private SpinBox? _displayScaleSpinbox;

	private string[] _languages = TranslationServer.GetLoadedLocales();

	public override void _Ready() {
		base._Ready();
		_fonts.NotNull();
		_languageOptionButton.NotNull();
		_displayScaleSpinbox.NotNull();

		_displayScaleSpinbox.Value = UI.Main.BaseConfig.DisplayScale * 100;
		_languageOptionButton.ItemSelected += LanguageOptionButtonOnItemSelected;
		_displayScaleSpinbox.ValueChanged += DisplayScaleSpinboxOnValueChanged;

		var size = UI.Main.SceneTree.Root.Size;
		UI.Main.SceneTree.Root.Size = new(Mathf.CeilToInt(size.X * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(size.Y * UI.Main.BaseConfig.DisplayScale));
		TranslationServer.SetLocale(UI.Main.BaseConfig.Language);

		DisplayScaleSpinboxOnValueChanged(_displayScaleSpinbox.Value);
		UpdateLanguage();
		UI.Main.SceneTree.Root.Position -= (UI.Main.SceneTree.Root.Size - size) / 2;
	}

	private void DisplayScaleSpinboxOnValueChanged(double value) {
		UI.Main.BaseConfig.DisplayScale = (float)(_displayScaleSpinbox!.Value / 100);
		UI.Main.SceneTree.Root.MinSize = new(Mathf.CeilToInt(1122 * UI.Main.BaseConfig.DisplayScale),
			Mathf.CeilToInt(618 * UI.Main.BaseConfig.DisplayScale));
		foreach (var fontFile in _fonts!) {
			fontFile.Oversampling = UI.Main.BaseConfig.DisplayScale;
		}

		UI.Main.Instance?.WindowMaterial?.SetShaderParameter(StringNames.Radius, 10 * UI.Main.BaseConfig.DisplayScale);
		UI.Main.Instance?.RootOnSizeChanged();

		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void LanguageOptionButtonOnItemSelected(long index) {
		UI.Main.BaseConfig.Language = _languages[(int)index];
		TranslationServer.SetLocale(UI.Main.BaseConfig.Language);
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	public void UpdateLanguage() {
		_languageOptionButton!.Clear();
		var i = 0;
		foreach (var locale in _languages) {
			_languageOptionButton.AddItem(TranslationServer.GetLocaleName(locale), i);
			if (locale == UI.Main.BaseConfig.Language) {
				_languageOptionButton.Select(i);
			}

			i++;
		}
	}
}
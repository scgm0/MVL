using Godot;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class SettingPage : MenuPage {
	[Export]
	private OptionButton? _languageOptionButton;

	private string[] _languages = TranslationServer.GetLoadedLocales();

	public override void _Ready() {
		base._Ready();
		_languageOptionButton.NotNull();
		_languageOptionButton.ItemSelected += LanguageOptionButtonOnItemSelected;
		TranslationServer.SetLocale(UI.Main.BaseConfig.Language);
		UpdateLanguage();
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
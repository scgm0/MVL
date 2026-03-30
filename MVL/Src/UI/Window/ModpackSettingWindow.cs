using System.Collections.Generic;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackSettingWindow : BaseWindow {
	[Export]
	private PackedScene? _modpackNameLocalizedItemScene;

	[Export]
	private TextureRect? _modpackIconTextureRect;

	[Export]
	private Button? _modifyIconButton;

	[Export]
	private Button? _resetIconButton;

	[Export]
	private LineEdit? _modpackNamEdit;

	[Export]
	private ModpackNameLocalizedItem? _addModpackNameLocalizedItem;

	[Export]
	private VBoxContainer? _modpackNameLocalizedContainer;

	public ModpackConfig? ModpackConfig { get; set; }

	public override void _Ready() {
		base._Ready();
		_modpackNameLocalizedItemScene.NotNull();
		_modpackIconTextureRect.NotNull();
		_modifyIconButton.NotNull();
		_resetIconButton.NotNull();
		_modpackNamEdit.NotNull();
		_addModpackNameLocalizedItem.NotNull();
		_modpackNameLocalizedContainer.NotNull();
		ModpackConfig.NotNull();

		ModpackConfig.ModpackName = ModpackConfig.ModpackName with {
			Localizations = ModpackConfig.ModpackName.Localizations ?? []
		};

		_modifyIconButton.Pressed += ModifyIconButtonOnPressed;
		_resetIconButton.Pressed += ResetIconButtonOnPressed;
		_modpackNamEdit.EditingToggled += ModpackNamEditOnEditingToggled;
		_addModpackNameLocalizedItem.Localizations = ModpackConfig.ModpackName.Localizations;
		CancelButton!.Pressed += CancelButtonOnPressed;
		_addModpackNameLocalizedItem.AddLocalizedName += AddModpackNameLocalizedItemOnAddLocalizedName;

		if (ModpackConfig.ModpackName.Localizations.Count > 0) {
			var ls = ModpackConfig.ModpackName;
			var localizations = ls.Localizations;
			foreach (var localized in localizations) {
				AddModpackNameLocalizedItemOnAddLocalizedName(localized.Key, ls.Value, localized.Value, localizations);
			}
		}

		UpdateUi();
	}

	private void ResetIconButtonOnPressed() {
		_modpackIconTextureRect!.Texture = ModpackConfig.DefaultIcon;
		ModpackConfig!.SetModpackIcon(null);
		_resetIconButton!.Disabled = true;
	}

	private void ModifyIconButtonOnPressed() {
		var fileDialog = new FileDialog {
			Access = FileDialog.AccessEnum.Filesystem,
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Filters = ["*.png", "*.jpg", "*.jpeg", "Image Files", "image/png", "image/jpeg"],
			UseNativeDialog = true
		};
		fileDialog.Canceled += fileDialog.QueueFree;
		fileDialog.FileSelected += async path => {
			using var icon = await Tools.LoadTextureFromPath(path);
			if (icon != null) {
				_modpackIconTextureRect!.Texture = icon;
				ModpackConfig!.SetModpackIcon(icon);
				_resetIconButton!.Disabled = false;
			}

			fileDialog.QueueFree();
		};
		AddChild(fileDialog);
		fileDialog.Show();
	}

	private void ModpackNamEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		if (string.IsNullOrEmpty(_modpackNamEdit!.Text)) {
			_modpackNamEdit!.Text = ModpackConfig!.ModpackName.Value;
			return;
		}

		var ls = ModpackConfig!.ModpackName;
		var old = ls.Value;
		ls = ls with { Value = _modpackNamEdit!.Text };
		ModpackConfig!.ModpackName = ls;
		ModpackConfig.RemoveLocalizationTranslations(old);
		ModpackConfig.AddLocalizationTranslations(ls);
		ModpackConfig.Save();

		foreach (var node in _modpackNameLocalizedContainer!.GetChildren()) {
			var item = (ModpackNameLocalizedItem)node;
			item.Key = ls.Value;
		}
	}

	private void AddModpackNameLocalizedItemOnAddLocalizedName() {
		var ls = ModpackConfig!.ModpackName;
		var language = _addModpackNameLocalizedItem!.Language;
		var localizedName = _addModpackNameLocalizedItem.LocalizedName;
		var localizations = ls.Localizations!;
		localizations[language] = localizedName;
		ModpackConfig!.ModpackName = ls;
		ModpackConfig.AddLocalizationTranslation(ls.Value, localizedName, language);
		ModpackConfig.Save();

		_addModpackNameLocalizedItem.Language = string.Empty;
		_addModpackNameLocalizedItem.LocalizedName = string.Empty;

		AddModpackNameLocalizedItemOnAddLocalizedName(language, ls.Value, localizedName, localizations);
		UpdateUi();
	}

	private void AddModpackNameLocalizedItemOnAddLocalizedName(
		string language,
		string key,
		string localizedName,
		Dictionary<string, string> localizations) {
		var item = _modpackNameLocalizedItemScene!.Instantiate<ModpackNameLocalizedItem>();
		item.EditMode = ModpackNameLocalizedItem.EditModeEnum.View;
		item.Language = language;
		item.LocalizedName = localizedName;
		item.Key = key;
		item.Localizations = localizations;
		item.LocalizedNameChanged += () => OnItemOnLocalizedNameChanged(item);
		item.RemoveLocalizedName += () => OnItemOnRemoveLocalizedName(item);
		_modpackNameLocalizedContainer!.AddChild(item);
	}

	private void OnItemOnLocalizedNameChanged(ModpackNameLocalizedItem item) {
		item.Localizations![item.Language] = item.LocalizedName;
		ModpackConfig!.AddLocalizationTranslation(item.Key, item.LocalizedName, item.Language);
		ModpackConfig.Save();
	}

	private void OnItemOnRemoveLocalizedName(ModpackNameLocalizedItem item) {
		item.Localizations!.Remove(item.Language);
		ModpackConfig!.RemoveLocalizationTranslation(item.Key, item.Language);
		ModpackConfig.Save();
		item.QueueFree();
		UpdateUi();
	}

	private async void UpdateUi() {
		_modpackNamEdit!.Text = ModpackConfig!.ModpackName.Value;
		_modpackNameLocalizedContainer!.Visible = ModpackConfig!.ModpackName.Localizations is { Count: > 0 };
		_modpackIconTextureRect!.Texture = await ModpackConfig.GetModpackIconAsync();
		_resetIconButton!.Disabled = _modpackIconTextureRect.Texture == ModpackConfig.DefaultIcon;
	}
}
using System;
using System.Collections.Generic;
using Godot;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class ModpackLocalizedItem : HBoxContainer {
	public enum EditModeEnum { Add, View }

	[Export]
	private LineEdit? _localizedEdit;

	[Export]
	private LineEdit? _nameEdit;

	[Export]
	private Button? _addButton;

	[Export]
	private Button? _removeButton;

	[Export]
	public EditModeEnum EditMode { get; set; } = EditModeEnum.View;

	public string LocalizedName {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;
			_nameEdit?.Text = field;
			UpdateUi();
		}
	} = string.Empty;

	public string Language {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;
			_localizedEdit?.Text = field;
			UpdateUi();
		}
	} = string.Empty;

	public string Key { get; set; } = string.Empty;
	public Dictionary<string, string>? Localizations { get; set; }
	public event Action? LocalizedNameChanged;
	public event Action? AddLocalizedName;
	public event Action? RemoveLocalizedName;

	public override void _Ready() {
		_localizedEdit.NotNull();
		_nameEdit.NotNull();
		_addButton.NotNull();
		_removeButton.NotNull();

		_localizedEdit.Text = Language;
		_localizedEdit.Editable = EditMode == EditModeEnum.Add;
		_nameEdit.Text = LocalizedName;
		_addButton.Visible = EditMode == EditModeEnum.Add;
		_removeButton.Visible = EditMode == EditModeEnum.View;

		if (EditMode == EditModeEnum.View) {
			_localizedEdit.RightIcon = null;
		}

		_localizedEdit.EditingToggled += LocalizedEditOnEditingToggled;
		_localizedEdit.TextChanged += _ => UpdateUi();
		_nameEdit.EditingToggled += NameEditOnEditingToggled;
		_nameEdit.TextChanged += _ => UpdateUi();
		_addButton.Pressed += AddButtonOnPressed;
		_removeButton.Pressed += RemoveButtonOnPressed;

		UpdateUi();
	}

	private void AddButtonOnPressed() { AddLocalizedName?.Invoke(); }

	private void RemoveButtonOnPressed() { RemoveLocalizedName?.Invoke(); }

	private void LocalizedEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		Language = TranslationServer.StandardizeLocale(_localizedEdit!.Text);
		_localizedEdit.Text = Language;
		UpdateUi();
	}

	private void NameEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		var old = LocalizedName;
		LocalizedName = _nameEdit!.Text;
		if (old != LocalizedName) {
			LocalizedNameChanged?.Invoke();
		}

		UpdateUi();
	}

	private void UpdateUi() {
		if (EditMode == EditModeEnum.Add) {
			_addButton!.Disabled = string.IsNullOrEmpty(_localizedEdit!.Text) || string.IsNullOrEmpty(_nameEdit!.Text) || Localizations!.ContainsKey(_localizedEdit!.Text);
		}
	}
}
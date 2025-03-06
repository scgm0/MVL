using Godot;
using System;
using MVL.Utils.Help;
using SharedLibrary;

namespace MVL.UI.Item;

public partial class AccountSelectItem : PanelContainer {
	[Export]
	private Label? _accountNameLabel;

	[Export]
	private Button? _removeButton;

	[Export]
	private Button? _editButton;

	[Export]
	private CheckBox? _checkBox;

	[Signal]
	public delegate void SelectEventHandler(AccountSelectItem item);

	[Signal]
	public delegate void RemoveEventHandler(AccountSelectItem item);

	[Signal]
	public delegate void EditEventHandler(AccountSelectItem item);

	public Account? Account { get; set; }

	public override void _Ready() {
		base._Ready();
		_accountNameLabel.NotNull();
		_removeButton.NotNull();
		_editButton.NotNull();
		_checkBox.NotNull();
		Account.NotNull();

		_accountNameLabel.Text = Account.PlayerName;
		_checkBox.ButtonPressed = Main.BaseConfig.CurrentAccount == Account.PlayerName;

		_removeButton.Pressed += RemoveButtonOnPressed;
		_editButton.Pressed += EditButtonOnPressed;
		_checkBox.Toggled += CheckBoxOnToggled;
	}

	private void CheckBoxOnToggled(bool toggledOn) {
		if (!toggledOn) {
			return;
		}

		EmitSignalSelect(this);
	}

	private void EditButtonOnPressed() { EmitSignalEdit(this); }

	private void RemoveButtonOnPressed() { EmitSignalRemove(this); }
}
using System.Collections.Generic;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class InfoPage : MenuPage {
	[Export]
	private PackedScene? _itemScene;

	[Export]
	private ButtonGroup? _buttonGroup;

	[Export]
	private Control? _titleBar;

	[Export]
	private Button? _authorButton;

	[Export]
	private Button? _donorButton;

	[Export]
	private Button? _licenseButton;

	[Export]
	private VBoxContainer? _list;

	public override void _Ready() {
		base._Ready();
		_itemScene.NotNull();
		_buttonGroup.NotNull();
		_titleBar.NotNull();
		_list.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_buttonGroup.Pressed += ButtonGroupOnPressed;
		UpdateList(Info.AUTHORS);
	}

	private void ButtonGroupOnPressed(BaseButton button) {
		if (button == _authorButton) {
			UpdateList(Info.AUTHORS);
		}

		if (button == _donorButton) {
			UpdateList(Info.DONORS);
		}

		if (button == _licenseButton) {
			UpdateList(Info.LICENSES);
		}
	}

	private void OnVisibilityChanged() { _titleBar!.Visible = !Visible; }

	private void UpdateList(IReadOnlyList<(string, string)> items) {
		foreach (var child in _list!.GetChildren()) {
			child.QueueFree();
		}

		foreach (var valueTuple in items) {
			var item = _itemScene!.Instantiate<InfoItem>();
			item.Title = valueTuple.Item1;
			item.Content = valueTuple.Item2;
			_list.AddChild(item);
		}
	}
}
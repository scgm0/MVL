using Godot;
using System;
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
	private VBoxContainer? _list;

	public override void _Ready() {
		base._Ready();
		_itemScene.NotNull();
		_buttonGroup.NotNull();
		_titleBar.NotNull();
		_list.NotNull();
		VisibilityChanged += OnVisibilityChanged;
	}

	private void OnVisibilityChanged() { _titleBar!.Visible = !Visible; }
}
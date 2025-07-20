using Godot;
using System;
using System.Collections.Generic;
using MVL.Utils.Help;

namespace MVL.UI.Other;

public partial class SelectionButton : Button {
	[Export] protected Button? Bg;

	[Export]
	private PanelContainer? _panelContainer;

	[Export]
	private ScrollContainer? _scrollContainer;

	[Export]
	private VBoxContainer? _vboxContainer;

	[Export]
	public bool Radio { get; set; }

	private ButtonGroup _buttonGroup = new();
	public IEnumerable<string> SelectionList { get; set; } = [];
	public int MaxShow { get; set; } = 5;

	public List<int> Selected { get; set; } = [];

	public override void _Ready() {
		Bg.NotNull();
		_panelContainer.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		Pressed += ShowList;
		Bg.Pressed += Bg.Hide;
	}

	public void ShowList() {
		Bg!.Show();

		var rect = GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X };
		_panelContainer.GlobalPosition = rect.Position;

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var i = 0;
		var maxHeight = 0f;
		foreach (var item in SelectionList) {
			var button = new CheckBox {
				Text = item,
				TooltipText = item,
				Alignment = HorizontalAlignment.Center
			};

			if (Radio) {
				button.ButtonGroup = _buttonGroup;
			}

			if (Selected.Contains(i)) {
				button.ButtonPressed = true;
			}

			button.Toggled += on => {
				if (on) {
					Selected.Add(button.GetIndex());
				} else {
					Selected.Remove(button.GetIndex());
				}
			};

			_vboxContainer!.AddChild(button);
			i++;
			if (i <= MaxShow) {
				maxHeight = _vboxContainer!.GetCombinedMinimumSize().Y;
			}
		}

		var maxWidth = _vboxContainer!.GetCombinedMinimumSize().X;
		if (maxWidth > _panelContainer!.Size.X) {
			_panelContainer!.GlobalPosition = _panelContainer.GlobalPosition with {
				X = _panelContainer.GlobalPosition.X - (maxWidth - _panelContainer!.Size.X) / 2
			};
		} else {
			maxWidth = _panelContainer!.Size.X;
		}

		_panelContainer!.Size = _panelContainer!.Size with { Y = maxHeight, X = maxWidth };
	}
}
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	private float _maxHeight;

	public IList<string> SelectionList { get; set; } = [];

	public int MaxShow { get; set; } = 5;

	public List<int> Selected {
		get;
		set {
			field = value.OrderBy(i => i).ToList();
			foreach (var i in field) {
				var child = _vboxContainer?.GetChild<Button?>(i);
				if (child != null) {
					child.ButtonPressed = true;
				}
			}
		}
	} = [];

	public override void _Ready() {
		Bg.NotNull();
		_panelContainer.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		Pressed += ShowList;
		Bg.Pressed += Bg.Hide;
	}

	public async Task UpdateList(IList<string> list) {
		SelectionList = list;
		Selected.Clear();

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var i = 0;
		_maxHeight = 0f;
		foreach (var item in SelectionList) {
			var button = new CheckBox {
				Text = item,
				TooltipText = item,
				Alignment = HorizontalAlignment.Center
			};

			if (Radio) {
				button.ButtonGroup = _buttonGroup;
			}

			button.Toggled += on => {
				if (on) {
					if (Selected.Contains(button.GetIndex())) {
						return;
					}
					Selected.Add(button.GetIndex());
				} else {
					if (!Selected.Contains(button.GetIndex())) {
						return;
					}
					Selected.Remove(button.GetIndex());
				}
			};

			_vboxContainer!.AddChild(button);
			await ToSignal(Main.SceneTree, SceneTree.SignalName.PhysicsFrame);
			i++;
			if (i <= MaxShow) {
				_maxHeight = _vboxContainer!.GetCombinedMinimumSize().Y;
			}
		}
	}

	public void ShowList() {
		Bg!.Show();

		var rect = GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X };
		_panelContainer.GlobalPosition = rect.Position;

		var maxWidth = _vboxContainer!.GetCombinedMinimumSize().X;
		if (maxWidth > _panelContainer!.Size.X) {
			_panelContainer!.GlobalPosition = _panelContainer.GlobalPosition with {
				X = _panelContainer.GlobalPosition.X - (maxWidth - _panelContainer!.Size.X) / 2
			};
		} else {
			maxWidth = _panelContainer!.Size.X;
		}

		_panelContainer!.Size = _panelContainer!.Size with { Y = _maxHeight, X = maxWidth };
	}
}
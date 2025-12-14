using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.Utils;
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
	private Tween? _tween;
	public string[] SelectionList { get; set; } = [];

	public int MaxShow { get; set; } = 5;

	public List<int> Selected {
		get;
		set {
			field = value.OrderBy(i => i).ToList();
			foreach (var child in field.Select(i => _vboxContainer?.GetChild<Button?>(i))) {
				child?.ButtonPressed = true;
			}
		}
	} = [];

	public event Action? SelectionChanged;

	public override void _Ready() {
		Bg.NotNull();
		_panelContainer.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		Pressed += ShowList;
		Bg.Pressed += BgOnPressed;
		Bg.VisibilityChanged += BgOnVisibilityChanged;
	}

	private void BgOnVisibilityChanged() {
		var rid = Bg!.GetCanvasItem();
		if (Bg.Visible) {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, true, Bg.GetGlobalRect());
		} else {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, false, new());
		}
	}

	private async void BgOnPressed() {
		Bg!.Disabled = true;
		_tween?.Kill();
		_tween?.Dispose();
		_tween = CreateTween();
		_tween.TweenProperty(_panelContainer, "size:y", 0, Mathf.Min(_vboxContainer!.GetChildCount(), 5) * 0.02f);
		await ToSignal(_tween, Tween.SignalName.Finished);

		_tween.Dispose();
		_tween = null;
		Bg!.Hide();
		Bg.Disabled = false;
	}

	public async Task UpdateList(string[] list) {
		SelectionList = list;
		Selected.Clear();

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var i = 0;
		_maxHeight = 0f;
		_scrollContainer!.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
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

				SelectionChanged?.Invoke();
			};

			_vboxContainer!.AddChild(button);
			await ToSignal(Tools.SceneTree, SceneTree.SignalName.ProcessFrame);
			i++;
			if (i <= MaxShow) {
				_maxHeight = _vboxContainer!.GetCombinedMinimumSize().Y;
			}
		}
	}

	public void ShowList() {
		Bg!.Modulate = Colors.Transparent;
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

		_scrollContainer!.VerticalScrollMode =
			_vboxContainer.GetChildCount() > MaxShow ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.ShowNever;
		_panelContainer!.Size = _panelContainer!.Size with { Y = 0, X = maxWidth };
		Bg.Modulate = Colors.White;
		_tween = CreateTween();
		_tween.TweenProperty(_panelContainer, "size:y", _maxHeight, Mathf.Min(_vboxContainer!.GetChildCount(), 5) * 0.02f);
	}
}
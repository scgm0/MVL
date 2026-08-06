using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.Help;

namespace MVL.UI.Other;

public abstract partial class CandidateLineEdit<T> : LineEdit {
	[Export] protected Button? Bg;

	[Export]
	private PanelContainer? _panelContainer;

	[Export]
	private ScrollContainer? _scrollContainer;

	[Export]
	private VBoxContainer? _vboxContainer;

	private readonly AsyncDebouncer _debouncer;

	public T[] Candidates { get; set; } = [];

	public T? Selected { get; set; }

	public int MaxShow { get; set; } = 5;

	public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(150);

	public CandidateLineEdit() { _debouncer = new(Delay); }

	public override void _Ready() {
		Bg.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		Bg.MouseFilter = MouseFilterEnum.Stop;
		Bg.Pressed += Bg.Hide;
		Bg.VisibilityChanged += BgOnVisibilityChanged;

		TextChanged += OnTextChanged;
	}

	public override void _ExitTree() {
		base._ExitTree();
		_debouncer.Cancel();
	}

	private async void OnTextChanged(string newText) {
		await _debouncer.DebounceAsync(UpdateCandidates);
	}

	private void BgOnVisibilityChanged() {
		var rid = Bg!.GetCanvasItem();
		if (Bg.Visible) {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, true, Bg.GetGlobalRect());
		} else {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, false, new());
		}
	}

	public abstract ReadOnlySpan<T> GetCandidate();

	public abstract Button GetItemContainer(T candidate);

	public void UpdateCandidates() {
		if (!IsInstanceValid(this)) {
			return;
		}

		Selected = default;

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var list = GetCandidate();
		if (list.Length == 0) {
			Bg!.Hide();
			return;
		}

		Bg!.Show();

		var rect = GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X };
		_panelContainer.GlobalPosition = rect.Position;

		var i = 0;
		var maxHeight = 0f;
		foreach (var item in list) {
			var button = GetItemContainer(item);
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
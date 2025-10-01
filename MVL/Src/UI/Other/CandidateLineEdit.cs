using System;
using Godot;
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

	[Export]
	private Timer? _timer;

	public T[] Candidates { get; set; } = [];

	public T? Selected { get; set; }

	public int MaxShow { get; set; } = 5;

	protected string SelfText = string.Empty;

	public override void _Ready() {
		Bg.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();
		_timer.NotNull();

		Bg.MouseFilter = MouseFilterEnum.Stop;
		Bg.Pressed += Bg.Hide;

		TextChanged += _ => {
			_timer.Start(0.2);
		};

		_timer.Timeout += UpdateCandidates;
	}

	public abstract Span<(T data, int ratio)> GetCandidate();

	public abstract Button GetItemContainer((T data, int ratio) candidate);

	public abstract void Sorted();

	public void UpdateCandidates() {
		Selected = default;
		SelfText = Text;

		Bg!.Show();

		var rect = GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X };
		_panelContainer.GlobalPosition = rect.Position;

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var list = GetCandidate();

		var i = 0;
		var maxHeight = 0f;
		foreach (var item in list) {
			if (item.ratio <= 0) {
				continue;
			}

			var button = GetItemContainer(item);
			_vboxContainer!.AddChild(button);

			i++;
			if (i <= MaxShow) {
				maxHeight = _vboxContainer!.GetCombinedMinimumSize().Y;
			}
		}

		Sorted();

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
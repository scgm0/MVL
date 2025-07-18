using Godot;
using System.Collections.Generic;
using System.Linq;
using MVL.Utils.Help;

namespace MVL.UI.Other;

public abstract partial class CandidateLineEdit<T> : LineEdit {
	[Export]
	private Button? _button;

	[Export]
	private PanelContainer? _panelContainer;

	[Export]
	private ScrollContainer? _scrollContainer;

	[Export]
	private VBoxContainer? _vboxContainer;

	public IEnumerable<T> Candidates { get; set; } = [];

	public T? Selected { get; set; }

	public int MaxCandidates { get; set; } = 10;

	public int MaxShow { get; set; } = 5;

	public override void _Ready() {
		_button.NotNull();
		_scrollContainer.NotNull();
		_vboxContainer.NotNull();

		_button.MouseFilter = MouseFilterEnum.Stop;
		_button.Pressed += _button.Hide;

		TextChanged += _ => { UpdateCandidates(); };
	}

	public abstract IEnumerable<(T data, string candidate, int ratio)> GetCandidate();

	public void UpdateCandidates() {
		if (!Candidates.Any()) {
			return;
		}

		if (!_button!.Visible) {
			_button.Show();
			var rect = GetGlobalRect();
			rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
			_panelContainer!.Size = _panelContainer!.Size with { X = rect.Size.X };
			_panelContainer.GlobalPosition = rect.Position;
		}

		foreach (var child in _vboxContainer!.GetChildren()) {
			child.Free();
		}

		var list = GetCandidate().OrderByDescending(item => item.ratio).Take(MaxCandidates);

		var i = 0;
		var maxHeight = 0f;
		foreach (var (data, candidate, ratio) in list) {
			if (ratio <= 0) {
				continue;
			}

			var button = new Button {
				Text = candidate,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				TooltipText = candidate,
			};

			button.Pressed += () => {
				Text = candidate;
				Selected = data;
				_button.Hide();
			};

			_vboxContainer!.AddChild(button);

			i++;
			if (i <= MaxShow) {
				maxHeight = _vboxContainer!.GetCombinedMinimumSize().Y;
			}
		}

		_panelContainer!.Size = _panelContainer!.Size with { Y = maxHeight };
	}
}
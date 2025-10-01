using System;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor?> {
	private (ApiAuthor? data, int ratio)[] _cachedCandidatesWithRatio = [];

	public int MaxCandidates { get; set; } = 10;

	public override void _Ready() {
		base._Ready();
		Bg!.Hidden += BgOnHidden;
	}

	private void BgOnHidden() {
		if (Selected is not null) {
			return;
		}

		Text = string.Empty;
		SelfText = string.Empty;
	}

	public override Span<(ApiAuthor? data, int ratio)> GetCandidate() {
		if (_cachedCandidatesWithRatio.Length != Candidates.Length) {
			_cachedCandidatesWithRatio = new (ApiAuthor? data, int ratio)[Candidates.Length];
		}

		if (Candidates.Length == 0) {
			return [];
		}

		for (var i = 0; i < Candidates.Length; i++) {
			var data = Candidates[i];
			var name = data?.Name ?? data?.UserId.ToString() ?? string.Empty;
			var ratio = Fuzzy.Ratio(name, SelfText);
			_cachedCandidatesWithRatio[i] = (data, ratio);
		}

		Array.Sort(_cachedCandidatesWithRatio, (x, y) => y.ratio.CompareTo(x.ratio));

		var count = Math.Min(Candidates.Length, MaxCandidates);

		return _cachedCandidatesWithRatio.AsSpan(0, count);
	}

	public override Button GetItemContainer((ApiAuthor? data, int ratio) item) {
		var button = new Button {
			Text = item.data?.Name,
			TooltipText = $"{item.data?.Name} ({item.data?.UserId})",
			Flat = true
		};

		button.Pressed += () => {
			Selected = item.data;
			Text = item.data?.Name;
			Bg?.Hide();
		};

		return button;
	}

	public override void Sorted() {
		Array.Clear(_cachedCandidatesWithRatio);
	}
}
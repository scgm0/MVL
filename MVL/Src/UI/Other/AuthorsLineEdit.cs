using System;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor?> {
	private (ApiAuthor? data, int ratio)[]? _cachedCandidatesWithRatio;
	private string? _cachedSelfText;

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
		if (_cachedSelfText == SelfText && _cachedCandidatesWithRatio is not null) {
			return _cachedCandidatesWithRatio;
		}

		_cachedSelfText = SelfText;

		if (_cachedCandidatesWithRatio == null || _cachedCandidatesWithRatio.Length != Candidates.Length) {
			_cachedCandidatesWithRatio = new (ApiAuthor? data, int ratio)[Candidates.Length];
		}

		for (var i = 0; i < Candidates.Length; i++) {
			var data = Candidates[i];
			var name = data?.Name ?? data?.UserId.ToString();
			var ratio = Fuzzy.Ratio(name, SelfText);
			_cachedCandidatesWithRatio[i] = (data, ratio);
		}

		Array.Sort(_cachedCandidatesWithRatio, (x, y) => y.ratio.CompareTo(x.ratio));

		return _cachedCandidatesWithRatio;
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
}
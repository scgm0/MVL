using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor?> {
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
	}

	public override ReadOnlySpan<ApiAuthor?> GetCandidate() {
		if (Candidates.Length == 0) {
			return [];
		}

		List<(ApiAuthor? data, int ratio)> cachedCandidatesWithRatio = [];
		foreach (var data in Candidates) {
			var name = data?.Name ?? data?.UserId.ToString() ?? string.Empty;
			var ratio = Fuzzy.Ratio(name, Text);
			if (ratio > 0) {
				cachedCandidatesWithRatio.Add((data, ratio));
			}
		}

		if (cachedCandidatesWithRatio.Count == 0) {
			return [];
		}

		cachedCandidatesWithRatio.Sort((a, b) => b.ratio - a.ratio);

		var count = Math.Min(cachedCandidatesWithRatio.Count, MaxCandidates);
		var result = new ApiAuthor?[count];
		for (var i = 0; i < count; i++) {
			result[i] = cachedCandidatesWithRatio[i].data;
		}

		return result;
	}

	public override Button GetItemContainer(ApiAuthor? item) {
		var button = new Button {
			Text = item?.Name,
			TooltipText = $"{item?.Name} ({item?.UserId})",
			Flat = true
		};

		button.Pressed += () => {
			Selected = item;
			Text = item?.Name;
			Bg?.Hide();
		};

		return button;
	}
}
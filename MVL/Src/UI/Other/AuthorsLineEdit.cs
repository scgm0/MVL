using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using Godot;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor?> {

	public override void _Ready() {
		base._Ready();
		Bg!.Hidden += BgOnHidden;
	}

	private void BgOnHidden() {
		if (Selected is null) {
			Text = "";
		}
	}

	public override IEnumerable<(ApiAuthor? data, int ratio)> GetCandidate() {
		return Candidates.Select(data => {
			var name = data?.Name ?? data?.UserId.ToString();
			var ratio = Fuzz.Ratio(name, Text);
			return (data, ratio);
		});
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
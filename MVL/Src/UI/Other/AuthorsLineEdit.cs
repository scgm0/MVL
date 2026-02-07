using System;
using System.Buffers;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor?> {
	private ApiAuthor?[] _resultCache = [];
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

		var pool = ArrayPool<(ApiAuthor? data, int ratio)>.Shared;
		var pooledArray = pool.Rent(Candidates.Length);

		var matchCount = 0;
		try {
			var searchText = Text;
			ReadOnlySpan<ApiAuthor?> candidatesSpan = Candidates;

			foreach (var data in candidatesSpan) {
				if (data == null) {
					continue;
				}

				var name = data.Value.Name;
				if (string.IsNullOrEmpty(name)) {
					name = data.Value.UserId.ToString();
				}

				var ratio = Fuzzy.Ratio(name, searchText);

				if (ratio <= 0) {
					continue;
				}

				pooledArray[matchCount] = (data, ratio);
				matchCount++;
			}

			if (matchCount == 0) {
				return [];
			}

			var validSlice = pooledArray.AsSpan(0, matchCount);
			validSlice.Sort((a, b) => b.ratio - a.ratio);

			var finalCount = Math.Min(matchCount, MaxCandidates);
			if (_resultCache.Length < finalCount) {
				Array.Resize(ref _resultCache, Math.Max(finalCount, _resultCache.Length * 2));
			}

			for (var i = 0; i < finalCount; i++) {
				_resultCache[i] = validSlice[i].data;
			}

			return _resultCache.AsSpan(0, finalCount);
		} finally {
			pool.Return(pooledArray, clearArray: true);
		}
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
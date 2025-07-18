using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using MVL.Utils.Game;

namespace MVL.UI.Other;

public partial class AuthorsLineEdit : CandidateLineEdit<ApiAuthor> {
	public override IEnumerable<(ApiAuthor data, string candidate, int ratio)> GetCandidate() {
		return Candidates.Select(data => {
			var name = data.Name ?? data.UserId.ToString();
			var ratio = Fuzz.Ratio(name, Text);
			return (data, name, ratio);
		});
	}
}
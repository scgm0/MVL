using Godot;
using MVL.UI.Page;

namespace MVL.UI.Item;

[GlobalClass]
public partial class MenuItem : Button {
	[Export]
	public MenuPage? Page { get; set; }

	public override void _Ready() {
		TooltipText = Text;
		Toggled += OnToggled;
		if (Page != null) {
			Page.Visible = ButtonPressed;
		}
	}

	private void OnToggled(bool toggledon) {
		if (Page is null || !toggledon || Page.Visible == toggledon) {
			return;
		}

		Page.Show();
	}

}
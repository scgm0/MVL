using Godot;
using 复古物语启动器.UI.Page;

namespace 复古物语启动器.UI.Item;

[GlobalClass]
public partial class MenuItem : Button {
	[Export]
	public MenuPage? Page { get; set; }

	public override void _Ready() {
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
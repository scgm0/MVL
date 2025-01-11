using Godot;
using 复古物语启动器.Utils.Help;

namespace 复古物语启动器.UI;

public partial class MenuPanel : NativeWindowUtility {
	[Export]
	public Button? MenuButton { get; set; }

	[Export]
	public AnimationPlayer? AnimationPlayer { get; set; }

	static private readonly StringName AnimationName = new("unfold");

	public override void _Ready() {
		base._Ready();
		MenuButton.NotNull();
		AnimationPlayer.NotNull();
		MenuButton.Toggled += MenuButtonOnToggled;
	}

	private void MenuButtonOnToggled(bool toggledOn) {
		AnimationPlayer?.Play(AnimationName, -1, (float)(toggledOn ? 1.0 : -1.0), !toggledOn);
	}
}
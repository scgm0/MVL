using Godot;
using 复古物语启动器.Utils;
using 复古物语启动器.Utils.Help;

namespace 复古物语启动器.UI;

public partial class MenuPanel : NativeWindowUtility {
	[Export]
	private Button? _menuButton;

	[Export]
	private TabContainer? _pageContainer;

	[Export]
	private AnimationPlayer? _animationPlayer;

	[Export]
	private ShaderMaterial? _blurShaderMaterial;
	
	public override void _Ready() {
		base._Ready();
		_menuButton.NotNull();
		_pageContainer.NotNull();
		_animationPlayer.NotNull();
		_blurShaderMaterial.NotNull();
		_menuButton.Toggled += MenuButtonOnToggled;
		_pageContainer.TabChanged += PageContainerOnTabChanged;
	}

	private void PageContainerOnTabChanged(long tab) {
		if (tab != 0) {
			_blurShaderMaterial!.SetShaderParameter(StringNames.Lod, 2.4);
		} else {
			_blurShaderMaterial!.SetShaderParameter(StringNames.Lod, 0);
		}
	}

	private void MenuButtonOnToggled(bool toggledOn) {
		_animationPlayer?.Play(StringNames.Unfold, -1, (float)(toggledOn ? 1.0 : -1.0), !toggledOn);
	}
}
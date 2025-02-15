using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class BaseWindow : Control {
	[Export]
	private PanelContainer? _container;

	[Export]
	private AnimationPlayer? _animationPlayer;

	public override void _Ready() {
		_container.NotNull();
		_animationPlayer.NotNull();
		_container.PivotOffset = _container.Size / 2.0f;
	}

	public new async Task Show() {
		Modulate = Colors.Transparent;
		_container!.Scale = Vector2.Zero;
		base.Show();
		_animationPlayer!.Play(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
	}

	public new async Task Hide() {
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		base.Hide();
	}
}
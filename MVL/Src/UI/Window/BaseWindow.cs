using System;
using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class BaseWindow : Control {
	[Export] protected PanelContainer? Container;

	[Export]
	private AnimationPlayer? _animationPlayer;

	[Export] public Label? TitleLabel;

	[Export] public Button? CancelButton;

	[Export] public Button? OkButton;

	[Export] public bool BackBufferCopy {
		get;
		set {
			field = value;
			RenderingServer.CanvasItemSetCopyToBackbuffer(GetCanvasItem(), value, GetGlobalRect());
		}
	}

	[Signal]
	public delegate void CancelEventHandler();

	public override void _Ready() {
		Container.NotNull();
		_animationPlayer.NotNull();
		TitleLabel.NotNull();
		CancelButton.NotNull();
		OkButton.NotNull();
	}

	public new virtual async Task Show() {
		Modulate = Colors.Transparent;
		Container!.Scale = Vector2.Zero;
		base.Show();
		_animationPlayer!.Play(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
	}

	public new virtual async Task Hide() {
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		base.Hide();
	}

	protected virtual async void CancelButtonOnPressed() {
		try {
			await Hide();
			EmitSignalCancel();
		} catch (Exception e) {
			Log.Error(e);
		}
	}
}
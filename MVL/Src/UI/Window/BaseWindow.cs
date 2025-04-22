using System;
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

	[Export] protected Label? TitleLabel;

	[Export] protected Button? CancelButton;

	[Export] protected Button? OkButton;

	[Signal]
	public delegate void CancelEventHandler();

	public override void _Ready() {
		_container.NotNull();
		_animationPlayer.NotNull();
		TitleLabel.NotNull();
		CancelButton.NotNull();
		OkButton.NotNull();
	}

	public new virtual async Task Show() {
		_container!.PivotOffset = _container.GetCombinedMinimumSize() / 2.0f;
		Modulate = Colors.Transparent;
		_container.Scale = Vector2.Zero;
		base.Show();
		_animationPlayer!.Play(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
	}

	public new virtual async Task Hide() {
		_container!.PivotOffset = _container.Size / 2.0f;
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		base.Hide();
	}

	protected virtual async void CancelButtonOnPressed() {
		try {
			await Hide();
			EmitSignalCancel();
		} catch (Exception e) {
			GD.PrintErr(e.ToString());
		}
	}
}
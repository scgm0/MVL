using Godot;
using System;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ConfirmationWindow : BaseWindow {
	[Export]
	private RichTextLabel? _messageLabel;

	[Signal]
	public delegate void ConfirmEventHandler();

	public string? Message {
		get => _messageLabel?.Text ?? field;
		set {
			field = value;
			if (_messageLabel != null) {
				_messageLabel.Text = value;
			}
		}
	} = "确定吗？";

	public override void _Ready() {
		base._Ready();
		OkButton.NotNull();
		CancelButton.NotNull();
		_messageLabel.NotNull();

		_messageLabel.Text = Message;

		OkButton.Pressed += EmitSignalConfirm;
		CancelButton.Pressed += CancelButtonOnPressed;
	}
}
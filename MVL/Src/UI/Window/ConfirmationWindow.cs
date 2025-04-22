using System.Threading.Tasks;
using Godot;
using MVL.Utils;
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
		_messageLabel.MetaClicked += Tools.RichTextOpenUrl;

		OkButton.Pressed += EmitSignalConfirm;
		CancelButton.Pressed += CancelButtonOnPressed;
	}

	public override async Task Show() {
		_messageLabel!.Size = _messageLabel.GetCombinedMinimumSize();
		await base.Show();
	}
}
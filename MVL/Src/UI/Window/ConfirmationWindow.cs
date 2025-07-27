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
			_messageLabel?.Text = value;
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
		_messageLabel!.FitContent = true;
		_messageLabel.ScrollActive = false;
		_messageLabel.CustomMinimumSize = Vector2.Zero;
		_messageLabel.Size = _messageLabel.GetCombinedMinimumSize();

		if (_messageLabel.Size.Y > 450) {
			_messageLabel.FitContent = false;
			_messageLabel.ScrollActive = true;
			_messageLabel.CustomMinimumSize = _messageLabel.Size with { X = _messageLabel!.Size.X + 20, Y = 450 };
		}

		await base.Show();
	}
}
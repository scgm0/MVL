using Godot;
using System;
using MVL.Utils.Help;

public partial class PopupWindow : Control {
	[Export]
	private Button? _closeButton;

	[Export]
	public Control? Content;

	private Control? Parent => GetParent<Control>();

	public event Action? CloseRequested;

	public override void _Ready() {
		_closeButton.NotNull();
		Content.NotNull();

		_closeButton.MouseFilter = MouseFilterEnum.Stop;
		_closeButton.Pressed += () => CloseRequested?.Invoke();
	}

	public void Popup() {
		Show();
		if (Parent is null) {
			return;
		}

		var rect = Parent.GetGlobalRect();
		rect.Position = rect.Position with { Y = rect.Position.Y + rect.Size.Y };
		Content!.CustomMinimumSize = Content!.CustomMinimumSize with { X = rect.Size.X };
		Content.GlobalPosition = rect.Position;
	}
}
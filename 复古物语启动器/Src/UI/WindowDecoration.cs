using Godot;
using System;
using 复古物语启动器.Utils.Extensions;
using 复古物语启动器.Utils.Help;

public partial class WindowDecoration : Control {
	[Export]
	public Button? MinimizeButton { get; set; }

	[Export]
	public Button? ResizeButton { get; set; }

	[Export]
	public Button? CloseButton { get; set; }

	public override void _Ready() {
		NullExceptionHelper.NotNull(MinimizeButton, ResizeButton, CloseButton);
		MinimizeButton.Pressed += MinimizeButtonOnPressed;
		ResizeButton.Pressed += ResizeButtonOnPressed;
		CloseButton.Pressed += CloseButtonOnPressed;
	}

	private void CloseButtonOnPressed() { GetTree().Quit(); }

	private void ResizeButtonOnPressed() {
		var mode = GetLastExclusiveWindow().Mode = GetLastExclusiveWindow().Mode == Window.ModeEnum.Windowed
			? Window.ModeEnum.Maximized
			: Window.ModeEnum.Windowed;
		ResizeButton!.Icon = mode switch {
			Window.ModeEnum.Windowed => GD.Load<Texture2D>("res://Assets/Gui/WindowMaximize.svg"),
			Window.ModeEnum.Maximized => GD.Load<Texture2D>("res://Assets/Gui/WindowRestore.svg"),
			_ => throw new ArgumentOutOfRangeException(nameof(mode))
		};
	}

	private void MinimizeButtonOnPressed() { GetLastExclusiveWindow().Minimize(); }
}
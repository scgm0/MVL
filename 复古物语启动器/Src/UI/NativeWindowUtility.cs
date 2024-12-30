using System;
using System.Diagnostics;
using Godot;
using 复古物语启动器.Utils.Extensions;

namespace 复古物语启动器.UI;

[GlobalClass]
public partial class NativeWindowUtility : Control {
	static private readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(300);
	private WindowEdge _windowEdge;
	private bool _isResizable;
	private bool _pressed;
	private long _pressedLastTime;

	private Timer _timer = new() {
		WaitTime = 0.5f,
		Autostart = false,
		OneShot = true
	};

	[Export]
	public bool IsDraggable { get; set; }

	[Export]
	public bool DoubleClickWindowMaximized { get; set; }

	[Export]
	public bool IsResizable {
		get => _isResizable;
		set {
			_isResizable = value;
			WindowEdge = _windowEdge;
		}
	}

	[Export(PropertyHint.Flags, "左,右,上,下")]
	public WindowEdge WindowEdge {
		get => _windowEdge;
		set {
			_windowEdge = value;
			MouseDefaultCursorShape = IsDraggable || !IsResizable
				? CursorShape.Arrow
				: _windowEdge switch {
					WindowEdge.Left => CursorShape.Hsize,
					WindowEdge.Right => CursorShape.Hsize,
					WindowEdge.Top => CursorShape.Vsize,
					WindowEdge.Bottom => CursorShape.Vsize,
					WindowEdge.Left | WindowEdge.Top => CursorShape.Fdiagsize,
					WindowEdge.Right | WindowEdge.Top => CursorShape.Bdiagsize,
					WindowEdge.Left | WindowEdge.Bottom => CursorShape.Bdiagsize,
					WindowEdge.Right | WindowEdge.Bottom => CursorShape.Fdiagsize,
					_ => CursorShape.Arrow
				};
		}
	}

	public override void _Ready() {
		Resized += OnResized;
		_timer.Timeout += OnTimerOnTimeout;
		AddChild(_timer, false, InternalMode.Back);
		OnResized();
	}

	private void OnResized() {
		if (GetLastExclusiveWindow().Mode != Window.ModeEnum.Windowed && IsResizable) {
			Visible = false;
		} else {
			Visible = true;
		}
	}

	public override void _GuiInput(InputEvent @event) {
		if (IsDraggable) {
			HandleDrag(@event);
		} else if (IsResizable) {
			HandleResize(@event);
		}

		if (DoubleClickWindowMaximized) {
			HandleMaximize(@event);
		}

		@event.Dispose();
	}

	private void HandleMaximize(InputEvent @event) {
		if (@event is InputEventMouseMotion) {
			_pressedLastTime = 0;
			return;
		}

		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) {
			return;
		}

		if (_pressedLastTime != 0 && Stopwatch.GetElapsedTime(_pressedLastTime) < DoubleClickInterval) {
			_pressedLastTime = 0;
			GetLastExclusiveWindow().Mode = GetLastExclusiveWindow().Mode == Window.ModeEnum.Windowed
				? Window.ModeEnum.Maximized
				: Window.ModeEnum.Windowed;
		} else {
			_pressedLastTime = Stopwatch.GetTimestamp();
		}
	}

	private void HandleResize(InputEvent @event) {
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && WindowEdge != 0) {
			GetLastExclusiveWindow().StartResizeDrag(WindowEdge);
		}
	}

	private void HandleDrag(InputEvent @event) {
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) {
			_pressed = true;
			_timer.Start();
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) {
			StopTimer();
		}

		if (!_pressed || @event is not InputEventMouseMotion) return;
		StopTimer();
		GetLastExclusiveWindow().StartMoveDrag();
	}

	private void OnTimerOnTimeout() {
		_pressed = false;
		GetLastExclusiveWindow().StartMoveDrag();
	}

	private void StopTimer() {
		_timer.Stop();
		_pressed = false;
	}
}
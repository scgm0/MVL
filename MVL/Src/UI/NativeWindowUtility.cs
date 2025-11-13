using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace MVL.UI;

[GlobalClass]
public partial class NativeWindowUtility : Control {
	static private readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(300);
	private DisplayServer.WindowResizeEdge _windowEdge;
	private bool _isResizable;
	private bool _pressed;
	private long _pressedLastTime;

	private CancellationTokenSource? _dragDelayCts;
	private Godot.Window? _window;

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

	[Export]
	public DisplayServer.WindowResizeEdge WindowEdge {
		get => _windowEdge;
		set {
			_windowEdge = value;
			MouseDefaultCursorShape = IsDraggable || !IsResizable
				? CursorShape.Arrow
				: _windowEdge switch {
					DisplayServer.WindowResizeEdge.Left => CursorShape.Hsize,
					DisplayServer.WindowResizeEdge.Right => CursorShape.Hsize,
					DisplayServer.WindowResizeEdge.Top => CursorShape.Vsize,
					DisplayServer.WindowResizeEdge.Bottom => CursorShape.Vsize,
					DisplayServer.WindowResizeEdge.TopLeft => CursorShape.Fdiagsize,
					DisplayServer.WindowResizeEdge.TopRight => CursorShape.Bdiagsize,
					DisplayServer.WindowResizeEdge.BottomLeft => CursorShape.Bdiagsize,
					DisplayServer.WindowResizeEdge.BottomRight => CursorShape.Fdiagsize,
					_ => CursorShape.Arrow
				};
		}
	}

	public override void _EnterTree() {
		_window = GetWindow();
		OnResized();
	}

	public override void _Ready() { Resized += OnResized; }

	public override void _ExitTree() {
		_dragDelayCts?.Cancel();
		_dragDelayCts?.Dispose();
	}

	private void OnResized() {
		if (IsResizable) {
			Visible = _window?.Mode != Godot.Window.ModeEnum.Windowed;
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
			_window?.Mode = _window.Mode == Godot.Window.ModeEnum.Windowed
				? Godot.Window.ModeEnum.Maximized
				: Godot.Window.ModeEnum.Windowed;
		} else {
			_pressedLastTime = Stopwatch.GetTimestamp();
		}
	}

	private void HandleResize(InputEvent @event) {
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || !IsResizable) {
			return;
		}

		_window?.StartResize(WindowEdge);
		using var mb = new InputEventMouseButton();
		mb.ButtonIndex = MouseButton.Left;
		mb.Pressed = false;
		Input.ParseInputEvent(mb);
	}

	private void HandleDrag(InputEvent @event) {
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) {
			_pressed = true;
			StartDelayedDragTask();
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) {
			CancelDragDelay();
		}

		if (!_pressed || @event is not InputEventMouseMotion) {
			return;
		}

		CancelDragDelay();
		_window?.StartDrag();
		using var mb = new InputEventMouseButton();
		mb.ButtonIndex = MouseButton.Left;
		mb.Pressed = false;
		Input.ParseInputEvent(mb);
	}

	private async void StartDelayedDragTask() {
		if (_dragDelayCts != null) {
			await _dragDelayCts.CancelAsync();
			_dragDelayCts.Dispose();
			_dragDelayCts = null;
		}

		_dragDelayCts = new();
		try {
			await Task.Delay(TimeSpan.FromSeconds(0.5), _dragDelayCts.Token);
			StartDragAfterDelay();
		} catch (OperationCanceledException) { }
	}

	private void StartDragAfterDelay() {
		if (!_pressed) {
			return;
		}

		_pressed = false;
		_window?.StartDrag();
		using var mb = new InputEventMouseButton();
		mb.ButtonIndex = MouseButton.Left;
		mb.Pressed = false;
		Input.ParseInputEvent(mb);
	}

	private void CancelDragDelay() {
		_dragDelayCts?.Cancel();
		_dragDelayCts?.Dispose();
		_dragDelayCts = null;
		_pressed = false;
	}
}
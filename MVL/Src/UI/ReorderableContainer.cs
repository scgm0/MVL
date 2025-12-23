using System;
using System.Collections.Generic;
using Godot;
using MVL.Utils;

namespace MVL.UI;

[Tool]
[GlobalClass]
public partial class ReorderableContainer : Container {
	[Export]
	public float HoldDuration { get; set; } = 0.5f;

	[Export]
	public float Speed { get; set; } = 10.0f;

	[Export]
	public int Separation {
		get;
		set {
			if (field == value || value < 0) {
				return;
			}

			field = value;
			_layoutDirty = true;
			QueueSort();
		}
	} = 10;

	[Export]
	public bool IsVertical {
		get;
		set {
			if (field == value) {
				return;
			}

			field = value;
			if (field) {
				CustomMinimumSize = CustomMinimumSize with { X = 0 };
			} else {
				CustomMinimumSize = CustomMinimumSize with { Y = 0 };
			}

			_layoutDirty = true;
			QueueSort();
		}
	}

	[Export]
	public ScrollContainer? ScrollContainer { get; set; }

	[Export]
	public float AutoScrollSpeed { get; set; } = 10.0f;

	[Export]
	public float AutoScrollRange { get; set; } = 0.3f;

	[Export]
	public int ScrollThreshold { get; set; } = 30;

	[Export]
	public bool IsDebugging { get; set; }

	private float _scrollStartingPoint;
	private bool _isSmoothScroll;

	private readonly List<Rect2> _dropZones = [];
	private readonly List<Rect2> _expectChildRect = [];
	private readonly List<Control> _visibleChildrenBuffer = [];

	private int _dropZoneIndex = -1;
	private int _lastDropZoneIndex = -1;
	private bool _layoutDirty = true;

	private Control _control = new();
	private Control? _focusChild;
	private bool _isPress;
	private bool _isHold;
	private float _currentDuration;
	private bool _isUsingProcess;

	[Signal]
	public delegate void ReorderedEventHandler(int from, int to);

	private const int DropZoneExtend = 2000;

	public override void _Ready() {
		_control.Visible = false;
		_control.TopLevel = true;
		_control.MouseFilter = MouseFilterEnum.Pass;
		AddChild(_control, false, InternalMode.Back);

		if (ScrollContainer == null && GetParent() is ScrollContainer parentScrollContainer) {
			ScrollContainer = parentScrollContainer;
		}

		if (ScrollContainer?.GetScript().As<Script?>()?.GetGlobalName() == "SmoothScrollContainer") {
			_isSmoothScroll = true;
		}

		ProcessMode = ProcessModeEnum.Pausable;

		SortChildren += OnSortChildrenCallback;
		ChildEnteredTree += _OnChildTreeChanged;
		ChildExitingTree += _OnChildTreeChanged;
		Resized += _OnResized;

		_layoutDirty = true;
		OnSortChildrenCallback();
	}

	private void _OnChildTreeChanged(Node node) {
		if (node is Control control && !Tools.IsEditorHint) {
			control.MouseFilter = MouseFilterEnum.Pass;
		}

		_layoutDirty = true;
		QueueSort();
	}

	private void _OnResized() { _layoutDirty = true; }

	private void OnSortChildrenCallback() { _OnSortChildren(); }

	public override void _GuiInput(InputEvent @event) {
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent) {
			return;
		}

		var count = GetChildCount();
		for (var i = 0; i < count; i++) {
			if (GetChild(i) is not Control child) {
				continue;
			}

			if (!child.GetRect().HasPoint(GetLocalMousePosition()) || !mouseEvent.IsPressed()) {
				continue;
			}

			_focusChild = child;
			_isPress = true;
			AcceptEvent();
			return;
		}

		if (!mouseEvent.IsPressed()) {
			_isPress = false;
			_isHold = false;
		}

		@event.Dispose();
	}

	public override void _Process(double delta) {
		if (Tools.IsEditorHint || !(_isPress || _isHold || _isUsingProcess)) {
			return;
		}

		_HandleInput(delta);

		if (_currentDuration >= HoldDuration != _isHold) {
			_isHold = _currentDuration >= HoldDuration;
			if (_isHold) {
				_OnStartDragging();
			}
		}

		switch (_isHold) {
			case true: {
				_HandleDraggingChildPos(delta);
				if (ScrollContainer != null) {
					_HandleAutoScroll(delta);
				}

				break;
			}
			case false when _dropZoneIndex != -1: {
				_OnStopDragging();
				break;
			}
		}

		if (_isUsingProcess) {
			_OnSortChildren((float)delta);
		}
	}

	private void _HandleInput(double delta) {
		if (ScrollContainer != null && _isPress && !_isHold) {
			var scrollPoint = IsVertical ? ScrollContainer.ScrollVertical : ScrollContainer.ScrollHorizontal;
			if (_currentDuration == 0) {
				_scrollStartingPoint = scrollPoint;
			} else {
				_isPress = Math.Abs(scrollPoint - _scrollStartingPoint) <= ScrollThreshold;
			}
		}

		_currentDuration = (float)(_isPress ? _currentDuration + delta : 0.0f);
	}

	private void _OnStartDragging() {
		_control.Size = GetTree().Root.Size;
		_control.Visible = true;
		MouseDefaultCursorShape = CursorShape.Drag;
		_isUsingProcess = true;
		_focusChild!.ZIndex = 1;

		if (_isSmoothScroll) {
			ScrollContainer!.ProcessMode = ProcessModeEnum.Disabled;
		}

		var currentIndex = 0;
		var childCount = GetChildCount();
		for (var i = 0; i < childCount; i++) {
			var node = GetChild(i);
			if (node == _focusChild) break;
			if (node is Control { Visible: true } c && c != _control) {
				currentIndex++;
			}
		}

		_dropZoneIndex = currentIndex;

		_layoutDirty = true;
		_UpdateVisibleChildrenBuffer();
		_OnSortChildren();
	}

	private void _OnStopDragging() {
		_control.Visible = false;
		MouseDefaultCursorShape = CursorShape.Arrow;
		_focusChild!.ZIndex = 0;
		var focusChildIndex = _focusChild.GetIndex();

		MoveChild(_focusChild, _dropZoneIndex);
		EmitSignalReordered(focusChildIndex, _dropZoneIndex);

		_focusChild = null;
		_dropZoneIndex = -1;
		_lastDropZoneIndex = -1;

		_layoutDirty = true;

		if (!_isSmoothScroll) {
			return;
		}

		ScrollContainer?.Set("pos", -new Vector2(ScrollContainer.ScrollHorizontal, ScrollContainer.ScrollVertical));
		ScrollContainer?.ProcessMode = ProcessModeEnum.Inherit;
	}

	private void _HandleDraggingChildPos(double delta) {
		float targetPos;
		var mouseLocalPos = GetLocalMousePosition();

		if (IsVertical) {
			targetPos = mouseLocalPos.Y - _focusChild!.Size.Y / 2.0f;
		} else {
			targetPos = mouseLocalPos.X - _focusChild!.Size.X / 2.0f;
		}

		if (ClipContents) {
			float limitMax;
			if (IsVertical) {
				limitMax = Size.Y - _focusChild.Size.Y;
			} else {
				limitMax = Size.X - _focusChild.Size.X;
			}

			limitMax = Math.Max(0, limitMax); 

			targetPos = Mathf.Clamp(targetPos, 0, limitMax);
		}

		if (IsVertical) {
			_focusChild.Position = new(_focusChild.Position.X, 
				(float)Mathf.Lerp(_focusChild.Position.Y, targetPos, delta * Speed));
		} else {
			_focusChild.Position = new((float)Mathf.Lerp(_focusChild.Position.X, targetPos, delta * Speed), 
				_focusChild.Position.Y);
		}

		var childCenterPos = _focusChild.GetRect().GetCenter();
		
		var newDropIndex = -1;
		for (var i = 0; i < _dropZones.Count; i++) {
			if (!_dropZones[i].HasPoint(childCenterPos)) {
				continue;
			}

			newDropIndex = i;
			break;
		}

		if (newDropIndex == -1 || newDropIndex == _dropZoneIndex) {
			return;
		}

		_dropZoneIndex = newDropIndex;
		_layoutDirty = true;
	}

	private void _HandleAutoScroll(double delta) {
		var mouseGPos = GetGlobalMousePosition();
		var scrollGRect = ScrollContainer!.GetGlobalRect();
		if (IsVertical) {
			var upper = scrollGRect.Position.Y + scrollGRect.Size.Y * AutoScrollRange;
			var lower = scrollGRect.Position.Y + scrollGRect.Size.Y * (1.0 - AutoScrollRange);

			if (upper > mouseGPos.Y) {
				var factor = (upper - mouseGPos.Y) / (upper - scrollGRect.Position.Y);
				ScrollContainer.ScrollVertical -= (int)(delta * AutoScrollSpeed * 150.0 * factor);
			} else if (lower < mouseGPos.Y) {
				var factor = (mouseGPos.Y - lower) / (scrollGRect.End.Y - lower);
				ScrollContainer.ScrollVertical += (int)(delta * AutoScrollSpeed * 150.0 * factor);
			}
		} else {
			var left = scrollGRect.Position.X + scrollGRect.Size.X * AutoScrollRange;
			var right = scrollGRect.Position.X + scrollGRect.Size.X * (1.0 - AutoScrollRange);

			if (left > mouseGPos.X) {
				var factor = (left - mouseGPos.X) / (left - scrollGRect.Position.X);
				ScrollContainer.ScrollHorizontal -= (int)(delta * AutoScrollSpeed * 150.0 * factor);
			} else if (right < mouseGPos.X) {
				var factor = (mouseGPos.X - right) / (scrollGRect.End.X - right);
				ScrollContainer.ScrollHorizontal += (int)(delta * AutoScrollSpeed * 150.0 * factor);
			}
		}
	}

	private void _OnSortChildren(double delta = -1.0) {
		var isManualSort = Math.Abs(delta - -1.0) < 0.0001;

		if (isManualSort) {
			_layoutDirty = true;
		} else if (!_isUsingProcess && !isManualSort) {
			return;
		}

		_UpdateVisibleChildrenBuffer();

		if (_layoutDirty) {
			_AdjustExpectedChildRect();
			_AdjustDropZoneRect();
			_layoutDirty = false;
		}

		_AdjustChildRect(delta);
	}

	private void _UpdateVisibleChildrenBuffer() {
		_visibleChildrenBuffer.Clear();
		var count = GetChildCount();
		for (var i = 0; i < count; i++) {
			var node = GetChild(i);
			if (node is not Control child) {
				continue;
			}

			if (!child.Visible) {
				continue;
			}

			if (node == _focusChild && _isHold) {
				continue;
			}

			if (child == _control) {
				continue;
			}

			_visibleChildrenBuffer.Add(child);
		}
	}

	private void _AdjustExpectedChildRect() {
		_expectChildRect.Clear();

		var endPoint = 0.0f;

		if (_expectChildRect.Capacity < _visibleChildrenBuffer.Count + 1) {
			_expectChildRect.Capacity = _visibleChildrenBuffer.Count + 1;
		}

		for (var i = 0; i < _visibleChildrenBuffer.Count; i++) {
			var child = _visibleChildrenBuffer[i];

			var minSize = child.GetCombinedMinimumSize();

			if (IsVertical) {
				if (i == _dropZoneIndex) {
					endPoint += _focusChild!.Size.Y + Separation;
				}

				_expectChildRect.Add(new(new(0, endPoint), new(Size.X, minSize.Y)));
				endPoint += minSize.Y + Separation;
			} else {
				if (i == _dropZoneIndex) {
					endPoint += _focusChild!.Size.X + Separation;
				}

				_expectChildRect.Add(new(new(endPoint, 0), new(minSize.X, Size.Y)));
				endPoint += minSize.X + Separation;
			}
		}
	}

	private void _AdjustChildRect(double delta = -1.0f) {
		if (_visibleChildrenBuffer.Count > 0) {
			if (_expectChildRect.Count != _visibleChildrenBuffer.Count) {
				_layoutDirty = true;
				return;
			}

			var isAnimating = false;
			var fDelta = (float)delta;
			var useProcess = _isUsingProcess && fDelta > 0;

			for (var i = 0; i < _visibleChildrenBuffer.Count; i++) {
				var child = _visibleChildrenBuffer[i];
				var targetRect = _expectChildRect[i];

				if (child.Position == targetRect.Position && child.Size == targetRect.Size) {
					continue;
				}

				if (useProcess) {
					isAnimating = true;
					child.Position = child.Position.Lerp(targetRect.Position, fDelta * Speed);
					child.Size = targetRect.Size;
					if (child.Position.DistanceSquaredTo(targetRect.Position) <= 1.0f) {
						child.Position = targetRect.Position;
					}
				} else {
					child.Position = targetRect.Position;
					child.Size = targetRect.Size;
				}
			}

			if (!isAnimating && _focusChild == null) {
				_isUsingProcess = false;
			}
		}

		float contentSize;
		float lastEnd = 0;

		if (_visibleChildrenBuffer.Count > 0) {
			lastEnd = IsVertical ? _expectChildRect[^1].End.Y : _expectChildRect[^1].End.X;
		}

		if (IsVertical) {
			if (_isUsingProcess && _dropZoneIndex == _visibleChildrenBuffer.Count) {
				float sep = _visibleChildrenBuffer.Count > 0 ? Separation : 0;
				contentSize = lastEnd + sep + _focusChild!.Size.Y;
			} else {
				contentSize = lastEnd;
			}

			CustomMinimumSize = new(CustomMinimumSize.X, contentSize);
		} else {
			if (_isUsingProcess && _dropZoneIndex == _visibleChildrenBuffer.Count) {
				float sep = _visibleChildrenBuffer.Count > 0 ? Separation : 0;
				contentSize = lastEnd + sep + _focusChild!.Size.X;
			} else {
				contentSize = lastEnd;
			}

			CustomMinimumSize = new(contentSize, CustomMinimumSize.Y);
		}
	}

	private void _AdjustDropZoneRect() {
		_dropZones.Clear();

		if (_visibleChildrenBuffer.Count == 0 || _expectChildRect.Count != _visibleChildrenBuffer.Count) {
			if (_visibleChildrenBuffer.Count == 0) {
				_dropZones.Add(new(Vector2.Zero, Size));
			}

			return;
		}

		for (var i = 0; i < _visibleChildrenBuffer.Count; i++) {
			var targetRect = _expectChildRect[i];
			var targetCenter = targetRect.GetCenter();

			Rect2 dropZoneRect = new();

			if (IsVertical) {
				if (i == 0) {
					dropZoneRect.Position = new(targetRect.Position.X, targetRect.Position.Y - DropZoneExtend);
				} else {
					var prevRect = _expectChildRect[i - 1];
					var prevCenter = prevRect.GetCenter();

					dropZoneRect.Position = new(prevRect.Position.X, prevCenter.Y);
				}

				dropZoneRect.End = new(targetRect.Size.X, targetCenter.Y);
				_dropZones.Add(dropZoneRect);

				if (i != _visibleChildrenBuffer.Count - 1) {
					continue;
				}

				dropZoneRect = new() {
					Position = new(targetRect.Position.X, targetCenter.Y),
					End = new(targetRect.Size.X, targetRect.End.Y + DropZoneExtend)
				};
			} else {
				if (i == 0) {
					dropZoneRect.Position = new(targetRect.Position.X - DropZoneExtend, targetRect.Position.Y);
				} else {
					var prevRect = _expectChildRect[i - 1];
					var prevCenter = prevRect.GetCenter();

					dropZoneRect.Position = new(prevCenter.X, prevRect.Position.Y);
				}

				dropZoneRect.End = new(targetCenter.X, targetRect.Size.Y);
				_dropZones.Add(dropZoneRect);

				if (i != _visibleChildrenBuffer.Count - 1) {
					continue;
				}

				dropZoneRect = new() {
					Position = new(targetCenter.X, targetRect.Position.Y),
					End = new(targetRect.End.X + DropZoneExtend, targetRect.Size.Y)
				};
			}

			_dropZones.Add(dropZoneRect);
		}
	}
}
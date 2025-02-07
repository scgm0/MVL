using System;
using System.Collections.Generic;
using Godot;

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
			if (value == field || value < 0) {
				return;
			}

			field = value;
			_OnSortChildren();
		}
	} = 10;

	[Export]
	public bool IsVertical {
		get;
		set {
			if (value == field) {
				return;
			}

			field = value;

			if (value) {
				CustomMinimumSize = CustomMinimumSize with { X = 0 };
			} else {
				CustomMinimumSize = CustomMinimumSize with { Y = 0 };
			}

			_OnSortChildren();
		}
	} = false;

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
	private int _dropZoneIndex = -1;
	private readonly List<Rect2> _expectChildRect = [];

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

		if (ScrollContainer != null && ScrollContainer.HasMethod("handle_overdrag")) {
			_isSmoothScroll = true;
		}

		ProcessMode = ProcessModeEnum.Pausable;
		_AdjustExpectedChildRect();
		SortChildren += () => _OnSortChildren();
		ChildEnteredTree += _OnChildEnteredTree;
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent) return;
		foreach (var node in GetChildren()) {
			if (node is not Control child) continue;
			if (child.GetRect().HasPoint(GetLocalMousePosition()) && mouseEvent.IsPressed()) {
				_focusChild = child;
				_isPress = true;
				AcceptEvent();
			} else if (!mouseEvent.IsPressed()) {
				_isPress = false;
				_isHold = false;
			}
		}
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint() || !(_isPress || _isHold || _isUsingProcess)) return;

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
			case false when _dropZoneIndex != -1: _OnStopDragging(); break;
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
		_control.Size = Main.SceneTree.Root.Size;
		_control.Visible = true;
		MouseDefaultCursorShape = CursorShape.Drag;
		_isUsingProcess = true;
		_focusChild!.ZIndex = 1;
		if (_isSmoothScroll) {
			ScrollContainer!.ProcessMode = ProcessModeEnum.Disabled;
		}
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
		if (!_isSmoothScroll) return;
		ScrollContainer!.Set("pos", -new Vector2(ScrollContainer.ScrollHorizontal, ScrollContainer.ScrollVertical));
		ScrollContainer.ProcessMode = ProcessModeEnum.Inherit;
	}

	private void _OnChildEnteredTree(Node node) {
		if (node is Control control && !Engine.IsEditorHint()) {
			control.MouseFilter = MouseFilterEnum.Pass;
		}
	}

	private void _HandleDraggingChildPos(double delta) {
		float targetPos;
		if (IsVertical) {
			targetPos = GetLocalMousePosition().Y - _focusChild!.Size.Y / 2.0f;
			_focusChild.Position =
				new(_focusChild.Position.X, (float)Mathf.Lerp(_focusChild.Position.Y, targetPos, delta * Speed));
		} else {
			targetPos = GetLocalMousePosition().X - _focusChild!.Size.X / 2.0f;
			_focusChild.Position =
				new((float)Mathf.Lerp(_focusChild.Position.X, targetPos, delta * Speed), _focusChild.Position.Y);
		}

		var childCenterPos = _focusChild.GetRect().GetCenter();
		for (var i = 0; i < _dropZones.Count; i++) {
			var dropZone = _dropZones[i];
			if (dropZone.HasPoint(childCenterPos)) {
				_dropZoneIndex = i;
				break;
			}

			if (i == _dropZones.Count - 1) {
				_dropZoneIndex = -1;
			}
		}
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
			} else {
				ScrollContainer.ScrollVertical = ScrollContainer.ScrollVertical;
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
			} else {
				ScrollContainer.ScrollHorizontal = ScrollContainer.ScrollHorizontal;
			}
		}
	}

	private void _OnSortChildren(double delta = -1.0) {
		if (_isUsingProcess && Math.Abs(delta - -1.0) < 0.0001) {
			return;
		}

		_AdjustExpectedChildRect();
		_AdjustChildRect(delta);
		_AdjustDropZoneRect();
	}

	private void _AdjustExpectedChildRect() {
		_expectChildRect.Clear();
		var children = _GetVisibleChildren();
		var endPoint = 0.0f;
		for (var i = 0; i < children.Count; i++) {
			var node = children[i];
			if (node is not Control child) continue;
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
		var children = _GetVisibleChildren();
		if (children.Count == 0) {
			return;
		}

		var isAnimating = false;
		for (var i = 0; i < children.Count; i++) {
			var node = children[i];
			if (node is not Control child) continue;
			if (child.Position == _expectChildRect[i].Position && child.Size == _expectChildRect[i].Size) {
				continue;
			}

			if (_isUsingProcess) {
				isAnimating = true;
				child.Position = child.Position.Lerp(_expectChildRect[i].Position, (float)(delta * Speed));
				child.Size = _expectChildRect[i].Size;
				if ((child.Position - _expectChildRect[i].Position).Length() <= 1.0) {
					child.Position = _expectChildRect[i].Position;
				}
			} else {
				child.Position = _expectChildRect[i].Position;
				child.Size = _expectChildRect[i].Size;
			}
		}

		var lastNode = children[^1];
		if (lastNode is Control lastChild) {
			if (IsVertical) {
				if (_isUsingProcess && _dropZoneIndex == children.Count) {
					CustomMinimumSize = new(CustomMinimumSize.X,
						_expectChildRect[^1].End.Y + _focusChild!.Size.Y + Separation);
				} else {
					CustomMinimumSize = new(CustomMinimumSize.X, lastChild.GetRect().End.Y);
				}
			} else {
				if (_isUsingProcess && _dropZoneIndex == children.Count) {
					CustomMinimumSize =
						new(_expectChildRect[^1].End.X + _focusChild!.Size.X + Separation,
							CustomMinimumSize.Y);
				} else {
					CustomMinimumSize = new(lastChild.GetRect().End.X, CustomMinimumSize.Y);
				}
			}
		}

		if (!isAnimating && _focusChild == null) {
			_isUsingProcess = false;
		}
	}

	private void _AdjustDropZoneRect() {
		_dropZones.Clear();
		var children = _GetVisibleChildren();
		for (var i = 0; i < children.Count; i++) {
			var node = children[i];
			if (node is not Control child) continue;
			Rect2 dropZoneRect = new();
			if (IsVertical) {
				if (i == 0) {
					dropZoneRect.Position = new(child.Position.X, child.Position.Y - DropZoneExtend);
					dropZoneRect.End = new(child.Size.X, child.GetRect().GetCenter().Y);
					_dropZones.Add(dropZoneRect);
				} else {
					if (children[i - 1] is Control prevChild) {
						dropZoneRect.Position = new(prevChild.Position.X,
							prevChild.GetRect().GetCenter().Y);
						dropZoneRect.End = new(child.Size.X, child.GetRect().GetCenter().Y);
						_dropZones.Add(dropZoneRect);
					}
				}

				if (i != children.Count - 1) continue;
				dropZoneRect.Position = new(child.Position.X, child.GetRect().GetCenter().Y);
				dropZoneRect.End = new(child.Size.X, child.GetRect().End.Y + DropZoneExtend);
			} else {
				if (i == 0) {
					dropZoneRect.Position = new(child.Position.X - DropZoneExtend, child.Position.Y);
					dropZoneRect.End = new(child.GetRect().GetCenter().X, child.Size.Y);
					_dropZones.Add(dropZoneRect);
				} else {
					if (children[i - 1] is Control prevChild) {
						dropZoneRect.Position = new(prevChild.GetRect().GetCenter().X,
							prevChild.Position.Y);
						dropZoneRect.End = new(child.GetRect().GetCenter().X, child.Size.Y);
						_dropZones.Add(dropZoneRect);
					}
				}

				if (i != children.Count - 1) continue;
				dropZoneRect.Position = new(child.GetRect().GetCenter().X, child.Position.Y);
				dropZoneRect.End = new(child.GetRect().End.X + DropZoneExtend, child.Size.Y);
			}

			_dropZones.Add(dropZoneRect);
		}
	}

	private List<Node> _GetVisibleChildren() {
		var visibleControls = new List<Node>();
		foreach (var node in GetChildren()) {
			if (node is not Control child) continue;

			if (!child.Visible) {
				continue;
			}

			if (node == _focusChild && _isHold) {
				continue;
			}

			if (child == _control) {
				continue;
			}

			visibleControls.Add(child);
		}

		return visibleControls;
	}
}
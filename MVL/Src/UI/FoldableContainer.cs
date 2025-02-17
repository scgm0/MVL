using Godot;
using MVL.Utils.Help;

namespace MVL.UI;

public partial class FoldableContainer : PanelContainer {
	[Export]
	private Button? _foldButton;

	[Export]
	private Container? _contentContainer;

	[Export]
	private Texture2D? _unfoldIcon;

	[Export]
	private Texture2D? _foldIcon;

	public bool IsFolded {
		get;
		set {
			field = value;
			if (_foldButton != null) {
				_foldButton.ButtonPressed = value;
				_foldButton.Icon = value ? _foldIcon : _unfoldIcon;
			}

			if (_contentContainer != null) {
				_contentContainer.Visible = value;
			}
		}
	} = false;

	public string Title {
		get;
		set {
			field = value;
			if (_foldButton != null) {
				_foldButton.Text = value;
			}
		}
	} = "";

	public override void _Ready() {
		_foldIcon.NotNull();
		_unfoldIcon.NotNull();
		_foldButton.NotNull();
		_contentContainer.NotNull();
		_foldButton.Text = Title;
		_foldButton.ButtonPressed = IsFolded;
		_foldButton.Icon = IsFolded ? _foldIcon : _unfoldIcon;
		_contentContainer.Visible = IsFolded;
		_foldButton.Pressed += FoldButtonOnPressed;
	}

	private void FoldButtonOnPressed() { IsFolded = !IsFolded; }

	public void AddChild(Control child) { _contentContainer?.GetNode<VBoxContainer>("VBoxContainer")?.AddChild(child); }
}
using Godot;

namespace MVL.UI.Page;

[GlobalClass]
public partial class MenuPage : Control {
	[Export]
	public Control? Main { get; set; }

	public MenuPage() {
		VisibilityChanged += OnVisibilityChanged;
	}

	private void OnVisibilityChanged() {
		if (!Visible) {
			return;
		}

		ShowAnimation();
	}

	public virtual async void ShowAnimation() {
		if (Main is null) {
			return;
		}

		var tween = Main.CreateTween();
		tween.TweenProperty(Main, new(CanvasItem.PropertyName.Modulate), Colors.White, 0.4).From(Colors.Transparent).Dispose();
		tween.Parallel().TweenProperty(Main, new(Control.PropertyName.Scale), Vector2.One, 0.2)
			.From(new Vector2(0.95f, 0.95f)).Dispose();
	}
}
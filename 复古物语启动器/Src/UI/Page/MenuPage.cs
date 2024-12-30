using Godot;

namespace 复古物语启动器.UI.Page;

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

		var min = Main.GetCombinedMinimumSize();
		while (Main.Size.X.Equals(min.X) || Main.Size.Y.Equals(min.Y)) {
			await ToSignal(Main, Control.SignalName.Resized);
		}

		var tween = Main.CreateTween();
		Main.PivotOffset = Main.Size / 2;
		tween.TweenProperty(Main, new(CanvasItem.PropertyName.Modulate), Colors.White, 0.4).From(Colors.Transparent).Dispose();
		tween.Parallel().TweenProperty(Main, new(Control.PropertyName.Scale), Vector2.One, 0.2)
			.From(new Vector2(0.95f, 0.95f)).Dispose();
	}
}
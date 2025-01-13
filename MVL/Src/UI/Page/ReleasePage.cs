using System.Linq;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class ReleasePage : MenuPage {

	[Export]
	private Button? _addReleaseButton;

	[Export]
	private PackedScene? _releaseItemScene;

	[Export]
	private Container? _grid;

	public override void _Ready() {
		_addReleaseButton.NotNull();
		_releaseItemScene.NotNull();
		_grid.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_addReleaseButton.Pressed += AddReleaseButtonOnPressed;
	}

	private void AddReleaseButtonOnPressed() { }

	private void OnVisibilityChanged() {
		if (!Visible) return;
		foreach (var child in _grid!.GetChildren()) {
			if (child == _addReleaseButton) {
				continue;
			}

			child.QueueFree();
		}

		UI.Main.CheckGameVersion();
		var list = UI.Main.Release.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);
		foreach (var info in list) {
			var item = _releaseItemScene!.Instantiate<ReleaseItem>();
			item.ReleasePath = info.Path;
			item.ReleaseName = "复古物语";
			item.ReleaseVersion = info.Version;
			_grid!.AddChild(item);
		}
	}
}
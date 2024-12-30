using Godot;
using 复古物语启动器.Utils.Help;

namespace 复古物语启动器.UI.Page;

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

	private void AddReleaseButtonOnPressed() {
		
	}

	private void OnVisibilityChanged() {
		if (!Visible) return;
		foreach (var child in _grid!.GetChildren()) {
			if (child == _addReleaseButton) {
				continue;
			}

			child.QueueFree();
		}

		UI.Main.CheckGameVersion();
		foreach (var (path, version) in UI.Main.GameVersions) {
			var item = _releaseItemScene!.Instantiate<Item.ReleaseItem>();
			item.ReleasePath = path;
			item.ReleaseName = "复古物语";
			item.ReleaseVersion = version;
			_grid!.AddChild(item);
		}
	}
}
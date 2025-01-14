using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Config;
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

	private InstalledGamesImport? _installedGamesImport;

	public override void _Ready() {
		_addReleaseButton.NotNull();
		_releaseItemScene.NotNull();
		_grid.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_addReleaseButton.Pressed += AddReleaseButtonOnPressed;
		UI.Main.SceneTree.Root.FilesDropped += ImportGame;
	}

	private async void ImportGame(string[] files) {
		var list = new List<string>();
		foreach (var path in files) {
			if (DirAccess.DirExistsAbsolute(path)) {
				list.Add(path);
			} else if (FileAccess.FileExists(path)) { }
		}

		if (list.Count <= 0) return;
		if (_installedGamesImport is null) {
			_installedGamesImport = await UI.Main.Instance!.ImportInstalledGames(list);
		} else {
			await _installedGamesImport.ShowInstalledGames(list);
		}
		
		_installedGamesImport.Import -= UpdateList;
		_installedGamesImport.Import += UpdateList;
	}

	private void UpdateList(string[] gamePaths) {
		_installedGamesImport!.Import -= UpdateList;
		if (gamePaths.Length <= 0) return;
		UpdateList();
	}

	private void AddReleaseButtonOnPressed() { UpdateList(); }

	private void OnVisibilityChanged() {
		if (!Visible) return;
		UpdateList();
	}

	public void UpdateList() {
		foreach (var child in _grid!.GetChildren()) {
			if (child == _addReleaseButton) {
				continue;
			}

			child.QueueFree();
		}

		UI.Main.CheckGameVersion();
		var list = UI.Main.Release.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);

		var i = 1;
		foreach (var info in list) {
			var item = _releaseItemScene!.Instantiate<ReleaseItem>();
			item.ReleasePath = info.Path;
			item.ReleaseName = "复古物语";
			item.ReleaseVersion = info.Version;
			item.Modulate = item.Modulate with { A = 0 };
			_grid!.AddChild(item);
			var tween = item.CreateTween();
			tween.TweenProperty(item, "modulate:a", 1, 0.4f).SetDelay(i * 0.1);
			i++;
		}
	}
}
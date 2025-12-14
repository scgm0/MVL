using System.Collections.Generic;
using System.Linq;
using Godot;
using MVL.UI.Item;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class ReleasePage : MenuPage {
	[Export]
	private PackedScene? _gameDownloadScene;

	[Export]
	private Button? _addReleaseButton;

	[Export]
	private PackedScene? _releaseItemScene;

	[Export]
	private Container? _grid;

	private InstalledGamesImport? _installedGamesImport;

	public override void _Ready() {
		_gameDownloadScene.NotNull();
		_addReleaseButton.NotNull();
		_releaseItemScene.NotNull();
		_grid.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_addReleaseButton.Pressed += AddReleaseButtonOnPressed;
		Tools.SceneTree.Root.FilesDropped += ImportGame;
	}

	private void ImportGame(string gamePath) { ImportGame([gamePath]); }

	private async void ImportGame(string[] files) {
		var list = new List<string>();
		foreach (var path in files) {
			if (DirAccess.DirExistsAbsolute(path)) {
				list.Add(path);
			} else if (FileAccess.FileExists(path)) { }
		}

		if (list.Count <= 0) {
			return;
		}

		_installedGamesImport ??= UI.Main.Instance!.InstantiateInstalledGamesImport();
		await _installedGamesImport.ShowInstalledGames(list);

		_installedGamesImport.Import -= UpdateList;
		_installedGamesImport.Import += UpdateList;
	}

	private void UpdateList(string[] gamePaths) {
		_installedGamesImport!.Import -= UpdateList;
		if (gamePaths.Length <= 0) {
			return;
		}

		UpdateList();
	}

	private async void AddReleaseButtonOnPressed() {
		var downloadWindow = _gameDownloadScene!.Instantiate<GameDownloadWindow>();
		UI.Main.Instance?.AddChild(downloadWindow);
		await downloadWindow.Show();
		downloadWindow.UpdateDownloadList("https://api.vintagestory.at/stable-unstable.json");
		downloadWindow.InstallGame += ImportGame;
		downloadWindow.Hidden += downloadWindow.QueueFree;
	}

	private void OnVisibilityChanged() {
		if (!Visible) {
			foreach (var child in _grid!.GetChildren()) {
				if (child == _addReleaseButton) {
					continue;
				}

				child.QueueFree();
			}

			return;
		}

		UpdateList();
	}

	public void UpdateList() {
		UI.Main.CheckReleaseInfo();
		var list = UI.Main.ReleaseInfos.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);

		var i = 1;
		foreach (var info in list) {
			var item = _releaseItemScene!.Instantiate<ReleaseItem>();
			item.ReleaseInfo = info;
			item.Modulate = item.Modulate with { A = 0 };
			_grid!.AddChild(item);
			using var tween = item.CreateTween();
			tween.TweenProperty(item, "modulate:a", 1, 0.4f).SetDelay(i * 0.1);
			i++;
		}
	}
}
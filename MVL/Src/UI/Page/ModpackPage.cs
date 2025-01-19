using System.Linq;
using Godot;
using MVL.UI.Item;
using MVL.UI.Window;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class ModpackPage : MenuPage {
	[Export]
	private Button? _addModpackButton;

	[Export]
	private PackedScene? _modpackItemScene;

	[Export]
	private PackedScene? _modpackAddScene;

	[Export]
	private Container? _list;

	public override void _Ready() {
		_addModpackButton.NotNull();
		_modpackItemScene.NotNull();
		_modpackAddScene.NotNull();
		_list.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_addModpackButton.Pressed += AddModpackButtonOnPressed;
	}

	private void AddModpackButtonOnPressed() {
		var addModpack = _modpackAddScene!.Instantiate<AddModpackWindow>();
		addModpack.AddModpack += (path, version) => {
			var gameVersion = new GameVersion(version);
			var info = UI.Main.ReleaseInfos.Values.First(i => i.Version == gameVersion);
			var modpack = new ModpackConfig {
				Path = path,
				Name = path.GetFile(),
				Version = gameVersion,
				ReleasePath = info.Path
			};
			ModpackConfig.Save(modpack);
			UI.Main.ModpackConfigs.Add(path, modpack);
			UI.Main.BaseConfig.Modpack.Insert(0, path);
			UpdateList();
		};
		addModpack.Hidden += addModpack.QueueFree;
		UI.Main.Instance?.AddChild(addModpack);
		_ = addModpack.Show();
	}

	private void OnVisibilityChanged() {
		if (!Visible) return;
		UpdateList();
	}

	public void UpdateList() {
		foreach (var child in _list!.GetChildren()) {
			if (child == _addModpackButton) {
				continue;
			}

			child.QueueFree();
		}

		UI.Main.CheckModpackConfig();
		var i = 1;
		foreach (var path in UI.Main.BaseConfig.Modpack) {
			var modpack = UI.Main.ModpackConfigs[path];
			var item = _modpackItemScene!.Instantiate<ModpackItem>();
			item.ModpackConfig = modpack;
			item.Modulate = item.Modulate with { A = 0 };
			_list!.AddChild(item);
			var tween = item.CreateTween();
			tween.TweenProperty(item, "modulate:a", 1, 0.4f).SetDelay(i * 0.1);
			i++;
		}
	}
}
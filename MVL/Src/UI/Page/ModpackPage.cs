using System.Threading.Tasks;
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
	private ReorderableContainer? _list;

	public override void _Ready() {
		_addModpackButton.NotNull();
		_modpackItemScene.NotNull();
		_modpackAddScene.NotNull();
		_list.NotNull();
		VisibilityChanged += OnVisibilityChanged;
		_addModpackButton.Pressed += AddModpackButtonOnPressed;
		_list.Reordered += ListOnReordered;
	}

	private void ListOnReordered(int from, int to) {
		var fromPath = UI.Main.BaseConfig.Modpack[from];
		UI.Main.BaseConfig.Modpack.RemoveAt(from);
		UI.Main.BaseConfig.Modpack.Insert(to, fromPath);
		BaseConfig.Save(UI.Main.BaseConfig);
	}

	private void AddModpackButtonOnPressed() {
		var addModpack = _modpackAddScene!.Instantiate<AddModpackWindow>();
		addModpack.AddModpack += AddModpackOnAddModpack;
		addModpack.Hidden += addModpack.QueueFree;
		UI.Main.Instance?.AddChild(addModpack);
		_ = addModpack.Show();
	}

	private void AddModpackOnAddModpack(string modpackName, string modpackPath, string gameVersion, string releasePath) {
		var modpack = new ModpackConfig {
			Path = modpackPath,
			Name = modpackName,
			Version = new GameVersion(gameVersion),
			ReleasePath = releasePath
		};
		ModpackConfig.Save(modpack);
		UI.Main.ModpackConfigs[modpackPath] = modpack;
		UI.Main.BaseConfig.Modpack.Insert(0, modpackPath);
		UpdateList();
	}

	private void OnVisibilityChanged() {
		if (!Visible) return;
		UpdateList();
	}

	public async void UpdateList() {
		foreach (var child in _list!.GetChildren()) {
			if (child == _addModpackButton) {
				continue;
			}

			child.QueueFree();
		}

		await Task.Run(UI.Main.CheckModpackConfig);
		var i = 1;
		foreach (var path in UI.Main.BaseConfig.Modpack) {
			if (!Visible) {
				break;
			}

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
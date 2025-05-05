using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackModManagementWindow : BaseWindow {
	[Export]
	private PackedScene? _modInfoItemScene;

	[Export]
	private VBoxContainer? _modInfoItemsContainer;

	[Export]
	private Control? _loadingContainer;

	public ModpackItem? ModpackItem { get; set; }

	public override void _Ready() {
		base._Ready();
		_modInfoItemScene.NotNull();
		_modInfoItemsContainer.NotNull();
		_loadingContainer.NotNull();
		ModpackItem.NotNull();
		CancelButton!.Pressed += CancelButtonOnPressed;
	}

	public async void ShowList() {
		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		if (ModpackItem!.ModpackConfig?.Mods == null) return;
		_loadingContainer!.Show();

		var list = ModpackItem.ModpackConfig.Mods.Values.OrderBy(m => m.ModId);
		List<Task> tasks = [];
		foreach (var modpackConfigMod in list) {
			if (!Visible) {
				break;
			}

			var modInfoItem = _modInfoItemScene!.Instantiate<ModInfoItem>();
			modInfoItem.Window = this;
			modInfoItem.Mod = modpackConfigMod;
			modInfoItem.Modulate = Colors.Transparent;
			tasks.Add(modInfoItem.UpdateApiModInfo());
			_modInfoItemsContainer!.AddChild(modInfoItem);
			var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}

		await Task.WhenAll(tasks);
		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}
}
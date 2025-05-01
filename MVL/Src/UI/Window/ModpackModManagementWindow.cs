using Godot;
using System;
using System.Linq;
using MVL.UI.Item;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackModManagementWindow : BaseWindow {
	[Export]
	private PackedScene? _modInfoItemScene;

	[Export]
	private VBoxContainer? _modInfoItemsContainer;

	public ModpackItem? ModpackItem { get; set; }

	public override void _Ready() {
		base._Ready();
		_modInfoItemScene.NotNull();
		_modInfoItemsContainer.NotNull();
		ModpackItem.NotNull();
		CancelButton!.Pressed += CancelButtonOnPressed;
	}

	public async void ShowList() {
		if (ModpackItem!.ModpackConfig?.Mods == null) return;

		var list = ModpackItem.ModpackConfig.Mods.Values.OrderBy(m => m.Name);
		foreach (var modpackConfigMod in list) {
			if (!Visible) {
				break;
			}

			var modInfoItem = _modInfoItemScene!.Instantiate<ModInfoItem>();
			modInfoItem.Mod = modpackConfigMod;
			modInfoItem.Modulate = Colors.Transparent;
			_modInfoItemsContainer!.AddChild(modInfoItem);
			var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 0.5, 0.025f);
			await ToSignal(tween, Tween.SignalName.Finished);
			tween.Stop();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
			tween.Play();
		}
	}
}
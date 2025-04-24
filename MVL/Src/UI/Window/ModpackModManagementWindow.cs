using Godot;
using System;
using MVL.UI.Item;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackModManagementWindow : BaseWindow
{
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

		if (ModpackItem.ModpackConfig?.Mods == null) return;
		foreach (var modpackConfigMod in ModpackItem.ModpackConfig.Mods) {
			var modInfoItem = _modInfoItemScene.Instantiate<ModInfoItem>();
			modInfoItem.Mod = modpackConfigMod.Value;
			_modInfoItemsContainer.AddChild(modInfoItem);
		}
	}
}

using Godot;
using MVL.UI.Item;

namespace MVL.UI.Window;

public partial class ApiModReleasesWindow : BaseWindow {
	[Export]
	private PackedScene? _apiModReleaseItemScene;

	[Export]
	private VBoxContainer? _apiModReleaseItemsContainer;

	public ModInfoItem? ModInfoItem { get; set; }

	public override void _Ready() {
		base._Ready();

		if (ModInfoItem?.ApiModInfo is not null) {
			UpdateApiModInfo();
		}

		CancelButton!.Pressed += CancelButtonOnPressed;
	}

	public void UpdateApiModInfo() {
		foreach (var child in _apiModReleaseItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		foreach (var apiModRelease in ModInfoItem!.ApiModInfo!.Releases) {
			var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
			apiModReleaseItem.Window = this;
			apiModReleaseItem.ModInfo = ModInfoItem.Mod;
			apiModReleaseItem.ApiModRelease = apiModRelease;
			_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);
		}
	}

	public void UpdateApiModInfo(ApiModReleaseItem apiModReleaseItem) {
		if (ModInfoItem is not null) {
			ModInfoItem.Mod = apiModReleaseItem.ModInfo;
			_ = ModInfoItem.UpdateApiModInfo();
		}

		UpdateApiModInfo();
	}
}
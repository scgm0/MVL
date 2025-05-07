using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;

namespace MVL.UI.Window;

public partial class ApiModReleasesWindow : BaseWindow {
	[Export]
	private PackedScene? _apiModReleaseItemScene;

	[Export]
	private VBoxContainer? _apiModReleaseItemsContainer;

	public ModInfoItem? ModInfoItem { get; set; }

	public IEnumerable<ModInfoItem>? AutoUpdateModInfoItems { get; set; }

	public override void _Ready() {
		base._Ready();

		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;
		UpdateApiModInfo();
	}

	private async void OkButtonOnPressed() {
		OkButton!.Disabled = true;
		var tasks = new List<Task>();
		foreach (var child in _apiModReleaseItemsContainer!.GetChildren()) {
			if (!IsInstanceValid(this)) {
				return;
			}

			if (child is ApiModReleaseItem { IsChecked: true } apiModReleaseItem) {
				tasks.Add(apiModReleaseItem.Download());
			}
		}

		await Task.WhenAll(tasks);
		if (!IsInstanceValid(this)) {
			return;
		}

		OkButton!.Disabled = false;
		UpdateApiModInfo();
	}

	public void UpdateApiModInfo() {
		foreach (var child in _apiModReleaseItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		if (ModInfoItem != null) {
			OkButton?.Hide();
			foreach (var apiModRelease in ModInfoItem.ApiModInfo!.Releases) {
				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = ModInfoItem.Mod;
				apiModReleaseItem.ApiModRelease = apiModRelease;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);
			}
		}

		if (AutoUpdateModInfoItems != null) {
			OkButton?.Show();
			foreach (var modInfoItem in AutoUpdateModInfoItems) {
				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = modInfoItem.Mod;
				apiModReleaseItem.ApiModRelease = modInfoItem.ApiModRelease;
				apiModReleaseItem.IsChecked = true;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);
			}
		}
	}

	public async void UpdateApiModInfo(ApiModReleaseItem apiModReleaseItem) {
		if (ModInfoItem is not null) {
			ModInfoItem.Mod = apiModReleaseItem.ModInfo;
			await ModInfoItem.UpdateApiModInfo();
			UpdateApiModInfo();
		}

		if (AutoUpdateModInfoItems is not null) {
			var modInfoItem = AutoUpdateModInfoItems.First(m => m.Mod?.ModId == apiModReleaseItem.ModInfo?.ModId);
			modInfoItem.Mod = apiModReleaseItem.ModInfo;
			await modInfoItem.UpdateApiModInfo();
		}
	}
}
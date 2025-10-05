using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ApiModReleasesWindow : BaseWindow {
	[Export]
	private PackedScene? _apiModReleaseItemScene;

	[Export]
	private VBoxContainer? _apiModReleaseItemsContainer;

	[Export]
	private Control? _loadingContainer;

	public ModInfoItem? ModInfoItem { get; set; }

	public IEnumerable<ModInfoItem>? AutoUpdateModInfoItems { get; set; }

	public IEnumerable<(ApiModInfo, ApiModRelease, ModpackConfig)>? ModDependencies { get; set; }

	public (ModInfo? modInfo, ApiModInfo apiModInfo, ModpackConfig modpackConfig)? DownloadModInfo { get; set; }

	public override void _Ready() {
		base._Ready();
		_apiModReleaseItemScene.NotNull();
		_apiModReleaseItemsContainer.NotNull();
		_loadingContainer.NotNull();

		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;
		_loadingContainer.VisibilityChanged += LoadingContainerOnVisibilityChanged;

		UpdateApiModInfo();
	}

	private void LoadingContainerOnVisibilityChanged() {
		var rid = _loadingContainer!.GetCanvasItem();
		if (_loadingContainer.Visible) {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, true, Container!.GetGlobalRect());
		} else {
			RenderingServer.CanvasItemSetCopyToBackbuffer(rid, false, new());
		}
	}

	private async void OkButtonOnPressed() {
		OkButton!.Disabled = true;
		var tasks = new List<Task>();
		foreach (var child in _apiModReleaseItemsContainer!.GetChildren()) {
			if (!IsInstanceValid(this)) {
				return;
			}

			if (child is not ApiModReleaseItem { IsChecked: true } apiModReleaseItem) {
				continue;
			}

			apiModReleaseItem.Disable();
			tasks.Add(apiModReleaseItem.Download());
		}

		await Task.WhenAll(tasks);
		if (!IsInstanceValid(this)) {
			return;
		}

		OkButton!.Disabled = false;
		UpdateApiModInfo();
	}

	public async void UpdateApiModInfo() {
		_loadingContainer!.Show();

		foreach (var child in _apiModReleaseItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		if (ModInfoItem != null) {
			OkButton?.Hide();
			foreach (var apiModRelease in ModInfoItem.ApiModInfo!.Value.Releases) {
				if (!IsInstanceValid(this)) {
					return;
				}

				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = ModInfoItem.Mod;
				apiModReleaseItem.ModpackConfig = ModInfoItem.Mod?.ModpackConfig;
				apiModReleaseItem.ApiModInfo = ModInfoItem.ApiModInfo;
				apiModReleaseItem.ApiModRelease = apiModRelease;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);

				using var tween = apiModReleaseItem.CreateTween();
				tween.TweenProperty(apiModReleaseItem, "modulate:a", 1, 0.025f);
				await ToSignal(tween, Tween.SignalName.Finished);
			}
		}

		if (AutoUpdateModInfoItems != null) {
			OkButton?.Show();
			foreach (var modInfoItem in AutoUpdateModInfoItems) {
				if (!IsInstanceValid(this)) {
					return;
				}

				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = modInfoItem.Mod;
				apiModReleaseItem.ModpackConfig = modInfoItem.Mod?.ModpackConfig;
				apiModReleaseItem.ApiModInfo = modInfoItem.ApiModInfo;
				apiModReleaseItem.ApiModRelease = modInfoItem.ApiModRelease;
				apiModReleaseItem.IsChecked = true;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);

				using var tween = apiModReleaseItem.CreateTween();
				tween.TweenProperty(apiModReleaseItem, "modulate:a", 1, 0.025f);
				await ToSignal(tween, Tween.SignalName.Finished);
			}
		}

		if (ModDependencies != null) {
			OkButton?.Show();
			foreach (var (apiModInfo, apiModRelease, modpackConfig) in ModDependencies) {
				if (!IsInstanceValid(this)) {
					return;
				}

				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = null;
				apiModReleaseItem.ModpackConfig = modpackConfig;
				apiModReleaseItem.ApiModInfo = apiModInfo;
				apiModReleaseItem.ApiModRelease = apiModRelease;
				apiModReleaseItem.IsChecked = true;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);

				using var tween = apiModReleaseItem.CreateTween();
				tween.TweenProperty(apiModReleaseItem, "modulate:a", 1, 0.025f);
				await ToSignal(tween, Tween.SignalName.Finished);
			}
		}

		if (DownloadModInfo != null) {
			OkButton?.Hide();
			foreach (var apiModRelease in DownloadModInfo.Value.apiModInfo.Releases) {
				if (!IsInstanceValid(this)) {
					return;
				}

				var apiModReleaseItem = _apiModReleaseItemScene!.Instantiate<ApiModReleaseItem>();
				apiModReleaseItem.Window = this;
				apiModReleaseItem.ModInfo = DownloadModInfo.Value.modInfo;
				apiModReleaseItem.ModpackConfig = DownloadModInfo.Value.modpackConfig;
				apiModReleaseItem.ApiModInfo = DownloadModInfo.Value.apiModInfo;
				apiModReleaseItem.ApiModRelease = apiModRelease;
				_apiModReleaseItemsContainer!.AddChild(apiModReleaseItem);

				using var tween = apiModReleaseItem.CreateTween();
				tween.TweenProperty(apiModReleaseItem, "modulate:a", 1, 0.025f);
				await ToSignal(tween, Tween.SignalName.Finished);
			}
		}

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
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

		if (ModDependencies is not null) {
			var list = ModDependencies.ToList();
			list.Remove((apiModReleaseItem.ApiModInfo!.Value, apiModReleaseItem.ApiModRelease!.Value,
				apiModReleaseItem.ModpackConfig!));
			ModDependencies = list;
		}

		if (DownloadModInfo is not null) {
			DownloadModInfo = DownloadModInfo.Value with { modInfo = apiModReleaseItem.ModInfo! };
			UpdateApiModInfo();
		}
	}
}
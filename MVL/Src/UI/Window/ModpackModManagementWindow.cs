using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CSemVer;
using Flurl.Http;
using Godot;
using MVL.UI.Item;
using MVL.UI.Page;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackModManagementWindow : BaseWindow {
	[Export]
	private PackedScene? _apiModReleasesWindowScene;

	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private PackedScene? _modInfoItemScene;

	[Export]
	private LineEdit? _searchInput;

	[Export]
	private Button? _searchButton;

	[Export]
	private Button? _updateInfoButton;

	[Export]
	private Button? _syncFileButton;

	[Export]
	private Button? _downloadButton;

	[Export]
	private ScrollContainer? _scrollContainer;

	[Export]
	private VBoxContainer? _modInfoItemsContainer;

	[Export]
	private Control? _loadingContainer;

	public ModpackConfig? ModpackConfig { get; set; }

	private readonly List<ModInfoItem> _autoModInfoItem = [];
	private List<ModDependency> _modDependencies = [];

	public override void _Ready() {
		base._Ready();
		_modInfoItemScene.NotNull();
		_searchInput.NotNull();
		_searchButton.NotNull();
		_updateInfoButton.NotNull();
		_syncFileButton.NotNull();
		_downloadButton.NotNull();
		_modInfoItemsContainer.NotNull();
		_loadingContainer.NotNull();
		ModpackConfig.NotNull();

		CancelButton!.Pressed += CancelButtonOnPressed;
		OkButton!.Pressed += OkButtonOnPressed;
		_searchInput.TextSubmitted += _ => SearchButtonOnPressed();
		_searchButton.Pressed += SearchButtonOnPressed;
		_updateInfoButton.Pressed += UpdateInfoButtonOnPressed;
		_syncFileButton.Pressed += SyncFileButtonOnPressed;
		_downloadButton.Pressed += DownloadButtonOnPressed;
		_loadingContainer.VisibilityChanged += LoadingContainerOnVisibilityChanged;
		ModpackConfig.ModsUpdated += ShowList;
	}

	public override void _ExitTree() {
		base._ExitTree();
		ModpackConfig!.ModsUpdated -= ShowList;
	}

	public override async Task Show() {
		_loadingContainer!.Show();
		await base.Show();
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
		await Hide();
		var browsePage = Main.Instance!.GetNode<BrowsePage>("%BrowsePage");
		browsePage.ModpackConfig = ModpackConfig;
		var button = Main.Instance.GetNode<Button>("%BrowseButton");
		button.ButtonPressed = true;
	}

	private async void DownloadButtonOnPressed() {
		var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
		apiModReleasesWindow.AutoUpdateModInfoItems = _autoModInfoItem;
		apiModReleasesWindow.Hidden += apiModReleasesWindow.QueueFree;
		Main.Instance?.AddChild(apiModReleasesWindow);
		await apiModReleasesWindow.Show();
	}

	private void SyncFileButtonOnPressed() { _ = ModpackConfig!.UpdateModsAsync(); }

	private async void UpdateInfoButtonOnPressed() {
		_downloadButton!.Disabled = true;
		_loadingContainer!.Show();
		List<Task> tasks = [];
		tasks.AddRange(_modInfoItemsContainer!.GetChildren()
			.Cast<ModInfoItem>()
			.TakeWhile(modInfoItem => modInfoItem.Visible && IsInstanceValid(this))
			.Select(modInfoItem => modInfoItem.UpdateApiModInfo()));

		await Task.WhenAll(tasks);

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	private async void SearchButtonOnPressed() {
		var searchString = _searchInput!.Text;
		var newList =
			SortNodesByNameSimilarityWithPrefixPriority(_modInfoItemsContainer!.GetChildren().Cast<ModInfoItem>(),
				searchString);

		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			_modInfoItemsContainer.RemoveChild(child);
		}

		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);

		_loadingContainer!.Show();

		foreach (var modInfoItem in newList) {
			if (!IsInstanceValid(this)) {
				return;
			}

			modInfoItem.Modulate = Colors.Transparent;
			_modInfoItemsContainer.AddChild(modInfoItem);
			if (!modInfoItem.Visible) {
				continue;
			}

			using var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	public async void ShowList(ModpackConfig modpackConfig) {
		if (!IsInstanceValid(this)) {
			modpackConfig.ModsUpdated -= ShowList;
			return;
		}

		_downloadButton!.Disabled = true;
		_modDependencies = [];
		_loadingContainer!.Show();

		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);

		if (modpackConfig.Mods.IsEmpty) {
			_loadingContainer.Hide();
			return;
		}

		var list = modpackConfig.Mods.Values.OrderBy(m => m.ModId);

		foreach (var modpackConfigMod in list) {
			if (!IsInstanceValid(this)) {
				return;
			}

			var modInfoItem = _modInfoItemScene!.Instantiate<ModInfoItem>();
			modInfoItem.Window = this;
			modInfoItem.Mod = modpackConfigMod;
			modInfoItem.Modulate = Colors.Transparent;
			modInfoItem.HasAutoUpdate += ModInfoItemOnHasAutoUpdate;
			modInfoItem.NeedToDepend += ModInfoItemOnNeedToDepend;

			_modInfoItemsContainer!.AddChild(modInfoItem);
			using var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.01f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}

		if (!IsInstanceValid(this)) {
			return;
		}

		_loadingContainer.Hide();

		if (_modDependencies.Count <= 0) {
			return;
		}

		var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
		confirmationWindow.Message = string.Format(Tr("缺少以下依赖模组，是否尝试从ModDB获取？\n{0}"),
			string.Join('\n', _modDependencies.Select(m => $"[b]{m.ModId}[/b]: {m.Version}")));
		confirmationWindow.Modulate = Colors.Transparent;
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		confirmationWindow.Confirm += () => GetModDependency(confirmationWindow);
		Main.Instance?.AddChild(confirmationWindow);
		await confirmationWindow.Show();
	}

	private async void GetModDependency(ConfirmationWindow confirmationWindow) {
		confirmationWindow.Message = Tr("正在从ModDB获取模组信息...");
		confirmationWindow.OkButton!.Disabled = true;

		var list = new List<(ApiModInfo, ApiModRelease, ModpackConfig)>();
		await Task.Run(async () => {
			foreach (var modDependency in _modDependencies) {
				var url = $"https://mods.vintagestory.at/api/mod/{modDependency.ModId}";
				await using var result = await url.GetStreamAsync();
				var status = await JsonSerializer.DeserializeAsync(result, SourceGenerationContext.Default.ApiStatusModInfo);
				if (status.StatusCode is not "200" || !IsInstanceValid(this)) {
					continue;
				}

				var apiModInfo = status.Mod!;
				apiModInfo = apiModInfo.Value with {
					Releases = apiModInfo.Value.Releases.OrderByDescending(modRelease => {
						var version = SVersion.TryParse(modRelease.ModVersion.Replace('*', '0'), out var m)
							? m
							: SVersion.ZeroVersion;
						return version;
					}).ToArray()
				};

				var release = apiModInfo.Value.Releases.Cast<ApiModRelease?>().FirstOrDefault(r => {
					var version = SVersion.TryParse(modDependency.Version.Replace('*', '0'), out var m)
						? m
						: SVersion.ZeroVersion;
					var releaseVersion = SVersion.TryParse(r?.ModVersion, out var v) ? v : SVersion.ZeroVersion;
					return releaseVersion >= version && r!.Value.Tags.Any(gameVersion =>
						GameVersion.ComparerVersion(ModpackConfig!.GameVersion!.Value, new(gameVersion)) >= 0);
				});

				if (release is null) {
					continue;
				}

				list.Add((apiModInfo.Value, release.Value, ModpackConfig!));
			}
		});

		await confirmationWindow.Hide();
		var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
		apiModReleasesWindow.ModDependencies = list;
		apiModReleasesWindow.Hidden += () => {
			apiModReleasesWindow.QueueFree();
			_ = ModpackConfig!.UpdateModsAsync();
		};
		Main.Instance?.AddChild(apiModReleasesWindow);
		await apiModReleasesWindow.Show();
	}

	private void ModInfoItemOnNeedToDepend(ModDependency modDependency) {
		var oldDependency = _modDependencies.Cast<ModDependency?>().FirstOrDefault(m => m?.ModId == modDependency.ModId);
		if (oldDependency is not null) {
			var newVersion = SVersion.TryParse(modDependency.Version.Replace('*', '0'), out var n) ? n : SVersion.ZeroVersion;
			var oldVersion = SVersion.TryParse(oldDependency.Value.Version.Replace('*', '0'), out var o)
				? o
				: SVersion.ZeroVersion;
			if (newVersion <= oldVersion) {
				return;
			}

			_modDependencies.Remove(oldDependency.Value);
		}

		_modDependencies.Add(modDependency);
	}

	private void ModInfoItemOnHasAutoUpdate(ModInfoItem modInfoItem) {
		switch (modInfoItem.CanUpdate) {
			case true when !_autoModInfoItem.Contains(modInfoItem): _autoModInfoItem.Add(modInfoItem); break;
			case false: _autoModInfoItem.Remove(modInfoItem); break;
		}

		_downloadButton!.Disabled = _autoModInfoItem.Count == 0;
	}

	public async void UpdateApiModInfo() {
		_loadingContainer!.Show();

		List<Task> tasks = [];
		tasks.AddRange(_modInfoItemsContainer!.GetChildren().TakeWhile(_ => Visible).OfType<ModInfoItem>()
			.Select(modInfoItem => modInfoItem.UpdateApiModInfo()));

		await Task.WhenAll(tasks);

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	public static IEnumerable<ModInfoItem> SortNodesByNameSimilarityWithPrefixPriority(
		IEnumerable<ModInfoItem> nodesToSort,
		string searchString) {
		if (string.IsNullOrEmpty(searchString)) {
			return nodesToSort.OrderBy(item => {
				item.Show();
				return item.Mod!.ModId;
			});
		}

		var sortedNodes = nodesToSort
			.OrderByDescending(item => {
				var ratio = Fuzzy.PartialRatio($"{item.Mod?.Name} {item.ApiModInfo?.Name} {item.Mod?.ModId}", searchString);
				if (ratio <= 0) {
					item.Hide();
				} else {
					item.Show();
				}

				return ratio;
			});

		return sortedNodes;
	}
}
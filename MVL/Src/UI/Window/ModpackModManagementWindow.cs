using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl.Http;
using FuzzySharp;
using Godot;
using MVL.UI.Item;
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

	public ModpackItem? ModpackItem { get; set; }

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

		CancelButton!.Pressed += CancelButtonOnPressed;
		_searchButton.Pressed += SearchButtonOnPressed;
		_updateInfoButton.Pressed += UpdateInfoButtonOnPressed;
		_syncFileButton.Pressed += SyncFileButtonOnPressed;
		_downloadButton.Pressed += DownloadButtonOnPressed;
	}

	private async void DownloadButtonOnPressed() {
		var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
		apiModReleasesWindow.AutoUpdateModInfoItems = _autoModInfoItem;
		apiModReleasesWindow.Hidden += apiModReleasesWindow.QueueFree;
		Main.Instance?.AddChild(apiModReleasesWindow);
		await apiModReleasesWindow.Show();
	}

	private void SyncFileButtonOnPressed() { ShowList(); }

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

	public async void ShowList() {
		_downloadButton!.Disabled = true;
		_modDependencies = [];
		_loadingContainer!.Show();

		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);

		await ModpackItem!.UpdateMods();
		if (ModpackItem!.ModpackConfig?.Mods == null) {
			_loadingContainer.Hide();
			return;
		}

		var list = ModpackItem.ModpackConfig.Mods.Values.OrderBy(m => m.ModId);

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
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
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
				var result = await url.GetStringAsync();
				var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
				if (status?.StatusCode != "200" || !IsInstanceValid(this)) {
					continue;
				}

				var apiModInfo = status.Mod!;
				var release = apiModInfo.Releases.FirstOrDefault(r => {
					var version = SemVer.TryParse(modDependency.Version.Replace('*', '0'), out var m) ? m : SemVer.Zero;
					var releaseVersion = SemVer.TryParse(r.ModVersion, out var v) ? v : SemVer.Zero;
					return releaseVersion >= version && r.Tags.Any(gameVersion =>
						GameVersion.ComparerVersion(ModpackItem!.ModpackConfig!.Version!.Value, new(gameVersion)) >= 0);
				});

				if (release is null) {
					continue;
				}

				list.Add((apiModInfo, release, ModpackItem!.ModpackConfig!));
			}
		});

		await confirmationWindow.Hide();
		var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
		apiModReleasesWindow.ModDependencies = list;
		apiModReleasesWindow.Hidden += () => {
			apiModReleasesWindow.QueueFree();
			ShowList();
		};
		Main.Instance?.AddChild(apiModReleasesWindow);
		await apiModReleasesWindow.Show();
	}

	private void ModInfoItemOnNeedToDepend(ModDependency modDependency) {
		var oldDependency = _modDependencies.FirstOrDefault(m => m.ModId == modDependency.ModId);
		if (oldDependency is not null) {
			var newVersion = SemVer.TryParse(modDependency.Version.Replace('*', '0'), out var n) ? n : SemVer.Zero;
			var oldVersion = SemVer.TryParse(oldDependency.Version.Replace('*', '0'), out var o) ? o : SemVer.Zero;
			if (newVersion <= oldVersion) {
				return;
			}

			_modDependencies.Remove(oldDependency);
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
				var ratio = Fuzz.PartialRatio($"{item.Mod?.Name} {item.ApiModInfo?.Name} {item.Mod?.ModId}", searchString);
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
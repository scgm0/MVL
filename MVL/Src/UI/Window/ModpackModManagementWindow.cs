using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FuzzySharp;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class ModpackModManagementWindow : BaseWindow {
	[Export]
	private PackedScene? _modInfoItemScene;

	[Export]
	private LineEdit? _searchInput;

	[Export]
	private Button? _searchButton;

	[Export]
	private Button? _updateInfoButton;

	[Export]
	private ScrollContainer? _scrollContainer;
	
	[Export]
	private VBoxContainer? _modInfoItemsContainer;

	[Export]
	private Control? _loadingContainer;

	public ModpackItem? ModpackItem { get; set; }

	public override void _Ready() {
		base._Ready();
		_modInfoItemScene.NotNull();
		_searchInput.NotNull();
		_searchButton.NotNull();
		_updateInfoButton.NotNull();
		_modInfoItemsContainer.NotNull();
		_loadingContainer.NotNull();

		CancelButton!.Pressed += CancelButtonOnPressed;
		_searchButton.Pressed += SearchButtonOnPressed;
		_updateInfoButton.Pressed += UpdateInfoButtonOnPressed;
	}

	private void UpdateInfoButtonOnPressed() {
		ShowList(true);
	}

	private async void SearchButtonOnPressed() {
		if (string.IsNullOrEmpty(_searchInput!.Text)) {
			ShowList();
		}

		var searchString = _searchInput!.Text;
		var newList =
			SortNodesByNameSimilarityWithPrefixPriority(_modInfoItemsContainer!.GetChildren().Select(n => (ModInfoItem)n),
				searchString);

		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			_modInfoItemsContainer.RemoveChild(child);
		}

		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);

		_loadingContainer!.Show();

		foreach (var modInfoItem in newList) {
			if (!Visible) {
				break;
			}

			modInfoItem.Modulate = Colors.Transparent;
			_modInfoItemsContainer.AddChild(modInfoItem);
			if (!modInfoItem.Visible) {
				continue;
			}

			var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	public async void ShowList(bool updateApiModInfo = false) {
		foreach (var child in _modInfoItemsContainer!.GetChildren()) {
			child.QueueFree();
		}

		_scrollContainer!.Call(StringNames.Scroll, true, 0, 0, 0);
		if (ModpackItem!.ModpackConfig?.Mods == null) {
			return;
		}

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

			_modInfoItemsContainer!.AddChild(modInfoItem);
			var tween = modInfoItem.CreateTween();
			tween.TweenProperty(modInfoItem, "modulate:a", 1, 0.025f);
			await ToSignal(tween, Tween.SignalName.Finished);

			if (updateApiModInfo) {
				tasks.Add(modInfoItem.UpdateApiModInfo());
			}
		}

		if (updateApiModInfo) {
			await Task.WhenAll(tasks);
		}

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	public async void UpdateApiModInfo() {
		_loadingContainer!.Show();

		List<Task> tasks = [];
		tasks.AddRange(_modInfoItemsContainer!.GetChildren().TakeWhile(_ => Visible).OfType<ModInfoItem>().Select(modInfoItem => modInfoItem.UpdateApiModInfo()));

		await Task.WhenAll(tasks);

		if (IsInstanceValid(this)) {
			_loadingContainer.Hide();
		}
	}

	public static IEnumerable<ModInfoItem> SortNodesByNameSimilarityWithPrefixPriority(
		IEnumerable<ModInfoItem> nodesToSort,
		string searchString) {
		if (string.IsNullOrEmpty(searchString)) {
			return nodesToSort;
		}

		var searchLower = searchString.ToLowerInvariant();

		var sortedNodes = nodesToSort
			.OrderBy(item => !item.ModName!.Text.StartsWith(searchLower, StringComparison.InvariantCultureIgnoreCase))
			.Select(item => {
				var ratio = Fuzz.PartialRatio(item.ModName!.Text.ToLowerInvariant(), searchLower);
				if (ratio <= 0) {
					item.Hide();
				} else {
					item.Show();
				}

				return (item, ratio);
			})
			.OrderByDescending(v => v.ratio)
			.Select(v => v.item);

		return sortedNodes;
	}
}
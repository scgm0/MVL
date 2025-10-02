using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using MVL.UI.Item;
using MVL.UI.Other;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Page;

public partial class BrowsePage : MenuPage {
	[Export]
	private PackedScene? _moduleItemScene;

	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private PackedScene? _apiModReleasesWindowScene;

	[Export]
	private LineEdit? _modNameLineEdit;

	[Export]
	private Button? _searchButton;

	[Export]
	private Button? _swapButton;

	[Export]
	private SpinBox? _modCountSpinBox;

	[Export]
	private SelectModpackButton? _selectModpackButton;

	[Export]
	private AuthorsLineEdit? _modAuthorLineEdit;

	[Export]
	private SelectionButton? _modVersionsButton;

	[Export]
	private SelectionButton? _modTagsButton;

	[Export]
	private SelectionButton? _modSideButton;

	[Export]
	private SelectionButton? _modOrderByButton;

	[Export]
	private SelectionButton? _modInstallStatusButton;

	[Export]
	private HFlowContainer? _moduleListContainer;

	[Export]
	private LineEdit? _pageNumberLineEdit;

	[Export]
	private Button? _pageNumberButton;

	[Export]
	private Button? _previousPageButton;

	[Export]
	private Button? _nextPageButton;

	[Export]
	private Control? _loadingControl;

	private int CurrentPage {
		get;
		set {
			if (field == value) {
				return;
			}

			field = value;
			_pageNumberLineEdit?.Text = value.ToString();

			_ = UpdateList();
		}
	} = 1;

	private int MaxPage {
		get;
		set {
			if (field == value) {
				return;
			}

			field = value;
			_pageNumberButton?.Text = $"/{value}";
		}
	} = 1;

	public ModpackConfig? ModpackConfig {
		get => _selectModpackButton?.ModpackConfig;
		set => _selectModpackButton?.ModpackConfig = value;
	}

	static private long[] _gameVersionIds = [];
	static private int[] _tagIds = [];
	static private readonly string[] OrderBys = ["asset.created", "lastreleased", "downloads", "follows", "trendingpoints"];
	static private ApiModSummary[] _modSummaryList = [];
	static private ApiModSummary[][] _modSummaryPageList = [];

	public override async void _Ready() {
		base._Ready();
		_moduleItemScene.NotNull();
		_moduleListContainer.NotNull();
		_modNameLineEdit.NotNull();
		_searchButton.NotNull();
		_swapButton.NotNull();
		_modCountSpinBox.NotNull();
		_selectModpackButton.NotNull();
		_modAuthorLineEdit.NotNull();
		_modVersionsButton.NotNull();
		_modTagsButton.NotNull();
		_modSideButton.NotNull();
		_modOrderByButton.NotNull();
		_modInstallStatusButton.NotNull();
		_pageNumberLineEdit.NotNull();
		_pageNumberButton.NotNull();
		_previousPageButton.NotNull();
		_nextPageButton.NotNull();
		_loadingControl.NotNull();

		await _modSideButton.UpdateList(["任意", "客户端", "服务端", "双方"]);
		_modSideButton.Selected = [0];

		await _modOrderByButton.UpdateList(["创建时间", "更新时间", "下载数量", "关注数量", "热门趋势"]);
		_modOrderByButton.Selected = [0];

		await _modInstallStatusButton.UpdateList(["所有", "已安装", "未安装"]);
		_modInstallStatusButton.Selected = [0];

		_modNameLineEdit.TextSubmitted += _ => SearchButtonOnPressed();
		_searchButton.Pressed += SearchButtonOnPressed;
		_swapButton.Toggled += SwapButtonOnToggled;
		_modCountSpinBox.ValueChanged += ModCountSpinBoxOnValueChanged;
		_pageNumberLineEdit.EditingToggled += PageNumberLineEditOnEditingToggled;
		_pageNumberButton.ButtonDown += PageNumberButtonOnButtonDown;
		_previousPageButton.ButtonDown += PreviousPageButtonOnButtonDown;
		_nextPageButton.ButtonDown += NextPageButtonOnButtonDown;

		_modSideButton.SelectionChanged += () => _ = UpdatePage();
		_modInstallStatusButton.SelectionChanged += () => _ = UpdatePage();
		_selectModpackButton.SelectionChanged += () => _ = UpdatePage();
	}

	private void ModCountSpinBoxOnValueChanged(double value) { _ = UpdatePage(); }

	private void SwapButtonOnToggled(bool toggledOn) { _ = UpdatePage(); }

	private void NextPageButtonOnButtonDown() {
		if (CurrentPage >= MaxPage) {
			return;
		}

		CurrentPage++;
	}

	private void PreviousPageButtonOnButtonDown() {
		if (CurrentPage <= 1) {
			return;
		}

		CurrentPage--;
	}

	private void SearchButtonOnPressed() {
		if (_gameVersionIds.Length != 0 && _tagIds.Length != 0) {
			GetModsList();
			return;
		}

		GetOnlineInfo();
	}

	private async void GetModsList() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		if (ModpackConfig is null) {
			var label = new Label {
				Text = "请先创建模组包",
				Modulate = Colors.Yellow,
				LabelSettings = new() {
					FontSize = 20
				}
			};
			_moduleListContainer!.AddChild(label);
			return;
		}

		_loadingControl?.Show();
		var url = "https://mods.vintagestory.at/api/mods";
		var modName = _modNameLineEdit!.Text;
		var modAuthor = _modAuthorLineEdit!.Selected;
		var modVersion = _modVersionsButton!.Selected;
		var modTags = _modTagsButton!.Selected;
		var modOrderBy = _modOrderByButton!.Selected;

		if (!string.IsNullOrWhiteSpace(modName)) {
			url = url.AppendQueryParam("text", modName);
		}

		if (modAuthor is not null) {
			url = url.AppendQueryParam("author", modAuthor.Value.UserId);
		}

		url = modVersion.Aggregate(url,
			(current, v) => current.AppendQueryParam("gameversions[]", _gameVersionIds[v].ToString()));
		url = modTags.Aggregate(url, (current, t) => current.AppendQueryParam("tagids[]", _tagIds[t].ToString()));
		url = modOrderBy.Aggregate(url, (current, o) => current.AppendQueryParam("orderby", OrderBys[o].ToString()));

		GD.Print($"获取模组列表: {url}");
		await Task.Run(async () => {
			try {
				await using var modListStream = await url.GetStreamAsync();
				var modList = JsonSerializer.Deserialize(modListStream, SourceGenerationContext.Default.ApiStatusModsList);
				if (modList.StatusCode is "200") {
					var list = modList.Mods!.Where(m => m.Type == "mod");
					_modSummaryList = list.ToArray();
					Dispatcher.SynchronizationContext.Send(async void (_) => { await UpdatePage(); }, null);
				} else {
					var label = new Label {
						Text = "获取在线信息时发生错误，请检查网络连接",
						Modulate = Colors.Red,
						LabelSettings = new() {
							FontSize = 20
						}
					};
					_moduleListContainer!.AddChild(label);
				}
			} catch (Exception e) {
				GD.PrintErr($"获取在线信息时发生错误: {e.Message}");
				var label = new Label {
					Text = "获取在线信息时发生错误，请检查网络连接",
					Modulate = Colors.Red,
					LabelSettings = new() {
						FontSize = 20
					}
				};
				_moduleListContainer!.AddChild(label);
			}
		});

		_loadingControl?.Hide();
	}

	private async Task UpdatePage() {
		await Task.Run(ModpackConfig!.UpdateMods);

		if (_gameVersionIds.Length == 0 || _tagIds.Length == 0) {
			GetOnlineInfo();
			return;
		}

		var filteredList = new List<ApiModSummary>(_modSummaryList.Length);

		var modInstallStatus = _modInstallStatusButton!.Selected;
		Lazy<HashSet<string>> installedModIdSet = new(() =>
			ModpackConfig!.Mods.Values
				.Select(m => m.ModId)
				.ToHashSet(StringComparer.OrdinalIgnoreCase)
		);

		var modSide = _modSideButton!.Selected;

		foreach (var summary in _modSummaryList) {
			if (modSide[0] != 0) {
				var sideMatch = modSide[0] switch {
					1 => summary.Side == "client", 2 => summary.Side == "server", 3 => summary.Side == "both", _ => false
				};
				if (!sideMatch) {
					continue;
				}
			}

			if (modInstallStatus[0] != 0) {
				var isInstalled = summary.ModIdStrs.Any(installedModIdSet.Value.Contains);
				var installStatusMatch = modInstallStatus[0] switch { 1 => isInstalled, 2 => !isInstalled, _ => false };
				if (!installStatusMatch) {
					continue;
				}
			}

			filteredList.Add(summary);
		}

		if (_swapButton!.ButtonPressed) {
			filteredList.Reverse();
		}

		_modSummaryPageList = filteredList.Chunk((int)_modCountSpinBox!.Value).ToArray();

		MaxPage = _modSummaryPageList.Length > 0 ? _modSummaryPageList.Length : 1;
		CurrentPage = 1;
		await UpdateList();
	}

	private async Task UpdateList() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		if (ModpackConfig is null) {
			var label = new Label {
				Text = "请先创建整合包",
				Modulate = Colors.Yellow,
				LabelSettings = new() {
					FontSize = 20
				}
			};
			_moduleListContainer!.AddChild(label);
			return;
		}

		if (_modSummaryPageList.Length == 0) {
			var label = new Label {
				Text = "没有找到符合条件的模组",
				Modulate = Colors.Yellow,
				LabelSettings = new() {
					FontSize = 20
				}
			};
			_moduleListContainer!.AddChild(label);
			return;
		}

		var list = _modSummaryPageList[CurrentPage - 1];
		foreach (var apiModSummary in list) {
			var moduleItem = _moduleItemScene!.Instantiate<BrowseItem>();
			moduleItem.ModSummary = apiModSummary;
			moduleItem.Pressed += async () => {
				GD.Print(apiModSummary);
				var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
				confirmationWindow.Message = "正在从ModDB获取模组信息...";
				confirmationWindow.Modulate = Colors.Transparent;
				confirmationWindow.Hidden += confirmationWindow.QueueFree;
				confirmationWindow.OkButton!.Disabled = true;
				confirmationWindow.CancelButton!.Disabled = true;
				UI.Main.Instance?.AddChild(confirmationWindow);
				await confirmationWindow.Show();
				try {
					ModpackConfig!.UpdateMods();
					var url = $"https://mods.vintagestory.at/api/mod/{apiModSummary.ModId}";
					await using var result = await url.GetStreamAsync();
					var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
					GD.Print($"获取模组信息: {url} ({status.StatusCode})");
					if (status.StatusCode != "200") {
						confirmationWindow.Message = "获取模组信息失败";
						confirmationWindow.CancelButton!.Disabled = false;
						return;
					}

					await confirmationWindow.Hide();
					var modInfo = ModpackConfig!.Mods.Values.FirstOrDefault(m =>
						apiModSummary.ModIdStrs.Any(s => s.Equals(m.ModId, StringComparison.OrdinalIgnoreCase)));
					var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
					apiModReleasesWindow.DownloadModInfo = (modInfo, status.Mod!.Value, ModpackConfig);
					apiModReleasesWindow.Hidden += () => { apiModReleasesWindow.QueueFree(); };
					UI.Main.Instance?.AddChild(apiModReleasesWindow);
					await apiModReleasesWindow.Show();
				} catch (Exception ex) {
					GD.Print($"获取模组信息时发生错误: {ex.Message}");
					confirmationWindow.Message = "获取模组信息失败";
					confirmationWindow.CancelButton!.Disabled = false;
				}
			};
			_moduleListContainer!.AddChild(moduleItem);
			var tween = moduleItem.CreateTween();
			tween.TweenProperty(moduleItem, "modulate:a", 0.5, 0.1f).From(0);
			await ToSignal(tween, Tween.SignalName.Finished);
			tween.Dispose();
			tween = moduleItem.CreateTween();
			tween.TweenProperty(moduleItem, "modulate:a", 1, 0.2f);
			tween.Dispose();
		}
	}

	private async void GetOnlineInfo() {
		try {
			foreach (var child in _moduleListContainer!.GetChildren()) {
				child.Free();
			}

			if (ModpackConfig is null) {
				var label = new Label {
					Text = "请先创建整合包",
					Modulate = Colors.Yellow,
					LabelSettings = new() {
						FontSize = 20
					}
				};
				_moduleListContainer!.AddChild(label);
				return;
			}

			_loadingControl?.Show();
			using var authorsTask = "https://mods.vintagestory.at/api/authors".GetStreamAsync();
			using var gameVersionsTask = "https://mods.vintagestory.at/api/gameversions".GetStreamAsync();
			using var tagsTask = "https://mods.vintagestory.at/api/tags".GetStreamAsync();

			await Task.WhenAll(authorsTask, gameVersionsTask, tagsTask);

			await using var apiAuthorsStream = await authorsTask;
			await using var apiGameVersionsStream = await gameVersionsTask;
			await using var apiTagsStream = await tagsTask;

			var apiAuthors = JsonSerializer.Deserialize(apiAuthorsStream,
				SourceGenerationContext.Default.ApiStatusAuthors);
			GD.PrintS("apiAuthors:", apiAuthors.StatusCode);
			if (apiAuthors.StatusCode is "200") {
				_modAuthorLineEdit!.Candidates = apiAuthors.Authors ?? [];
			}

			var apiGameVersions = JsonSerializer.Deserialize(apiGameVersionsStream,
				SourceGenerationContext.Default.ApiStatusGameVersions);
			GD.PrintS("apiGameVersions:", apiGameVersions.StatusCode);
			if (apiGameVersions.StatusCode is "200") {
				apiGameVersions.GameVersions?.Reverse();

				Array.Resize(ref _gameVersionIds, apiGameVersions.GameVersions?.Length ?? 0);
				for (var i = 0; i < _gameVersionIds.Length; i++) {
					_gameVersionIds[i] = apiGameVersions.GameVersions![i].TagId;
				}

				var list = new string[apiGameVersions.GameVersions?.Length ?? 0];
				for (var i = 0; i < list.Length; i++) {
					list[i] = apiGameVersions.GameVersions![i].Name;
				}

				_ = _modVersionsButton!.UpdateList(list);
			}

			var apiTags = JsonSerializer.Deserialize(apiTagsStream,
				SourceGenerationContext.Default.ApiStatusModTags);
			GD.PrintS("apiTags:", apiTags.StatusCode);
			if (apiTags.StatusCode is "200") {
				Array.Resize(ref _tagIds, apiTags.Tags?.Length ?? 0);
				for (var i = 0; i < _tagIds.Length; i++) {
					_tagIds[i] = apiTags.Tags![i].TagId;
				}

				var list = new string[apiTags.Tags?.Length ?? 0];
				for (var i = 0; i < list.Length; i++) {
					list[i] = apiTags.Tags![i].Name;
				}

				_ = _modTagsButton!.UpdateList(list);
			}

			GetModsList();
		} catch (Exception ex) {
			GD.PrintErr($"获取在线信息时发生错误: {ex.Message}");
			var label = new Label {
				Text = "获取在线信息时发生错误，请检查网络连接",
				Modulate = Colors.Red,
				LabelSettings = new() {
					FontSize = 20
				}
			};
			_moduleListContainer!.AddChild(label);
			_loadingControl?.Hide();
		}
	}

	private void PageNumberButtonOnButtonDown() {
		_pageNumberLineEdit?.GrabFocus(true);
		_pageNumberLineEdit?.CaretColumn = _pageNumberLineEdit.Text.Length;
	}

	private void PageNumberLineEditOnEditingToggled(bool toggledOn) {
		if (toggledOn) {
			return;
		}

		var inputText = _pageNumberLineEdit?.Text ?? string.Empty;

		var pageText = NumRegex().Replace(inputText, "");

		if (int.TryParse(pageText, out var parsedPage)) {
			CurrentPage = Math.Clamp(parsedPage, 1, MaxPage);
		} else {
			CurrentPage = string.IsNullOrWhiteSpace(pageText) ? 1 : MaxPage;
		}
	}

	[GeneratedRegex(@"[^\d]")]
	static private partial Regex NumRegex();
}
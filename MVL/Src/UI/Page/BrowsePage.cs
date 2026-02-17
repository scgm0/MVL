using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;
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

			_ = UpdateListAsync();
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

		_modSideButton.SelectionChanged += () => _ = UpdatePageAsync();
		_modInstallStatusButton.SelectionChanged += () => _ = UpdatePageAsync();
		_selectModpackButton.SelectionChanged += () => _ = UpdatePageAsync();
	}

	private void ModCountSpinBoxOnValueChanged(double value) { _ = UpdatePageAsync(); }

	private void SwapButtonOnToggled(bool toggledOn) { _ = UpdatePageAsync(); }

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
			_ = GetModsListAsync();
			return;
		}

		_ = GetOnlineInfoAsync();
	}

	private async Task GetModsListAsync() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		if (ModpackConfig is null) {
			ShowMessageInContainer(_moduleListContainer!, "请先创建整合包", Colors.Yellow);
			return;
		}

		_loadingControl?.Show();

		try {
			var url = BuildModsApiUrl();
			Log.Debug($"获取模组列表: {url}");

			await using var modListStream = await url.GetStreamAsync();
			var modList =
				await JsonSerializer.DeserializeAsync(modListStream, SourceGenerationContext.Default.ApiStatusModsList);

			Log.Debug($"模组列表获取完成，StatusCode: {modList.StatusCode}");

			if (modList.StatusCode is "200") {
				var mods = modList.Mods ?? [];
				var filteredList = new List<ApiModSummary>(mods.Length);
				foreach (var mod in mods) {
					if (mod.Type == "mod") {
						filteredList.Add(mod);
					}
				}

				_modSummaryList = filteredList.ToArray();

				_loadingControl?.Hide();
				await UpdatePageAsync();
			} else {
				ShowMessageInContainer(_moduleListContainer!, "获取模组列表失败，请检查筛选条件或网络", Colors.Red);
			}
		} catch (Exception ex) {
			_loadingControl?.Hide();
			Log.Error("获取在线信息时发生错误", ex);
			ShowMessageInContainer(_moduleListContainer!, "获取在线信息时发生错误，请检查网络连接", Colors.Red);
		}
	}

	private string BuildModsApiUrl() {
		const string baseUrl = "https://mods.vintagestory.at/api/mods";
		var queryParams = new List<KeyValuePair<string, string>>();

		var modName = _modNameLineEdit!.Text;
		if (!string.IsNullOrWhiteSpace(modName)) {
			queryParams.Add(new("text", modName));
		}

		var modAuthor = _modAuthorLineEdit!.Selected;
		if (modAuthor is not null) {
			queryParams.Add(new("author", modAuthor.Value.UserId.ToString()));
		}

		foreach (var v in _modVersionsButton!.Selected) {
			queryParams.Add(new("gameversions[]", _gameVersionIds[v].ToString()));
		}

		foreach (var t in _modTagsButton!.Selected) {
			queryParams.Add(new("tagids[]", _tagIds[t].ToString()));
		}

		foreach (var o in _modOrderByButton!.Selected) {
			queryParams.Add(new("orderby", OrderBys[o]));
		}

		if (queryParams.Count == 0) {
			return baseUrl;
		}

		var queryString = string.Join("&",
			queryParams.Select(kvp =>
				$"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

		return $"{baseUrl}?{queryString}";
	}

	private async Task UpdatePageAsync() {
		await Task.Run(ModpackConfig!.UpdateMods);

		if (_gameVersionIds.Length == 0 || _tagIds.Length == 0) {
			await GetOnlineInfoAsync();
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
		await UpdateListAsync();
	}

	private async Task UpdateListAsync() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		if (ModpackConfig is null) {
			ShowMessageInContainer(_moduleListContainer!, "请先创建整合包", Colors.Yellow);
			return;
		}

		if (_modSummaryPageList.Length == 0) {
			ShowMessageInContainer(_moduleListContainer!, "没有找到符合条件的模组", Colors.Yellow);
			return;
		}

		var list = _modSummaryPageList[CurrentPage - 1];
		foreach (var apiModSummary in list) {
			var moduleItem = _moduleItemScene!.Instantiate<BrowseItem>();
			moduleItem.ModSummary = apiModSummary;
			moduleItem.Pressed += async () => {
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
					var status = await JsonSerializer.DeserializeAsync(result,
						SourceGenerationContext.Default.ApiStatusModInfo);
					Log.Debug($"获取模组信息: {url} ({status.StatusCode})");
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
					Log.Error("获取模组信息时发生错误", ex);
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

	private async Task GetOnlineInfoAsync() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.QueueFree();
		}

		if (ModpackConfig is null) {
			ShowMessageInContainer(_moduleListContainer!, "请先创建整合包", Colors.Yellow);
			return;
		}

		_loadingControl?.Show();

		try {
			var authorsTask = FetchAndDeserializeAsync(
				"https://mods.vintagestory.at/api/authors",
				SourceGenerationContext.Default.ApiStatusAuthors);

			var gameVersionsTask = FetchAndDeserializeAsync(
				"https://mods.vintagestory.at/api/gameversions",
				SourceGenerationContext.Default.ApiStatusGameVersions);

			var tagsTask = FetchAndDeserializeAsync(
				"https://mods.vintagestory.at/api/tags",
				SourceGenerationContext.Default.ApiStatusModTags);

			await Task.WhenAll(authorsTask, gameVersionsTask, tagsTask);

			var apiAuthors = await authorsTask;
			var apiGameVersions = await gameVersionsTask;
			var apiTags = await tagsTask;

			ProcessAuthors(apiAuthors);
			ProcessGameVersions(apiGameVersions);
			ProcessTags(apiTags);

			await GetModsListAsync();
		} catch (Exception ex) {
			_loadingControl?.Hide();
			Log.Error("获取在线信息时发生错误", ex);
			ShowMessageInContainer(_moduleListContainer!, "获取在线信息时发生错误，请检查网络连接", Colors.Red);
		} finally {
			_loadingControl?.Hide();
		}
	}

	private async Task<T?> FetchAndDeserializeAsync<T>(
		string url,
		JsonTypeInfo<T> typeInfo) {
		Log.Debug($"正在获取: {url}");
		await using var stream = await url.GetStreamAsync();
		var result = await JsonSerializer.DeserializeAsync(stream, typeInfo);
		return result;
	}

	private void ProcessAuthors(ApiStatusAuthors? apiAuthors) {
		Log.Debug($"apiAuthors status: {apiAuthors?.StatusCode}");
		if (apiAuthors?.StatusCode is "200") {
			_modAuthorLineEdit!.Candidates = apiAuthors.Value.Authors ?? [];
		}
	}

	private void ProcessGameVersions(ApiStatusGameVersions? apiGameVersions) {
		Log.Debug($"apiGameVersions status: {apiGameVersions?.StatusCode}");
		if (apiGameVersions?.StatusCode is not "200") {
			return;
		}

		var versions = apiGameVersions.Value.GameVersions ?? [];
		var count = versions.Length;

		Array.Resize(ref _gameVersionIds, count);
		var nameList = new string[count];

		for (var i = 0; i < count; i++) {
			var version = versions[count - 1 - i];
			_gameVersionIds[i] = version.TagId;
			nameList[i] = version.Name;
		}

		_ = _modVersionsButton!.UpdateList(nameList);
	}

	private void ProcessTags(ApiStatusModTags? apiTags) {
		Log.Debug($"apiTags status: {apiTags?.StatusCode}");
		if (apiTags?.StatusCode is not "200") {
			return;
		}

		var tags = apiTags.Value.Tags ?? [];
		var count = tags.Length;

		Array.Resize(ref _tagIds, count);
		var nameList = new string[count];

		for (var i = 0; i < count; i++) {
			var tag = tags[i];
			_tagIds[i] = tag.TagId;
			nameList[i] = tag.Name;
		}

		_ = _modTagsButton!.UpdateList(nameList);
	}

	private void ShowMessageInContainer(Node container, string text, Color color) {
		var label = new Label {
			Text = text,
			Modulate = color,
			LabelSettings = new() { FontSize = 20 }
		};
		container.AddChild(label);
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
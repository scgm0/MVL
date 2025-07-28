using Godot;
using System;
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

public partial class ModulePage : MenuPage {
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

	private ModpackConfig? _modpackConfig;

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

		_searchButton.Pressed += SearchButtonOnPressed;
		_swapButton.Toggled += SwapButtonOnToggled;
		_modCountSpinBox.ValueChanged += ModCountSpinBoxOnValueChanged;
		_pageNumberLineEdit.EditingToggled += PageNumberLineEditOnEditingToggled;
		_pageNumberButton.ButtonDown += PageNumberButtonOnButtonDown;
		_previousPageButton.ButtonDown += PreviousPageButtonOnButtonDown;
		_nextPageButton.ButtonDown += NextPageButtonOnButtonDown;
		VisibilityChanged += OnVisibilityChanged;
	}

	private void ModCountSpinBoxOnValueChanged(double value) {
		_ = UpdatePage();
	}

	private void SwapButtonOnToggled(bool toggledOn) {
		_ = UpdatePage();
	}

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

	private void SearchButtonOnPressed() { GetModsList(); }

	private void OnVisibilityChanged() {
		if (!Visible || _modpackConfig is not null) {
			return;
		}

		_modpackConfig = UI.Main.ModpackConfigs[UI.Main.BaseConfig.CurrentModpack];
		GetOnlineInfo();
	}

	private async void GetModsList() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		_loadingControl?.Show();
		var url = "https://mods.vintagestory.at/api/mods";
		var modName = _modNameLineEdit!.Text;
		var modAuthor = _modAuthorLineEdit!.Selected;
		var modVersion = _modVersionsButton!.Selected;
		var modTags = _modTagsButton!.Selected;
		var modSide = _modSideButton!.Selected;
		var modOrderBy = _modOrderByButton!.Selected;
		var modInstallStatus = _modInstallStatusButton!.Selected;

		if (!string.IsNullOrWhiteSpace(modName)) {
			url = url.AppendQueryParam("text", modName);
		}

		if (modAuthor is not null) {
			url = url.AppendQueryParam("author", modAuthor.UserId);
		}

		url = modVersion.Aggregate(url,
			(current, v) => current.AppendQueryParam("gameversions[]", _gameVersionIds[v].ToString()));
		url = modTags.Aggregate(url, (current, t) => current.AppendQueryParam("tagids[]", _tagIds[t].ToString()));
		url = modOrderBy.Aggregate(url, (current, o) => current.AppendQueryParam("orderby", OrderBys[o].ToString()));

		await Task.Run(async () => {
			var modListText = await url.GetStringAsync();
			var modList = JsonSerializer.Deserialize(modListText, SourceGenerationContext.Default.ApiStatusModsList);
			if (modList?.StatusCode is "200") {
				var list = modList.Mods!.Where(m => m.Type == "mod");
				if (modSide[0] != 0) {
					list = list.Where(summary => {
						return modSide[0] switch {
							1 => summary.Side == "client",
							2 => summary.Side == "server",
							3 => summary.Side == "both",
							_ => false
						};
					});
				}

				if (modInstallStatus[0] != 0) {
					_modpackConfig!.UpdateMods();
					list = list.Where(summary => {
						var isInstalled = _modpackConfig!.Mods.Values.Any(m =>
							summary.ModIdStrs.Any(s => s.Equals(m.ModId, StringComparison.OrdinalIgnoreCase)));
						return modInstallStatus[0] switch { 1 => isInstalled, 2 => !isInstalled, _ => false };
					});
				}

				_modSummaryList = list.ToArray();
				Dispatcher.SynchronizationContext.Send(async void (_) => { await UpdatePage(); }, null);
			}
		});

		_loadingControl?.Hide();
	}

	private async Task UpdatePage() {
		var list = _modSummaryList.ToList();
		if (_swapButton!.ButtonPressed) {
			list.Reverse();
		}
		_modSummaryPageList = list.Chunk((int)_modCountSpinBox!.Value).ToArray();
		MaxPage = _modSummaryPageList.Length > 0 ? _modSummaryPageList.Length : 1;
		CurrentPage = 1;
		await UpdateList();
	}

	private async Task UpdateList() {
		foreach (var child in _moduleListContainer!.GetChildren()) {
			child.Free();
		}

		if (_modSummaryPageList.Length == 0) {
			return;
		}

		var list = _modSummaryPageList[CurrentPage - 1];
		foreach (var apiModSummary in list) {
			var moduleItem = _moduleItemScene!.Instantiate<ModuleItem>();
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
					_modpackConfig!.UpdateMods();
					var url = $"https://mods.vintagestory.at/api/mod/{apiModSummary.ModId}";
					var result = await url.GetStringAsync();
					var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
					if (status?.StatusCode != "200") {
						confirmationWindow.Message = "获取模组信息失败";
						confirmationWindow.OkButton!.Disabled = false;
						confirmationWindow.CancelButton!.Disabled = false;
						return;
					}

					await confirmationWindow.Hide();
					var modInfo = _modpackConfig!.Mods.Values.FirstOrDefault(m =>
						apiModSummary.ModIdStrs.Any(s => s.Equals(m.ModId, StringComparison.OrdinalIgnoreCase)));
					var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
					apiModReleasesWindow.DownloadModInfo = (modInfo, status.Mod!, _modpackConfig);
					apiModReleasesWindow.Hidden += () => { apiModReleasesWindow.QueueFree(); };
					UI.Main.Instance?.AddChild(apiModReleasesWindow);
					await apiModReleasesWindow.Show();
				} catch (Exception ex) {
					GD.Print($"获取模组信息时发生错误: {ex.Message}");
					confirmationWindow.Message = "获取模组信息失败";
					confirmationWindow.OkButton!.Disabled = false;
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
			_loadingControl?.Show();
			using var authorsTask = "https://mods.vintagestory.at/api/authors".GetStringAsync();
			using var gameVersionsTask = "https://mods.vintagestory.at/api/gameversions".GetStringAsync();
			using var tagsTask = "https://mods.vintagestory.at/api/tags".GetStringAsync();

			await Task.WhenAll(authorsTask, gameVersionsTask, tagsTask);

			var apiAuthorsText = await authorsTask;
			var apiGameVersionsText = await gameVersionsTask;
			var apiTagsText = await tagsTask;

			GD.Print($"apiGameVersionsText: {apiGameVersionsText}");
			GD.Print($"apiTagsText: {apiTagsText}");
			var apiAuthors = JsonSerializer.Deserialize(apiAuthorsText,
				SourceGenerationContext.Default.ApiStatusAuthors);
			if (apiAuthors?.StatusCode is "200") {
				_modAuthorLineEdit?.Candidates = apiAuthors.Authors ?? [];
			}

			var apiGameVersions = JsonSerializer.Deserialize(apiGameVersionsText,
				SourceGenerationContext.Default.ApiStatusGameVersions);
			if (apiGameVersions?.StatusCode is "200") {
				apiGameVersions.GameVersions.Reverse();
				_gameVersionIds = (apiGameVersions.GameVersions ?? []).Select(g => g.TagId).ToArray();
				var list = (apiGameVersions.GameVersions ?? []).Select(g => g.Name).ToList();
				await _modVersionsButton!.UpdateList(list);
			}

			var apiTags = JsonSerializer.Deserialize(apiTagsText,
				SourceGenerationContext.Default.ApiStatusModTags);
			if (apiTags?.StatusCode is "200") {
				_tagIds = (apiTags.Tags ?? []).Select(t => t.TagId).ToArray();
				await _modTagsButton!.UpdateList((apiTags.Tags ?? []).Select(t => t.Name).ToList());
			}

			GetModsList();
		} catch (Exception ex) {
			GD.PrintErr($"获取在线信息时发生错误: {ex.Message}");
		}
	}

	private void PageNumberButtonOnButtonDown() {
		_pageNumberLineEdit?.GrabFocus();
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
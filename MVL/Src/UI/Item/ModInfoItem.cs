using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CSemVer;
using Downloader;
using Flurl.Http;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;
using HttpClient = System.Net.Http.HttpClient;
using Range = Godot.Range;

namespace MVL.UI.Item;

public partial class ModInfoItem : PanelContainer {
	[Export]
	private PackedScene? _apiModReleasesWindowScene;

	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private TextureRect? _icon;

	[Export]
	private RichTextLabel? _modName;

	[Export]
	private RichTextLabel? _version;

	[Export]
	private Label? _description;

	[Export]
	private Button? _webButton;

	[Export]
	private Button? _updateButton;

	[Export]
	private Button? _releaseButton;

	[Export]
	private Button? _deleteButton;

	[Export]
	private ProgressBar? _progressBar;

	public ModpackModManagementWindow? Window { get; set; }

	public ModInfo? Mod { get; set; }

	public ApiModInfo? ApiModInfo { get; set; }


	public ApiModRelease? ApiModRelease { get; set; }

	public bool HasNewVersion { get; set; }

	public bool CanUpdate { get; set; }

	public event Action<ModInfoItem>? HasAutoUpdate;

	public event Action<ModDependency>? NeedToDepend;

	static private readonly HashSet<string> SystemModIds = new(StringComparer.OrdinalIgnoreCase) {
		"game",
		"survival",
		"essentials",
		"creative"
	};

	public override void _Ready() {
		_icon.NotNull();
		_modName.NotNull();
		_version.NotNull();
		_description.NotNull();
		_updateButton.NotNull();
		_progressBar.NotNull();

		UpdateUI();

		_webButton!.Pressed += WebButtonOnPressed;
		_updateButton.Pressed += UpdateButtonOnPressed;
		_releaseButton!.Pressed += ReleaseButtonOnPressed;
		_deleteButton!.Pressed += DeleteButtonOnPressed;

		Task.Run(DependencyDetection);
	}

	private async void ReleaseButtonOnPressed() {
		var apiModReleasesWindow = _apiModReleasesWindowScene!.Instantiate<ApiModReleasesWindow>();
		apiModReleasesWindow.ModInfoItem = this;
		apiModReleasesWindow.Hidden += apiModReleasesWindow.QueueFree;
		Main.Instance?.AddChild(apiModReleasesWindow);
		await apiModReleasesWindow.Show();
	}

	private void DeleteButtonOnPressed() {
		var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
		confirmationWindow!.Message = string.Format(Tr("确定要删除 [b]{0}[/b] 吗？"), Mod!.Name);
		confirmationWindow.Modulate = Colors.Transparent;
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		confirmationWindow.Confirm += async () => {
			await confirmationWindow.Hide();
			CanUpdate = false;
			HasAutoUpdate?.Invoke(this);
			File.Delete(Mod!.ModPath);

			await Window!.ModpackConfig!.UpdateModsAsync();
			var newMod = Window.ModpackConfig.Mods.Values.FirstOrDefault(x => x.ModId == Mod.ModId);
			if (newMod is not null) {
				Mod = newMod;
				await UpdateApiModInfo();
			} else {
				QueueFree();
			}
		};
		Main.Instance?.AddChild(confirmationWindow);
		_ = confirmationWindow.Show();
	}

	private void WebButtonOnPressed() {
		Tools.RichTextOpenUrl(ApiModInfo?.UrlAlias is null
			? $"https://mods.vintagestory.at/show/mod/{ApiModInfo?.AssetId}"
			: $"https://mods.vintagestory.at/{ApiModInfo.Value.UrlAlias}");
	}

	private async void UpdateButtonOnPressed() {
		_updateButton!.Disabled = true;
		_progressBar!.Show();
		Log.Info($"开始下载: {ApiModRelease!.Value.FileName}...");

		using var downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = downloadTmp.GetCurrentDir();
		var download = DownloadBuilder.New()
			.WithUrl(ApiModRelease?.MainFile)
			.WithDirectory(downloadDir)
			.WithFileName(ApiModRelease?.FileName)
			.WithConfiguration(new() {
				ParallelDownload = true,
				ChunkCount = Main.BaseConfig.DownloadThreads,
				ParallelCount = Main.BaseConfig.DownloadThreads,
				RequestConfiguration = new() {
					Proxy = string.IsNullOrWhiteSpace(Main.BaseConfig.ProxyAddress)
						? HttpClient.DefaultProxy
						: new WebProxy(Main.BaseConfig.ProxyAddress)
				}
			})
			.Build();
		download.DownloadProgressChanged += (_, args) => {
			_progressBar.CallDeferred(Range.MethodName.SetValue, args.ProgressPercentage);
		};
		await download.StartAsync();
		download.Dispose();

		var modFile = Path.Combine(downloadDir, ApiModRelease!.Value.FileName);
		if (!File.Exists(modFile)) {
			Log.Error($"下载失败: {modFile} 不存在");
			if (IsInstanceValid(this)) {
				_progressBar.Hide();
				await UpdateApiModInfo();
				return;
			}
		}

		var path = Path.Combine(Mod!.ModPath.GetBaseDir(), ApiModRelease.Value.FileName);
		File.Move(modFile, path);
		Log.Info($"下载完成: {ApiModRelease.Value.FileName}");

		if (File.Exists(Mod.ModPath) && !path.Equals(Mod.ModPath, StringComparison.OrdinalIgnoreCase)) {
			File.Delete(Mod.ModPath);
			Log.Debug($"删除旧文件: {Mod.ModPath.GetFile()}");
		}

		var mod = ModInfo.FromZip(path);
		if (mod != null) {
			mod.ModpackConfig = Mod!.ModpackConfig;
			mod.ModpackConfig!.Mods[mod.ModId] = mod;
			Mod = mod;
		}

		if (!IsInstanceValid(this)) {
			return;
		}

		_progressBar.Hide();
		await UpdateApiModInfo();
	}

	public void DependencyDetection() {
		foreach (var modDependency in Mod!.Dependencies) {
			var modId = modDependency.ModId;

			if (SystemModIds.Contains(modId)) {
				continue;
			}

			if (!SVersion.TryParse(modDependency.Version.Replace('*', '0'), out var version)) {
				version = SVersion.ZeroVersion;
			}

			var hasDependency = Mod.ModpackConfig!.Mods.Any(kv => {
				var mod = kv.Value;
				return mod.ModId.Equals(modId, StringComparison.OrdinalIgnoreCase) &&
					SVersion.TryParse(mod.Version, out var ver) && ver >= version;
			});

			if (!hasDependency) {
				NeedToDepend?.Invoke(modDependency);
			}
		}
	}

	public void UpdateUI() {
		_modName!.Text = Mod?.Name;
		_modName.TooltipText = Mod!.ModPath;
		_version!.Text = $"{Mod?.ModId}-{Mod?.Version}";
		_description!.Text = Mod?.Description;
		_webButton!.Disabled = true;
		_releaseButton!.Disabled = true;
		_updateButton!.Disabled = true;
		_updateButton!.Modulate = Colors.White;

		if (string.IsNullOrEmpty(Mod?.Icon)) {
			return;
		}

		Texture2D? texture;
		if (Mod.ModPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
			Task.Run(() => {
				using var zipArchive = ZipFile.OpenRead(Mod.ModPath);
				var iconEntry = zipArchive.GetEntry(Mod.Icon);
				if (iconEntry == null) {
					return;
				}

				var path = Path.Combine(Mod.ModPath, iconEntry.FullName);
				if (ResourceLoader.Exists(path)) {
					texture = ResourceLoader.Load<Texture2D>(path);
				} else {
					using var iconStream = iconEntry.Open();
					using var iconReader = new BinaryReader(iconStream);
					var iconBytes = iconReader.ReadBytes((int)iconEntry.Length);
					texture = Tools.CreateTextureFromBytes(iconBytes);
				}

				if (texture is null) {
					return;
				}

				texture.TakeOverPath(path);
				_icon!.SetDeferred(TextureRect.PropertyName.Texture, texture);
			});
		} else {
			var iconPath = Path.Combine(Mod.ModPath, Mod.Icon);
			texture = Tools.LoadTextureFromPath(iconPath);

			if (texture is null) {
				return;
			}

			_icon!.Texture = texture;
		}
	}

	public async Task UpdateApiModInfo() {
		UpdateUI();
		ApiModInfo = null;
		ApiModRelease = null;
		HasNewVersion = false;
		CanUpdate = false;
		HasAutoUpdate?.Invoke(this);

		if (!string.IsNullOrWhiteSpace(Mod!.ModId)) {
			try {
				var url = $"https://mods.vintagestory.at/api/mod/{Mod.ModId}";
				await using var result = await url.GetStreamAsync();
				var status = await JsonSerializer.DeserializeAsync(result, SourceGenerationContext.Default.ApiStatusModInfo);
				if (status.StatusCode is not "200" || !IsInstanceValid(this)) {
					return;
				}

				ApiModInfo = status.Mod!;

				await Task.Run(UpdateApiModRelease);

				if (_modName == null || !IsInstanceValid(this)) {
					return;
				}

				_webButton!.Disabled = false;
				_releaseButton!.Disabled = false;
				_modName.Text = Mod!.Name.Equals(ApiModInfo.Value.Name, StringComparison.Ordinal)
					? Mod.Name
					: $"{ApiModInfo.Value.Name} ({Mod.Name})";
			} catch (Exception e) {
				Log.Error(e);
			}
		}
	}

	public void UpdateApiModRelease() {
		foreach (var modInfoRelease in ApiModInfo!.Value.Releases) {
			try {
				if (!IsInstanceValid(this)) {
					return;
				}

				var version1 = SVersion.Parse(Mod!.Version);
				var version2 = SVersion.Parse(modInfoRelease.ModVersion);
				if (version1 >= version2) {
					continue;
				}

				if (string.IsNullOrWhiteSpace(modInfoRelease.MainFile)) {
					continue;
				}

				HasNewVersion = true;
				ApiModRelease = modInfoRelease;
				Log.Debug(
					$"找到可更新版本: {ApiModInfo.Value.Name} {modInfoRelease.ModVersion} (现有版本: {Mod.Version}) (兼容的游戏版本: {modInfoRelease.Tags.Stringify()})");
				if (!modInfoRelease.Tags.Any(gameVersion =>
					GameVersion.ComparerVersion(Mod.ModpackConfig!.GameVersion!.Value, new(gameVersion)) >= 0)) {
					continue;
				}

				CanUpdate = true;
				Dispatcher.SynchronizationContext.Post(_ => {
						if (!IsInstanceValid(this)) {
							return;
						}

						_updateButton!.Disabled = false;
						_updateButton!.Modulate = Colors.Green;
						HasAutoUpdate?.Invoke(this);
					},
					null);
				return;
			} catch (Exception e) {
				Log.Error($"解析失败 {Mod!.ModId} {Mod!.Version} {modInfoRelease.ModVersion}", e);
			}
		}
	}
}
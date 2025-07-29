using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Downloader;
using Flurl.Http;
using Godot;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;
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

		_icon!.Texture = Mod?.Icon;
		_modName!.Text = Mod?.Name;
		_modName.TooltipText = Mod!.ModPath;
		_version!.Text = $"{Mod?.ModId}-{Mod?.Version}";
		_description!.Text = Mod?.Description;
		_webButton!.Disabled = true;
		_updateButton!.Disabled = true;
		_updateButton!.Modulate = Colors.White;

		_webButton!.Pressed += WebButtonOnPressed;
		_updateButton.Pressed += UpdateButtonOnPressed;
		_releaseButton!.Pressed += ReleaseButtonOnPressed;
		_deleteButton!.Pressed += DeleteButtonOnPressed;

		DependencyDetection();
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
			await Window!.ModpackItem!.UpdateMods();
			var newMod = Window!.ModpackItem!.ModpackConfig!.Mods.Values.FirstOrDefault(x => x.ModId == Mod.ModId);
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
		Tools.RichTextOpenUrl(ApiModInfo!.UrlAlias is null
			? $"https://mods.vintagestory.at/show/mod/{ApiModInfo!.AssetId}"
			: $"https://mods.vintagestory.at/{ApiModInfo.UrlAlias}");
	}

	private async void UpdateButtonOnPressed() {
		_updateButton!.Disabled = true;
		_progressBar!.Show();
		GD.Print($"下载 {ApiModRelease!.FileName}...");

		using var downloadTmp = DirAccess.CreateTemp("MVL_Download");
		var downloadDir = downloadTmp.GetCurrentDir();
		var download = DownloadBuilder.New()
			.WithUrl(ApiModRelease!.MainFile)
			.WithDirectory(downloadDir)
			.WithFileName(ApiModRelease.FileName)
			.WithConfiguration(new() {
				ParallelDownload = true,
				ChunkCount = Main.BaseConfig.DownloadThreads,
				ParallelCount = Main.BaseConfig.DownloadThreads,
				RequestConfiguration = new() {
					Proxy = string.IsNullOrEmpty(Main.BaseConfig.ProxyAddress)
						? null
						: new WebProxy(Main.BaseConfig.ProxyAddress)
				}
			})
			.Build();
		download.DownloadProgressChanged += (_, args) => {
			_progressBar.CallDeferred(Range.MethodName.SetValue, args.ProgressPercentage);
		};
		await download.StartAsync();
		download.Dispose();

		if (!IsInstanceValid(this)) {
			return;
		}

		_progressBar.Hide();

		var path = Path.Combine(Mod!.ModPath.GetBaseDir(), ApiModRelease.FileName);
		File.Move(Path.Combine(downloadDir, ApiModRelease.FileName), path);
		File.Delete(Mod!.ModPath);

		var mod = ModInfo.FromZip(path);
		if (mod != null) {
			mod.ModpackConfig = Mod!.ModpackConfig;
			mod.ModpackConfig!.Mods[mod.ModId] = mod;
			Mod = mod;
		}

		await UpdateApiModInfo();
	}

	public void DependencyDetection() {
		foreach (var modDependency in Mod!.Dependencies) {
			var modId = modDependency.ModId;

			if (SystemModIds.Contains(modId)) {
				continue;
			}

			if (!SemVer.TryParse(modDependency.Version.Replace('*', '0'), out var version)) {
				version = SemVer.Zero;
			}

			var hasDependency = Mod.ModpackConfig!.Mods.Any(kv => {
				var mod = kv.Value;
				return mod.ModId.Equals(modId, StringComparison.OrdinalIgnoreCase) &&
					SemVer.TryParse(mod.Version, out var ver) && ver >= version;
			});

			if (!hasDependency) {
				NeedToDepend?.Invoke(modDependency);
			}
		}
	}

	public async Task UpdateApiModInfo() {
		_icon!.Texture = Mod!.Icon;
		_modName!.Text = Mod!.Name;
		_modName.TooltipText = Mod!.ModPath;
		_version!.Text = $"{Mod?.ModId}-{Mod?.Version}";
		_description!.Text = Mod?.Description;
		_webButton!.Disabled = true;
		_releaseButton!.Disabled = true;
		_updateButton!.Disabled = true;
		_updateButton!.Modulate = Colors.White;
		ApiModInfo = null;
		ApiModRelease = null;
		HasNewVersion = false;
		CanUpdate = false;
		HasAutoUpdate?.Invoke(this);

		await Task.Run(async () => {
			if (!string.IsNullOrWhiteSpace(Mod!.ModId)) {
				try {
					var url = $"https://mods.vintagestory.at/api/mod/{Mod.ModId}";
					var result = await url.GetStringAsync();
					var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
					if (status?.StatusCode != "200" || !IsInstanceValid(this)) {
						return;
					}

					ApiModInfo = status.Mod!;
					Dispatcher.SynchronizationContext.Post(_ => {
							if (_modName == null || !IsInstanceValid(this)) {
								return;
							}

							_webButton.Disabled = false;
							_releaseButton.Disabled = false;
							_modName.Text = Mod!.Name.Equals(ApiModInfo.Name, StringComparison.Ordinal)
								? Mod.Name
								: $"{ApiModInfo.Name} ({Mod.Name})";
						},
						null);

					UpdateApiModRelease();
				} catch (Exception e) {
					GD.PrintErr(e);
				}
			}
		});
	}

	public void UpdateApiModRelease() {
		foreach (var modInfoRelease in ApiModInfo!.Releases) {
			try {
				if (!IsInstanceValid(this)) {
					return;
				}

				var version1 = SemVer.Parse(Mod!.Version);
				var version2 = SemVer.Parse(modInfoRelease.ModVersion);
				if (version1 >= version2) {
					return;
				}

				if (string.IsNullOrWhiteSpace(modInfoRelease.MainFile)) {
					continue;
				}

				HasNewVersion = true;
				ApiModRelease = modInfoRelease;
				GD.Print(
					$"找到 {ApiModInfo.Name} {modInfoRelease.ModVersion} ({Mod.Version}) ({modInfoRelease.Tags.Stringify()})");
				if (!modInfoRelease.Tags.Any(gameVersion =>
					GameVersion.ComparerVersion(Mod.ModpackConfig!.Version!.Value, new(gameVersion)) >= 0)) {
					continue;
				}

				CanUpdate = true;
				Dispatcher.SynchronizationContext.Post(_ => {
						if (!IsInstanceValid(this)) return;
						_updateButton!.Disabled = false;
						_updateButton!.Modulate = Colors.Green;
						HasAutoUpdate?.Invoke(this);
					},
					null);
				return;
			} catch (Exception e) {
				GD.PrintErr(e.Message);
			}
		}
	}
}
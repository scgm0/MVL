using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
	public RichTextLabel? ModName { get; set; }

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

	public override void _Ready() {
		_icon.NotNull();
		ModName.NotNull();
		_version.NotNull();
		_description.NotNull();
		_updateButton.NotNull();
		_progressBar.NotNull();

		_icon!.Texture = Mod?.Icon;
		ModName!.Text = Mod?.Name;
		_version!.Text = $"{Mod?.ModId}-{Mod?.Version}";
		_description!.Text = Mod?.Description;
		_webButton!.Disabled = true;
		_updateButton!.Disabled = true;
		_updateButton!.Modulate = Colors.White;

		_webButton!.Pressed += WebButtonOnPressed;
		_updateButton.Pressed += UpdateButtonOnPressed;
		_releaseButton!.Pressed += ReleaseButtonOnPressed;
		_deleteButton!.Pressed += DeleteButtonOnPressed;
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
			File.Delete(Mod!.ModPath);
			await Window!.ModpackItem!.UpdateMods();
			Window?.ShowList();
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

	public async Task UpdateApiModInfo() {
		_icon!.Texture = Mod?.Icon;
		ModName!.Text = Mod?.Name;
		_version!.Text = $"{Mod?.ModId}-{Mod?.Version}";
		_description!.Text = Mod?.Description;
		_webButton!.Disabled = true;
		_releaseButton!.Disabled = true;
		_updateButton!.Disabled = true;
		_updateButton!.Modulate = Colors.White;
		HasNewVersion = false;
		CanUpdate = false;
		HasAutoUpdate?.Invoke(this);

		await Task.Run(async () => {
			if (!string.IsNullOrWhiteSpace(Mod?.ModId)) {
				try {
					var url = $"https://mods.vintagestory.at/api/mod/{Mod.ModId}";
					var result = await url.GetStringAsync();
					var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
					if (status?.StatusCode != "200" || !IsInstanceValid(this)) {
						return;
					}

					ApiModInfo = status.Mod!;
					Dispatcher.SynchronizationContext.Post(_ => {
							if (ModName == null || !IsInstanceValid(this)) {
								return;
							}

							_webButton.Disabled = false;
							_releaseButton.Disabled = false;
							ModName.Text = Mod!.Name.Equals(ApiModInfo.Name, StringComparison.Ordinal)
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

public class ApiStatusModInfo {
	public required string StatusCode { get; init; }
	public ApiModInfo? Mod { get; init; }
}

public record ApiModInfo {
	public int ModId { get; init; }
	public int AssetId { get; init; }
	public string Name { get; init; } = "";
	public string Text { get; init; } = "";
	public string Author { get; init; } = "";
	public string? UrlAlias { get; init; }
	public string? LogoFileName { get; init; }
	public string? LogoFile { get; init; }
	public string? HomePageUrl { get; init; }
	public string? Sourcecodeurl { get; init; }
	public string? TrailerVideoUrl { get; init; }
	public string? IssueTrackerUrl { get; init; }
	public string? WikiUrl { get; init; }
	public int Downloads { get; init; }
	public int Follows { get; init; }
	public int TrendingPoints { get; init; }
	public int Comments { get; init; }
	public string Side { get; init; }
	public string Type { get; init; }
	public DateTimeOffset Created { get; init; }
	public DateTimeOffset LastModified { get; init; }
	public string[] Tags { get; init; } = [];
	public ApiModRelease[] Releases { get; init; } = [];
}

public record ApiModRelease {
	public int ReleaseId { get; init; }
	public string MainFile { get; init; }
	public string FileName { get; init; }
	public int FileId { get; init; }
	public int Downloads { get; init; }
	public string[] Tags { get; init; }
	public string ModIdStr { get; init; }
	public string ModVersion { get; init; }
	public DateTimeOffset Created { get; init; }
}

public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset> {
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return DateTimeOffset.Parse(reader.GetString() ?? string.Empty);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}
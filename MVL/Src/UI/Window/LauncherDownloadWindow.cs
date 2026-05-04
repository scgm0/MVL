using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CSemVer;
using Godot;
using MVL.Utils;
using MVL.Utils.Downloader;
using MVL.Utils.Extensions;
using MVL.Utils.GitHub;
using MVL.Utils.Help;
using HttpClient = System.Net.Http.HttpClient;

namespace MVL.UI.Window;

public partial class LauncherDownloadWindow : BaseWindow {
	[Export]
	private TabContainer? _tabContainer;

	[Export]
	private RichTextLabel? _chineseRichTextLabel;

	[Export]
	private RichTextLabel? _englishRichTextLabel;

	[Export]
	private ColorRect? _loadingColorRect;

	[Export]
	private Label? _errorLabel;

	[Export]
	private Button? _refreshButton;

	[Export]
	private FileDialog? _fileDialog;

	[Export]
	private ProgressBar? _progressBar;

	[Export]
	private Label? _progressLabel;

	[Export]
	private CheckButton? _usePortableCheckButton;

	private CancellationTokenSource? _cancellationTokenSource;
	private ApiRelease? _apiRelease;

#if GODOT_LINUXBSD
	private const string FileExtension = ".AppImage";
	private const string SystemName = "linux";
#elif GODOT_WINDOWS
	private const string FileExtension = ".exe";
	private const string SystemName = "windows";
#endif
	private ApiAsset? ApiAsset {
		get {
			if (_apiRelease == null) {
				return null;
			}

			foreach (var apiAsset in _apiRelease.Value.Assets) {
				if (_usePortableCheckButton!.ButtonPressed &&
					apiAsset.Name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) ||
					!_usePortableCheckButton!.ButtonPressed &&
					apiAsset.Name.Contains(SystemName, StringComparison.OrdinalIgnoreCase)) {
					return apiAsset;
				}
			}

			return null;
		}
	}

	public event Action<ApiRelease>? OnGetLatestRelease;

	public override void _Ready() {
		base._Ready();
		_tabContainer.NotNull();
		_chineseRichTextLabel.NotNull();
		_englishRichTextLabel.NotNull();
		_loadingColorRect.NotNull();
		_errorLabel.NotNull();
		_refreshButton.NotNull();
		_fileDialog.NotNull();
		_progressBar.NotNull();
		_progressLabel.NotNull();
		_usePortableCheckButton.NotNull();

		_fileDialog.CurrentDir = Paths.WorkingFolder;
		_chineseRichTextLabel.MetaClicked += Tools.RichTextOpenUrl;
		_englishRichTextLabel.MetaClicked += Tools.RichTextOpenUrl;
		_refreshButton.Pressed += () => GetLatestRelease();
		_fileDialog.FileSelected += FileDialogOnFileSelected;
		_usePortableCheckButton.Toggled += UsePortableCheckButtonOnToggled;
		OkButton!.Pressed += OnPressed;
		CancelButton!.Pressed += () => {
			_cancellationTokenSource?.Cancel();
			CancelButtonOnPressed();
		};
		Hidden += () => {
			_cancellationTokenSource?.Dispose();
			QueueFree();
		};
	}

	private void UsePortableCheckButtonOnToggled(bool toggledOn) {
		if (ApiAsset is null) {
			OkButton!.Disabled = true;
			OkButton.TooltipText = "没有找到可用的下载文件";
		} else {
			OkButton!.Disabled = false;
			OkButton.TooltipText = "";
		}
	}

	private async void FileDialogOnFileSelected(string path) {
		_tabContainer!.Visible = false;
		_refreshButton!.Disabled = true;
		OkButton!.Disabled = true;
		_loadingColorRect!.Visible = true;
		_progressBar!.Visible = true;

		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = new();

		try {
			Log.Info($"开始下载 {ApiAsset!.Value.Name}");
			using var download = new LightDownloader(new() {
				ChunkCount = Main.BaseConfig.DownloadThreads,
				ParallelCount = Main.BaseConfig.DownloadThreads,
				Proxy = string.IsNullOrWhiteSpace(Main.BaseConfig.ProxyAddress)
					? HttpClient.DefaultProxy
					: new WebProxy(Main.BaseConfig.ProxyAddress)
			});
			download.ProgressChanged += progress => {
				UpdateProgress(progress.Percentage, (ulong)progress.SpeedBytesPerSecond);
			};

			await download.DownloadAsync(ApiAsset!.Value.BrowserDownloadUrl,
				path,
				_cancellationTokenSource.Token);
		} catch (OperationCanceledException) {
			Log.Info("下载取消");
			return;
		} catch (Exception e) {
			Log.Error("下载失败", e);
			_errorLabel!.Text = "发生错误，请检查网络连接";
			_errorLabel!.Visible = true;
			_progressBar!.Visible = false;
			_loadingColorRect!.Visible = false;
			return;
		}

		Log.Info("下载完成");
		OS.ShellOpen(path.GetBaseDir());
		await Hide();
	}

	private void UpdateProgress(double percentage, ulong speed) {
		_progressBar!.Value = percentage;
		var (fmtSpeed, unit) = Tools.GetSizeAndUnit(speed);
		_progressLabel!.Text = $"{fmtSpeed:F2} {unit}/s";
	}

	private void OnPressed() {
		_fileDialog!.CurrentFile = ApiAsset!.Value.Name;
		_fileDialog.Popup();
	}

	public async void GetLatestRelease(ApiRelease? apiRelease = null, bool onlyCheck = false) {
		TitleLabel!.Text = "正在获取最新版本";
		_tabContainer!.Visible = false;
		_loadingColorRect!.Visible = true;
		_errorLabel!.Visible = false;
		_progressBar!.Visible = false;
		_usePortableCheckButton!.Visible = false;
		_chineseRichTextLabel!.Clear();
		_englishRichTextLabel!.Clear();
		OkButton!.Disabled = true;
		OkButton.TooltipText = "";
		_refreshButton!.Disabled = true;
		if (_cancellationTokenSource != null) {
			await _cancellationTokenSource.CancelAsync();
			_cancellationTokenSource.Dispose();
		}

		_cancellationTokenSource = new();

		try {
			_apiRelease = apiRelease;
			if (_apiRelease is null) {
				Log.Debug("正在获取最新版本");
				_apiRelease = await GitHubTool.GetLatestReleaseAsync("scgm0/MVL",
					Main.BaseConfig.GitHubProxy,
					_cancellationTokenSource.Token);
			}

			OnGetLatestRelease?.Invoke(_apiRelease.Value);
			UpdateContent(onlyCheck);
		} catch (Exception e) {
			if (_cancellationTokenSource.IsCancellationRequested ||
				e is { InnerException: TaskCanceledException } or TaskCanceledException) {
				return;
			}

			TitleLabel!.Text = "获取最新版本失败";
			_errorLabel!.Visible = true;
			_errorLabel!.Text = "发生错误，请检查网络连接";
			Log.Error("获取最新版本失败", e);
		} finally {
			if (!_cancellationTokenSource.IsCancellationRequested && IsInstanceValid(this)) {
				_loadingColorRect!.Visible = false;
				_refreshButton!.Disabled = false;
			}

			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;
		}
	}

	private void UpdateContent(bool onlyCheck = false) {
		if (_cancellationTokenSource?.IsCancellationRequested is true || !IsInstanceValid(this)) {
			return;
		}

		var version = SVersion.Parse(_apiRelease!.Value.TagName);
		Log.Debug($"最新版本: {version} 当前版本: {BuildInfo.InformationalVersion}");
		if (onlyCheck && version <= BuildInfo.InformationalVersion) {
			CancelButtonOnPressed();
			return;
		}

		TitleLabel!.Text = $"MVL {version} ({Tr(version > BuildInfo.InformationalVersion ? "发现新版本" : "无新版本")})";

		var body = _apiRelease!.Value.Body.SplitAndConvert();
		_chineseRichTextLabel!.AppendText(body.Chinese.ConvertMarkdownToBbcode());
		_englishRichTextLabel!.AppendText(body.English.ConvertMarkdownToBbcode());

		_tabContainer!.Visible = true;
		_loadingColorRect!.Visible = false;
		_refreshButton!.Disabled = false;
		_usePortableCheckButton!.Visible = true;

		if (ApiAsset is null) {
			OkButton!.Disabled = true;
			OkButton.TooltipText = "没有找到可用的下载文件";
		} else {
			OkButton!.Disabled = false;
			OkButton.TooltipText = "";
		}
	}
}
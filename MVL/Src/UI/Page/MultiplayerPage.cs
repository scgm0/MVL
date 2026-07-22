using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Downloader;
using MVL.Utils.GitHub;
using MVL.Utils.Help;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Page;

public partial class MultiplayerPage : MenuPage {
	[Export]
	private PackedScene? _playerItemScene;

	[Export]
	private IconTexture2D? _removeIcon;

	[Export]
	private Button? _createButton;

	[Export]
	private Button? _joinButton;

	[Export]
	private Button? _resetButton;

	[Export]
	private Button? _downloadButton;

	[Export]
	private LineEdit? _codeLineEdit;

	[Export]
	private RichTextLabel? _tooltip;

	[Export]
	private Label? _versionLabel;

	[Export]
	private PanelContainer? _roomInfoPanel;

	[Export]
	private Label? _playerCountLabel;

	[Export]
	private VBoxContainer? _playerListContainer;

	[Export]
	private SpinBox? _lanPortSpinBox;

	[Export]
	private LineEdit? _customNodeInput;

	[Export]
	private Button? _addNodeButton;

	[Export]
	private VBoxContainer? _nodeListContainer;

	[Export]
	private TextureRect? _connectionIndicator;

	[Export]
	private PanelContainer? _nodePanel;

	[Export]
	private Label? _statusLabel;

	[Export]
	private Label? _statusDetailLabel;

	[Export]
	private Button? _openFolderButton;

	[Export]
	private Button? _officialSiteButton;

	[Export]
	private Label? _customNodeSectionLabel;

	[Export]
	private Label? _noCustomNodeLabel;

	[Export]
	private VBoxContainer? _customNodeRowsContainer;

	[Export]
	private Label? _presetSectionLabel;

	private UiState _uiState = UiState.None;
	private Room? _room;
	private bool _isHost;
	private string? _coreVersion;
	private string? _cliVersion;
	private CancellationTokenSource? _cancellationTokenSource;
	private Tween? _indicatorTween;
	private readonly Dictionary<uint, PlayerItem> _playerRows = new();

	private enum UiState {
		None,
		Checking,
		EasyTierNotInstalled,
		EasyTierDownloading,
		EasyTierInstalled,
		NoAccount,
		HostPreCheck,
		Connecting,
		Connected,
		Error,
	}

	public override void _Ready() {
		_playerItemScene.NotNull();
		_removeIcon.NotNull();
		_createButton.NotNull();
		_joinButton.NotNull();
		_resetButton.NotNull();
		_downloadButton.NotNull();
		_codeLineEdit.NotNull();
		_tooltip.NotNull();
		_versionLabel.NotNull();
		_roomInfoPanel.NotNull();
		_playerCountLabel.NotNull();
		_playerListContainer.NotNull();
		_lanPortSpinBox.NotNull();
		_customNodeInput.NotNull();
		_addNodeButton.NotNull();
		_nodeListContainer.NotNull();
		_connectionIndicator.NotNull();
		_nodePanel.NotNull();
		_statusLabel.NotNull();
		_statusDetailLabel.NotNull();
		_openFolderButton.NotNull();
		_officialSiteButton.NotNull();
		_playerItemScene.NotNull();
		_customNodeSectionLabel.NotNull();
		_noCustomNodeLabel.NotNull();
		_customNodeRowsContainer.NotNull();
		_presetSectionLabel.NotNull();
		base._Ready();

		_createButton.Pressed += HostButtonOnPressed;
		_joinButton.Pressed += JoinButtonOnPressed;
		_resetButton.Pressed += ResetButtonOnPressed;
		_downloadButton.Pressed += DownloadButtonOnPressed;
		_customNodeInput.TextChanged += _ => UpdateAddButtonState();
		_customNodeInput.TextSubmitted += _ => TryAddCustomNode();
		_addNodeButton.Pressed += TryAddCustomNode;
		_openFolderButton.Pressed += OpenEasyTierFolder;
		_officialSiteButton.Pressed += OpenEasyTierSite;
		_codeLineEdit.TextChanged += OnCodeLineEditTextChanged;
		_codeLineEdit.TextSubmitted += OnCodeLineEditTextSubmitted;

		VisibilityChanged += OnVisibilityChanged;
		Tools.SceneTree.Root.TreeExiting += OnExit;

		foreach (var node in EasyTier.FallbackServers) {
			var label = new Label {
				Text = node,
				MouseFilter = MouseFilterEnum.Pass,
				ClipText = true,
				LabelSettings = new() { FontSize = 12, FontColor = new(0.55f, 0.55f, 0.55f) }
			};
			label.AddThemeConstantOverride("margin_left", 4);
			_nodeListContainer!.AddChild(label);
		}
	}

	private void ResetVisibility() {
		_createButton!.Visible = false;
		_joinButton!.Visible = false;
		_resetButton!.Visible = false;
		_downloadButton!.Visible = false;
		_downloadButton.Disabled = false;
		_roomInfoPanel!.Visible = false;
		_lanPortSpinBox!.GetParent<Control>()!.Visible = false;
		_codeLineEdit!.Visible = false;
		_codeLineEdit.Editable = false;
		_nodePanel!.Visible = false;
		_connectionIndicator!.Visible = false;
	}

	private void SetState(UiState state) {
		_uiState = state;
		UI.Main.AccountLocked = false;
		ResetVisibility();

		switch (state) {
			case UiState.None:
				_joinButton!.Disabled = true;
				_createButton!.Disabled = false;
				_tooltip!.Text = string.Empty;
				_versionLabel!.Text = "版本信息";
				_statusLabel!.Text = "";
				_statusDetailLabel!.Text = string.Empty;
				break;

			case UiState.Checking:
				_joinButton!.Disabled = true;
				_createButton!.Disabled = true;
				_tooltip!.Text = "正在检查 EasyTier 环境...";
				_tooltip.Modulate = Colors.DarkGray;
				_versionLabel!.Text = "正在检查...";
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.DarkGray;
				_statusLabel!.Text = "正在检查环境...";
				_statusLabel.Modulate = Colors.DarkGray;
				_statusDetailLabel!.Text = string.Empty;
				break;

			case UiState.EasyTierNotInstalled:
				_joinButton!.Disabled = true;
				_createButton!.Disabled = true;
				_tooltip!.Text = "EasyTier 未安装，请下载后使用联机功能";
				_tooltip.Modulate = Colors.LightCoral;
				_versionLabel!.Text = "未安装";
				_downloadButton!.Visible = true;
				_downloadButton.Text = "下载 EasyTier";
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.LightCoral;
				_statusLabel!.Text = "未安装";
				_statusLabel.Modulate = Colors.LightCoral;
				_statusDetailLabel!.Text = string.Empty;
				break;

			case UiState.EasyTierDownloading:
				_joinButton!.Disabled = true;
				_createButton!.Disabled = true;
				_tooltip!.Text = "正在下载 EasyTier...";
				_tooltip.Modulate = Colors.Gold;
				_downloadButton!.Text = "取消下载";
				_downloadButton.Visible = true;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.Gold;
				_statusLabel!.Text = "正在下载...";
				_statusLabel.Modulate = Colors.Gold;
				_statusDetailLabel!.Text = string.Empty;
				break;

			case UiState.EasyTierInstalled:
				_joinButton!.Disabled = false;
				_createButton!.Disabled = false;
				_createButton.Text = "创建房间";
				_tooltip!.Text = "输入房间码或创建新房间";
				_tooltip.Modulate = Colors.DarkGray;
				_versionLabel!.Text = $"Core: {_coreVersion}\nCLI: {_cliVersion}";
				_createButton.Visible = true;
				_joinButton.Visible = true;
				_codeLineEdit!.Visible = true;
				_codeLineEdit.Editable = true;
				_codeLineEdit.GrabFocus(true);
				_nodePanel!.Visible = true;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.DarkGray;
				_statusLabel!.Text = "已就绪";
				_statusLabel.Modulate = Colors.DarkGray;
				_statusDetailLabel!.Text = GetNodeCountText();
				RefreshNodeList();
				CheckAccount();
				break;

			case UiState.NoAccount:
				_tooltip!.Text = "使用多人联机前请先登录账号";
				_tooltip.Modulate = Colors.LightCoral;
				_resetButton!.Text = "返回";
				_resetButton.Visible = true;
				RefreshNodeList();
				break;

			case UiState.HostPreCheck:
				UI.Main.AccountLocked = true;
				_joinButton!.Visible = false;
				_createButton!.Text = "确认创建";
				_createButton.Disabled = false;
				_createButton.Visible = true;
				_lanPortSpinBox!.GetParent<Control>()!.Visible = true;
				_resetButton!.Text = "返回";
				_resetButton.Visible = true;
				_codeLineEdit!.Editable = false;
				_tooltip!.Text = "请输入局域网端口并点击确认创建";
				_tooltip.Modulate = Colors.Gold;
				_nodePanel!.Visible = true;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.Gold;
				_statusLabel!.Text = "等待创建";
				_statusLabel.Modulate = Colors.Gold;
				_statusDetailLabel!.Text = GetNodeCountText();
				RefreshNodeList();
				break;

			case UiState.Connecting:
				UI.Main.AccountLocked = true;
				_joinButton!.Disabled = true;
				_createButton!.Disabled = true;
				_tooltip!.Text = "正在连接...";
				_tooltip.Modulate = Colors.Gold;
				_codeLineEdit!.Editable = false;
				_resetButton!.Text = "取消";
				_resetButton.Visible = true;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.Gold;
				_statusLabel!.Text = "连接中...";
				_statusLabel.Modulate = Colors.Gold;
				_statusDetailLabel!.Text = "正在连接中继网络";
				StartIndicatorPulse();
				break;

			case UiState.Connected:
				UI.Main.AccountLocked = true;
				_roomInfoPanel!.Visible = true;
				_resetButton!.Text = _isHost ? "关闭房间" : "退出房间";
				_resetButton.Visible = true;
				_codeLineEdit!.Visible = true;
				_codeLineEdit.Editable = false;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.MediumSeaGreen;
				_statusLabel!.Text = "已连接";
				_statusLabel.Modulate = Colors.MediumSeaGreen;
				_statusDetailLabel!.Text = GetNodeCountText();
				StopIndicatorPulse();
				SyncPlayerList();
				break;

			case UiState.Error:
				_resetButton!.Text = "返回";
				_resetButton.Visible = true;
				_connectionIndicator!.Visible = true;
				_connectionIndicator.Modulate = Colors.LightCoral;
				_statusLabel!.Text = "连接失败";
				_statusLabel.Modulate = Colors.LightCoral;
				_statusDetailLabel!.Text = string.Empty;
				StopIndicatorPulse();
				break;
		}
	}

	private void SyncPlayerList() {
		var players = _room?.GetPlayersSnapshot();
		if (players is not { Count: > 0 }) {
			foreach (var kvp in _playerRows) {
				kvp.Value.QueueFree();
			}

			_playerCountLabel!.Text = string.Format(Tr("当前在线玩家: {0} 人"), 0);
			return;
		}

		_playerCountLabel!.Text = string.Format(Tr("当前在线玩家: {0} 人"), players.Count);

		var nameStats = new Dictionary<string, (int Count, bool HasOffline)>();
		foreach (var p in players) {
			nameStats.TryGetValue(p.Name, out var stat);
			nameStats[p.Name] = (stat.Count + 1, stat.HasOffline || p.Offline);
		}

		var currentIds = new HashSet<uint>();
		foreach (var player in players) {
			currentIds.Add(player.Identity);
			var stat = nameStats[player.Name];
			var showWarning = stat is { Count: >= 2, HasOffline: true };

			if (_playerRows.TryGetValue(player.Identity, out var existingRow)) {
				existingRow.Update(player, showWarning);
			} else {
				var item = _playerItemScene!.Instantiate<PlayerItem>();
				item.Update(player, showWarning);
				_playerListContainer!.AddChild(item);
				_playerRows[player.Identity] = item;
			}
		}

		var idsToRemove = _playerRows.Keys.Where(k => !currentIds.Contains(k)).ToArray();
		foreach (var id in idsToRemove) {
			_playerRows[id].QueueFree();
			_playerRows.Remove(id);
		}
	}

	private void RefreshPlayerLatency() {
		foreach (var kvp in _playerRows) {
			kvp.Value.UpdatePing();
		}
	}

	static private bool HasValidAccount() {
		var currentAccount = UI.Main.BaseConfig.CurrentAccount;
		return !string.IsNullOrEmpty(currentAccount) && UI.Main.Accounts.ContainsKey(currentAccount);
	}

	private void CheckAccount() {
		if (!HasValidAccount()) {
			SetState(UiState.NoAccount);
			return;
		}

		if (_uiState != UiState.EasyTierInstalled) {
			SetState(UiState.EasyTierInstalled);
			return;
		}

		UpdateJoinButtonState();
	}

	private async Task CheckLanAndCreate() {
		_createButton!.Disabled = true;
		_createButton.Text = "检测中...";

		var port = (ushort)_lanPortSpinBox!.Value;
		bool valid;
		try {
			using var client = new TcpClient();
			await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(2));
			valid = true;
		} catch {
			valid = false;
		}

		if (valid) {
			_tooltip!.Text = "端口检测通过，正在创建房间...";
			_tooltip.Modulate = Colors.MediumSeaGreen;
			StartHostRoom();
		} else {
			_tooltip!.Text = "端口无法连接，请先在游戏内开启局域网联机";
			_tooltip.Modulate = Colors.LightCoral;
			_createButton.Text = "确认创建";
			_createButton.Disabled = false;
		}
	}

	private void UpdateJoinButtonState() {
		if (_uiState != UiState.EasyTierInstalled) {
			return;
		}

		var code = _codeLineEdit!.Text;
		_joinButton!.Disabled = !Room.IsValidCode(code);
		_joinButton.TooltipText = _joinButton.Disabled ? "请输入有效的房间码" : string.Empty;
	}

	private void OnCodeLineEditTextChanged(string newText) => UpdateJoinButtonState();

	private void OnCodeLineEditTextSubmitted(string newText) {
		if (_uiState == UiState.EasyTierInstalled && _joinButton is { Disabled: false }) {
			JoinButtonOnPressed();
		}
	}

	private void OnVisibilityChanged() {
		if (!Visible) {
			_cancellationTokenSource?.Cancel();
			return;
		}

		if (_uiState == UiState.NoAccount) {
			CheckAccount();
			return;
		}

		if (_room != null && _uiState == UiState.Connected) {
			SyncPlayerList();
			return;
		}

		if (_room != null) {
			return;
		}

		_ = CheckVersion();
	}

	private async Task CheckVersion(bool showChecking = true) {
		if (showChecking) SetState(UiState.Checking);
		_coreVersion = await EasyTier.GetCoreVersion();
		_cliVersion = await EasyTier.GetCliVersion();
		SetState(_coreVersion == null || _cliVersion == null
			? UiState.EasyTierNotInstalled
			: UiState.EasyTierInstalled);
	}

	private void JoinButtonOnPressed() {
		_isHost = false;
		var room = Room.Parse(_codeLineEdit!.Text);
		if (room != null) {
			AttachRoom(room);
			_room!.StartGuest();
		} else {
			_tooltip!.Text = "房间码解析失败";
			_tooltip.Modulate = Colors.LightCoral;
		}
	}

	private async void HostButtonOnPressed() {
		if (_uiState == UiState.HostPreCheck) {
			await CheckLanAndCreate();
			return;
		}

		SetState(UiState.HostPreCheck);
	}

	private void AttachRoom(Room room) {
		_room = room;
		SetState(UiState.Connecting);
		_room.OnStateChanged += OnRoomStateChanged;
		_room.OnPlayerListChanged += OnPlayerListChanged;
	}

	private void StartHostRoom() {
		_isHost = true;
		var port = (ushort)_lanPortSpinBox!.Value;
		var room = Room.Create(port, "mvl");
		AttachRoom(room);
		_room!.StartHost();
	}

	private void OnPlayerListChanged() {
		Dispatcher.SynchronizationContext.Post(_ => {
				if (_uiState == UiState.Connected) {
					SyncPlayerList();
				}
			},
			null);
	}

	private void OnRoomStateChanged(RoomStateChange change) {
		Dispatcher.SynchronizationContext.Post(_ => {
				switch (change.State) {
					case RoomState.Connecting:
						_tooltip!.Text = "正在连接 EasyTier 中继网络...";
						_tooltip.Modulate = Colors.Gold;
						break;

					case RoomState.EasyTierReady:
						_tooltip!.Text = "已连接中继网络，正在建立房间...";
						_tooltip.Modulate = Colors.LightSkyBlue;
						break;

					case RoomState.RoomJoining:
						_tooltip!.Text = "正在加入房间...";
						_tooltip.Modulate = Colors.LightSkyBlue;
						break;

					case RoomState.Ready:
						SetState(UiState.Connected);
						if (_isHost) {
							_codeLineEdit!.Text = _room!.Code;
							_tooltip!.Text = "房间已创建，请复制房间码给其他人用于加入房间";
						} else {
							_tooltip!.Text = $"已进入房间，请在游戏中添加服务器: 127.0.0.1:{_room!.LocalPort}";
						}

						_tooltip.Modulate = Colors.White;
						SyncPlayerList();
						break;

					case RoomState.Failed:
						SetState(UiState.Error);
						_tooltip!.Text = change.ErrorMessage ?? "连接失败";
						_tooltip.Modulate = Colors.LightCoral;
						ResetRoom();
						break;

					case RoomState.Disconnected:
						SetState(UiState.Error);
						_tooltip!.Text = "主机已关闭房间";
						_tooltip.Modulate = Colors.LightCoral;
						ResetRoom();
						break;
				}
			},
			null);
	}

	private void ResetRoom() {
		if (_room != null) {
			_room.OnStateChanged -= OnRoomStateChanged;
			_room.OnPlayerListChanged -= OnPlayerListChanged;
			_room.Dispose();
			_room = null;
		}

		foreach (var row in _playerRows.Values) {
			row.QueueFree();
		}

		_playerRows.Clear();

		_playerCountLabel!.Text = string.Format(Tr("当前在线玩家: {0} 人"), 0);
		foreach (var child in _playerListContainer!.GetChildren()) {
			child.QueueFree();
		}
	}

	private void ResetButtonOnPressed() {
		if (_uiState == UiState.HostPreCheck) {
			SetState(UiState.EasyTierInstalled);
			return;
		}

		if (_uiState == UiState.Connected) {
			ResetRoom();
			_isHost = false;
			_codeLineEdit!.Text = string.Empty;
			SetState(UiState.None);
			_ = CheckVersion();
			return;
		}

		if (_uiState == UiState.Connecting) {
			ResetRoom();
			_isHost = false;
			_codeLineEdit!.Text = string.Empty;
			SetState(UiState.EasyTierInstalled);
			return;
		}

		if (_uiState == UiState.NoAccount) {
			CheckAccount();
			return;
		}

		_cancellationTokenSource?.Cancel();
		ResetRoom();
		_isHost = false;
		_codeLineEdit!.Text = string.Empty;
		_ = CheckVersion();
	}

	private async void DownloadButtonOnPressed() {
		if (_uiState == UiState.EasyTierDownloading) {
			if (_cancellationTokenSource != null) {
				await _cancellationTokenSource.CancelAsync();
			}

			return;
		}

		SetState(UiState.EasyTierDownloading);
		if (_cancellationTokenSource != null) {
			await _cancellationTokenSource.CancelAsync();
			_cancellationTokenSource.Dispose();
		}

		_cancellationTokenSource = new();
		var token = _cancellationTokenSource.Token;

		try {
			var downloadUrl = await GetEasyTierLatestFileAsync();
			if (string.IsNullOrEmpty(downloadUrl)) {
				SetState(UiState.EasyTierNotInstalled);
				_tooltip!.Text = "未找到适用于当前系统的 EasyTier 版本";
				return;
			}

			var fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
			var downloadPath = Paths.EasyTierFolder.PathJoin(fileName);

			using var downloader = new LightDownloader(new() {
				ChunkCount = UI.Main.BaseConfig.DownloadThreads,
				ParallelCount = UI.Main.BaseConfig.DownloadThreads,
				Proxy = string.IsNullOrWhiteSpace(UI.Main.BaseConfig.ProxyAddress)
					? null
					: new WebProxy(UI.Main.BaseConfig.ProxyAddress),
				UserAgent = Tools.UserAgent
			});

			downloadUrl = GitHubTool.GetProxyRequestUrl(UI.Main.BaseConfig.GitHubProxy) + downloadUrl;

			downloader.ProgressChanged += progress => {
				Dispatcher.SynchronizationContext.Post(_ => {
						var (fmtSpeed, unit) = Tools.GetSizeAndUnit((ulong)progress.SpeedBytesPerSecond);
						_statusDetailLabel!.Text = $"已下载 {progress.Percentage:F1}%  {fmtSpeed:F2} {unit}/s";
					},
					null);
			};

			await downloader.DownloadAsync(downloadUrl, downloadPath, token);

			_tooltip!.Text = "正在解压 EasyTier...";
			_tooltip.Modulate = Colors.Gold;
			_downloadButton!.Disabled = true;
			_downloadButton!.Text = "解压中...";

			await Task.Run(() => {
					var tmpDir = Paths.EasyTierFolder.PathJoin("_tmp_extract");
					try {
						if (Directory.Exists(tmpDir)) {
							Directory.Delete(tmpDir, true);
						}

						ZipFile.ExtractToDirectory(downloadPath, tmpDir);

						foreach (var file in Directory.GetFiles(tmpDir, "*", SearchOption.AllDirectories)) {
							var name = Path.GetFileName(file);
							var destPath = Paths.EasyTierFolder.PathJoin(name);
							File.Move(file, destPath, overwrite: true);
						}

						if (Directory.Exists(tmpDir)) {
							Directory.Delete(tmpDir, true);
						}
					} catch (Exception e) {
						Log.Error(e);
					} finally {
						if (Directory.Exists(tmpDir)) {
							Directory.Delete(tmpDir, true);
						}
					}
				},
				token);

			try {
				File.Delete(downloadPath);
			} catch {
				/* ignored */
			}

#if GODOT_LINUXBSD
			using var process = Process.Start(new ProcessStartInfo("chmod") {
				Arguments = $"+x \"{EasyTier.CorePath}\" \"{EasyTier.CliPath}\"",
				UseShellExecute = false, CreateNoWindow = true
			});
			process?.WaitForExitAsync(token);
#endif

			await CheckVersion(false);
		} catch (OperationCanceledException) {
			SetState(UiState.EasyTierNotInstalled);
			_tooltip!.Text = "下载已取消";
		} catch (Exception e) {
			Log.Error("EasyTier 下载失败", e);
			SetState(UiState.EasyTierNotInstalled);
			_tooltip!.Text = "下载失败，请检查网络连接";
		} finally {
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
		}
	}

	private void OnExit() {
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		ResetRoom();
	}

	public static async Task<string?> GetEasyTierLatestFileAsync() {
		var latestRelease = await GitHubTool.GetLatestReleaseAsync("EasyTier/EasyTier");
		foreach (var asset in latestRelease.Assets) {
#if GODOT_LINUXBSD
			if (asset.Name.StartsWith("easytier-linux-x86_64")) {
				return asset.BrowserDownloadUrl;
			}
#elif GODOT_WINDOWS
			if (asset.Name.StartsWith("easytier-windows-x86_64")) {
				return asset.BrowserDownloadUrl;
			}
#endif
		}

		return null;
	}

	private void UpdateAddButtonState() { _addNodeButton!.Disabled = string.IsNullOrWhiteSpace(_customNodeInput!.Text); }

	private void TryAddCustomNode() {
		var text = _customNodeInput!.Text.Trim();
		if (string.IsNullOrEmpty(text)) {
			return;
		}

		if (!Uri.TryCreate(text, UriKind.Absolute, out _)) {
			_tooltip!.Text = "节点地址格式无效";
			_tooltip.Modulate = Colors.LightCoral;
			return;
		}

		if (UI.Main.BaseConfig.CustomEasyTierNodes.Contains(text)) {
			_tooltip!.Text = "该节点已存在";
			_tooltip.Modulate = Colors.Gold;
			return;
		}

		_tooltip!.Text = string.Empty;
		UI.Main.BaseConfig.CustomEasyTierNodes.Add(text);
		UI.Main.BaseConfig.SaveAsync();
		_customNodeInput.Clear();
		UpdateAddButtonState();
		RefreshNodeList();
		_statusDetailLabel!.Text = GetNodeCountText();
	}

	private void RefreshNodeList() {
		foreach (var child in _customNodeRowsContainer!.GetChildren()) {
			child.QueueFree();
		}

		var hasCustomNodes = UI.Main.BaseConfig.CustomEasyTierNodes.Count > 0;
		_noCustomNodeLabel!.Visible = !hasCustomNodes;

		foreach (var node in UI.Main.BaseConfig.CustomEasyTierNodes) {
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var label = new Label {
				Text = node,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Pass,
				ClipText = true
			};
			label.AddThemeConstantOverride("margin_left", 4);
			row.AddChild(label);

			var removeBtn = new Button {
				Icon = _removeIcon,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			removeBtn.Pressed += () => {
				UI.Main.BaseConfig.CustomEasyTierNodes.Remove(node);
				UI.Main.BaseConfig.SaveAsync();
				RefreshNodeList();
			};
			row.AddChild(removeBtn);

			_customNodeRowsContainer.AddChild(row);
		}
	}

	static private void OpenEasyTierFolder() { OS.ShellOpen(Paths.EasyTierFolder); }

	static private void OpenEasyTierSite() { OS.ShellOpen("https://github.com/EasyTier/EasyTier"); }

	private string GetNodeCountText() {
		var publicCount = EasyTier.FallbackServers.Length;
		var customCount = UI.Main.BaseConfig.CustomEasyTierNodes.Count;
		var total = publicCount + customCount;
		return $"启用节点: {total} 个 (公共 {publicCount} + 自定义 {customCount})";
	}

	private void StartIndicatorPulse() {
		StopIndicatorPulse();
		_indicatorTween = CreateTween();
		_indicatorTween.SetLoops();
		_indicatorTween.TweenProperty(_connectionIndicator, "modulate:a", 0.3f, 0.8f);
		_indicatorTween.TweenProperty(_connectionIndicator, "modulate:a", 1.0f, 0.8f);
	}

	private void StopIndicatorPulse() {
		_indicatorTween?.Kill();
		_indicatorTween = null;
	}
}
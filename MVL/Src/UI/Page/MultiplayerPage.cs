using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.GitHub;
using MVL.Utils.Help;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Page;

public partial class MultiplayerPage : MenuPage {
	[Export]
	private Button? _createButton;

	[Export]
	private Button? _joinButton;

	[Export]
	private LineEdit? _codeLineEdit;

	[Export]
	private RichTextLabel? _tooltip;

	private Room? _room;

	public override void _Ready() {
		_createButton.NotNull();
		_joinButton.NotNull();
		_codeLineEdit.NotNull();
		_tooltip.NotNull();
		base._Ready();
		_createButton.Pressed += HostButtonOnPressed;
		_joinButton.Pressed += JoinButtonOnPressed;

		Tools.SceneTree.Root.TreeExiting += OnExit;
	}

	private void JoinButtonOnPressed() {
		_room = Room.Parse(_codeLineEdit!.Text);
		if (_room != null) {
			_codeLineEdit.Editable = false;
			_room.StartGuest();
			_tooltip!.Text = "正在加入房间...";
			_room.OnReady += b => {
				Dispatcher.SynchronizationContext.Post(_ =>
						_tooltip!.Text = b ? $"已进入房间，请在游戏中添加服务器，IP: 127.0.0.1:{_room.LocalPort}" : "加入房间失败",
					null);
			};
		} else {
			_tooltip!.Text = "分享码错误";
		}
	}

	private void HostButtonOnPressed() {
		_room = Room.Create(42420, "MVL");
		_codeLineEdit!.Editable = false;
		_room.StartHost();
		_tooltip!.Text = "正在创建房间...";
		_room.OnReady += b => {
			Dispatcher.SynchronizationContext.Post(_ => {
				_codeLineEdit.Text = _room.Code;
				_tooltip.Text = b ? "房间已创建，请复制分享码给他人用于加入房间" : "创建房间失败";
			}, null);
		};
	}

	private void OnExit() {
		_room?.Dispose();
	}

	public static async Task<string?> GetEasyTierLatestFileAsync() {
		var latestRelease = await GitHubTool.GetLatestReleaseAsync("EasyTier/EasyTier", GhProxyEnum.V6);
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
}
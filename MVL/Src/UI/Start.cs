using System;
using System.IO;
using System.Net;
using Godot;
using MVL.Utils;
using NetMQ;
using NetMQ.Sockets;
using FileAccess = System.IO.FileAccess;

namespace MVL.UI;

public partial class Start : Control {
	static private FileStream? _lockFile;
	static private PullSocket? _pullServer;
	static private NetMQPoller? _poller;

	public static bool IsRunning => _lockFile != null;
	public static int MvlPort { get; private set; } = -1;

	public Start() {
		try {
			_lockFile = File.Open(Paths.LockFile,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None);

			_pullServer = new();
			MvlPort = _pullServer.BindRandomPort($"tcp://{IPAddress.Any}");
			File.WriteAllTextAsync(Paths.PortFile, MvlPort.ToString());

			_pullServer.ReceiveReady += OnMessageReceived;

			_poller = new() { _pullServer };
			_poller.RunAsync();

			Main.SceneTree.Root.TreeExiting += OnExit;
		} catch (Exception) {
			_lockFile = null;
			try {
				var port  = File.ReadAllText(Paths.PortFile);
				var serverAddress = $"tcp://{IPAddress.Loopback}:{port}";
				using var pushClient = new PushSocket();
				pushClient.Options.Linger = TimeSpan.FromMilliseconds(100);
				pushClient.Connect(serverAddress);
				pushClient.SendFrame(new AppEventData(AppEventEnum.RepeatStartup).ToString());
			} catch (Exception e) {
				GD.PrintErr("发送通知失败: ", e);
			}

			Main.SceneTree.Quit();
		}
	}

	static private void OnMessageReceived(object? sender, NetMQSocketEventArgs e) {
		while (e.Socket.TryReceiveFrameBytes(out var data)) {
			var appEvent = AppEventData.Parse(data);
			Dispatcher.SynchronizationContext.Post(_ => HandleEvent(appEvent.EventCode), null);
		}
	}

	static private void HandleEvent(AppEventEnum eventCode) {
		switch (eventCode) {
			case AppEventEnum.RepeatStartup:
				OS.Alert(TranslationServer.Translate("启动器运行中，无法重复启动"), TranslationServer.Translate("警告"));
				var window = Main.SceneTree.Root;
				window.GrabFocus();
				break;
			case AppEventEnum.None:
			default:
				GD.PrintErr("收到未知的事件代码: ", eventCode);
				break;
		}
	}

	static private void OnExit() {
		_poller?.StopAsync();
		_poller?.Dispose();
		_pullServer?.Dispose();
		_poller = null;
		_pullServer = null;

		_lockFile?.Dispose();
		_lockFile = null;

		try {
			if (File.Exists(Paths.LockFile)) {
				File.Delete(Paths.LockFile);
			}

			if (File.Exists(Paths.PortFile)) {
				File.Delete(Paths.PortFile);
			}
		} catch (Exception e) {
			GD.PrintErr("清理文件失败: ", e);
		}
	}
}
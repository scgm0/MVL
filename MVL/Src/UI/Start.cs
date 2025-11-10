using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Godot;
using MVL.Utils;
using FileAccess = System.IO.FileAccess;

namespace MVL.UI;

public partial class Start : Control {
	static private FileStream? _lockFile;
	public static bool IsRunning => _lockFile != null;

	static Start() { AsyncIO.ForceDotNet.Force(); }

	public Start() {
		try {
			_lockFile = File.Open(Paths.LockFile,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None);

			var mvlPort = Tools.GetAvailablePort();
			TcpListener server = new(IPAddress.Loopback, mvlPort);
			server.Start();
			ListenForMessagesAsync(server);

			File.WriteAllBytesAsync(Paths.PortFile, BitConverter.GetBytes(mvlPort));
			Main.SceneTree.Root.TreeExiting += () => {
				_lockFile?.Close();
				_lockFile?.Dispose();
				_lockFile = null;
				server.Stop();
			};
		} catch (Exception err) {
			GD.PrintErr(err);
			_lockFile = null;
			try {
				var port = File.ReadAllBytes(Paths.PortFile);
				using var client = new TcpClient();
				client.Connect(IPAddress.Loopback, BitConverter.ToInt32(port));
				client.Client.Send(BitConverter.GetBytes((int)AppEventEnum.RepeatStartup));
			} catch (Exception e) {
				GD.PrintErr("发送通知失败: ", e);
			}

			Main.SceneTree.Quit();
		}
	}

	static private async void ListenForMessagesAsync(TcpListener server) {
		try {
			while (true) {
				using var client = await server.AcceptTcpClientAsync();
				HandleClientAsync(client);
			}
		} catch (Exception e) {
			GD.Print("TCP监听器已停止: ", e.Message);
		}
	}

	static private async void HandleClientAsync(TcpClient client) {
		try {
			using (client)
			await using (var stream = client.GetStream()) {
				var buffer = new byte[4];
				await stream.ReadExactlyAsync(buffer, 0, 4);

				var eventCode = (AppEventEnum)BitConverter.ToInt32(buffer, 0);
				HandleAppEvent(eventCode);
			}
		} catch (Exception e) {
			GD.PrintErr("处理客户端消息时出错: ", e);
		}
	}

	static private void HandleAppEvent(AppEventEnum eventCode) {
		switch (eventCode) {
			case AppEventEnum.RepeatStartup:
				Dispatcher.SynchronizationContext.Post(_ => {
						OS.Alert(TranslationServer.Translate("启动器运行中，无法重复启动"), TranslationServer.Translate("警告"));
						var window = Main.SceneTree.Root;
						window.GrabFocus();
					},
					null);
				break;
			case AppEventEnum.None:
			default:
				GD.PrintErr("收到未知的事件代码: ", eventCode);
				break;
		}
	}
}
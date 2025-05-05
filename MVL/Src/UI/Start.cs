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
	static private TcpListener? _listener;

	public static bool IsRunning => _lockFile != null;

	public Start() {
		try {
			_lockFile = File.Open(Paths.LockFile,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None);
			_listener = new(IPAddress.Loopback, 0);
			_listener.Start();

			var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
			File.WriteAllBytesAsync(Paths.PortFile, BitConverter.GetBytes(port));
			AppStartEventListen();
		} catch (Exception) {
			_lockFile = null;
			try {
				QueueFree();
				Main.SceneTree.Root.MinSize = Vector2I.Zero;
				Main.SceneTree.Root.Size = Vector2I.Zero;

				var port = BitConverter.ToInt32(File.ReadAllBytes(Paths.PortFile));
				using var client = new TcpClient();
				client.Connect(IPAddress.Loopback, port);
				using var stream = client.GetStream();
				stream.Write(BitConverter.GetBytes((int)AppEventEnum.RepeatStartup));
			} catch (Exception e) {
				GD.PrintErr(e);
			}

			Main.SceneTree.Quit();
		}
	}

	static private async void AppStartEventListen() {
		try {
			while (true) {
				using var client = await _listener!.AcceptTcpClientAsync();
				await using var stream = client.GetStream();
				using var reader = new StreamReader(stream);
				var buffer = new byte[4];
				await stream.ReadExactlyAsync(buffer, 0, 4);

				var eventCode = (AppEventEnum)BitConverter.ToInt32(buffer, 0);
				switch (eventCode) {
					case AppEventEnum.RepeatStartup:
						OS.Alert(TranslationServer.Translate("启动器运行中，无法重复启动"), TranslationServer.Translate("警告"));
						break;
					default: throw new ArgumentOutOfRangeException();
				}

				var window = Main.SceneTree.Root;
				window.GrabFocus();
			}
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}
}
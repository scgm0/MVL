using Godot;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MVL.Utils;
using FileAccess = System.IO.FileAccess;

namespace MVL.UI;

enum AppEventEnum { Focus }

public partial class Start : Control {
	static private FileStream? _lockFile;
	static private TcpListener? _listener;

	public Start() {
		try {
			_lockFile = File.Open(Paths.LockFile,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None);
			_listener = new(IPAddress.Loopback, 0);
			_listener.Start();
			var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
			File.WriteAllTextAsync(Paths.PortFile, port.ToString());
			Task.Run(AppStartEventListen);
		} catch (Exception) {
			QueueFree();
			Main.SceneTree.Root.MinSize = Vector2I.Zero;
			Main.SceneTree.Root.Size = Vector2I.Zero;
			var port = int.Parse(File.ReadAllText(Paths.PortFile));
			using var client = new TcpClient();
			client.Connect(IPAddress.Loopback, port);
			using var stream = client.GetStream();
			using var writer = new StreamWriter(stream);
			writer.Write(AppEventEnum.Focus);
			Main.SceneTree.Quit();
		}
	}

	static private async Task AppStartEventListen() {
		while (true) {
			using var client = await _listener!.AcceptTcpClientAsync();
			await using var stream = client.GetStream();
			using var reader = new StreamReader(stream);
			var str = await reader.ReadLineAsync();
			if (str != null) {
				var e = Enum.Parse<AppEventEnum>(str);
				switch (e) {
					case AppEventEnum.Focus:
						OS.Alert("启动器运行中，无法重复启动", "警告");
						break;
					default: throw new ArgumentOutOfRangeException();
				}
			}

			Dispatcher.SynchronizationContext.Post(_ => {
					var window = Main.SceneTree.Root;
					window.GrabFocus();
				},
				null);
		}
	}
}
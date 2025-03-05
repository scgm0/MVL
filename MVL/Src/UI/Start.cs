using Godot;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MVL.UI;

enum AppEventEnum { Focus }

public partial class Start : Control {
	static private TcpListener? _listener;

	public Start() {
		try {
			_listener = new(IPAddress.Loopback, 35200);
			_listener.Start();
			Task.Run(AppStartEventListen);
		} catch (Exception) {
			QueueFree();
			Main.SceneTree.Root.MinSize = Vector2I.Zero;
			Main.SceneTree.Root.Size = Vector2I.Zero;
			using var client = new TcpClient();
			client.Connect(IPAddress.Loopback, 35200);
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
			var e = Enum.Parse<AppEventEnum>(str!);
			GD.Print(e);
			Dispatcher.SynchronizationContext.Post(_ => {
					var window = Main.SceneTree.Root;
					window.GrabFocus();
				},
				null);
		}
	}
}
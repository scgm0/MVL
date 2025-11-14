using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Godot;
using MVL.Utils;
using MVL.Utils.Help;
using FileAccess = System.IO.FileAccess;

namespace MVL.UI;

public partial class Start : Control {
	[Export(PropertyHint.File, "*.tscn")]
	private string? _mainSceneFile;

	[Export]
	private MarginContainer? _margin;

	[Export]
	private SubViewport? _subViewport;

	static private TcpListener? _server;
	static private FileStream? _lockFile;
	public static bool IsRunning => _lockFile != null;

	public Start() {
		try {
			_lockFile = File.Open(Paths.LockFile,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None);

			Modulate = Colors.Transparent;
			Scale = Vector2.Zero;

			_server = new(IPAddress.Loopback, 0);
			_server.Start();
			var mvlPort = ((IPEndPoint)_server.LocalEndpoint).Port;
			ListenForMessagesAsync();

			File.WriteAllBytesAsync(Paths.PortFile, BitConverter.GetBytes(mvlPort));
			Main.SceneTree.Root.TreeExiting += () => {
				_lockFile?.Close();
				_lockFile?.Dispose();
				_lockFile = null;
				_server.Stop();
				_server?.Dispose();
				_server = null;
				File.Delete(Paths.PortFile);
			};
		} catch (Exception err) {
			if (err is not IOException) {
				GD.PrintErr(err);
			}

			_lockFile = null;
			try {
				var port = File.ReadAllBytes(Paths.PortFile);
				using var client = new TcpClient();
				client.Connect(IPAddress.Loopback, BitConverter.ToInt32(port));
				using var stream = client.GetStream();
				stream.Write(BitConverter.GetBytes((int)AppEventEnum.RepeatStartup));
				GD.PrintErr("启动器已运行，无法重复启动");
			} catch (Exception e) {
				GD.PrintErr("发送通知失败: ", e);
			}

			Main.SceneTree.Quit();
		}
	}

	public override void _Ready() {
		if (!IsRunning) {
			return;
		}

		_mainSceneFile.NotNull();
		_margin.NotNull();
		_subViewport.NotNull();

		using var mainScene = ResourceLoader.Load<PackedScene>(_mainSceneFile);
		var main = mainScene.Instantiate<Main>();
		main.Margin = _margin;
		main.Viewport = _subViewport;
		_subViewport.AddChild(main);

		using var tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.Parallel().TweenProperty(this, new(CanvasItem.PropertyName.Modulate), Colors.White, 0.5f).Dispose();
		tween.Parallel().TweenProperty(this, new(Control.PropertyName.Scale), new Vector2(1, 1), 0.5f).Dispose();
		tween.Finished += main.Init;
	}

	static private async void ListenForMessagesAsync() {
		try {
			while (true) {
				using var client = await _server!.AcceptTcpClientAsync();
				await using var stream = client.GetStream();
				var buffer = new byte[4];
				await stream.ReadExactlyAsync(buffer, 0, 4);

				var eventCode = (AppEventEnum)BitConverter.ToInt32(buffer, 0);
				switch (eventCode) {
					case AppEventEnum.RepeatStartup:
						Dispatcher.SynchronizationContext.Post(_ => {
								OS.Alert(TranslationServer.Translate("启动器运行中，无法重复启动"), TranslationServer.Translate("警告"));
								Main.SceneTree.Root.GrabFocus();
							},
							null);
						break;
					case AppEventEnum.None:
					default:
						GD.PrintErr("收到未知的事件代码: ", eventCode);
						break;
				}
			}
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}
}
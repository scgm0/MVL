using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Godot;
using MVL.UI;
using NetMQ;
using NetMQ.Sockets;

namespace MVL.Utils.Multiplayer;

public partial record Room {
	private DealerSocket? _dealerSocket;
	private EasyTierPlayerInfo? _hostEasyTierInfo;
	private NetMQTimer? _heartbeatTimer;
	private NetMQTimer? _timeoutTimer;
	private readonly TimeSpan _heartbeatIntervalMs = TimeSpan.FromSeconds(5);
	private readonly TimeSpan _heartbeatTimeoutMs = TimeSpan.FromSeconds(15);
	private volatile bool _isHostAlive = true;

	public async void StartGuest() {
		var args = await ComposeArgs();
		args.AddRange([
			"-d",
			"--tcp-whitelist=0",
			"--udp-whitelist=0"
		]);

		_easyTier = new();
		_easyTier.OnReady += OnEasyTierReadyByGuest;
		await _easyTier.Start(args);
	}

	private async void OnEasyTierReadyByGuest(bool ready) {
		if (!ready) {
			OnReady?.Invoke(false);
			return;
		}

		_isHostAlive = true;
		for (var i = 0; i < 3; i++) {
			var players = await _easyTier!.GetPlayers();
			foreach (var playerInfo in players) {
				if (!playerInfo.HostName.Equals(NetworkName, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				_hostEasyTierInfo = playerInfo;

				var localPort = Tools.GetAvailablePort();
				var local = $"{IPAddress.Any}:{localPort}";
				var remote = $"{playerInfo.IpV4}:{HostPort}";
				if (await _easyTier!.AddPortForward((local, remote, "tcp"))) {
					_poller = new();
					_poller.RunAsync("MVL-Room-Gust", true);
					_dealerSocket = new();
					_dealerSocket.ReceiveReady += DealerClientOnReceiveReady;
					_dealerSocket.Options.Linger = TimeSpan.FromMilliseconds(100);
					_dealerSocket.Options.Identity = BitConverter.GetBytes(_easyTier.LocalPlayer.Id);
					_dealerSocket.Options.HeartbeatInterval = TimeSpan.FromSeconds(10);
					_dealerSocket.Options.HeartbeatTimeout = TimeSpan.FromSeconds(10);
					_dealerSocket.Options.HeartbeatTtl = TimeSpan.FromSeconds(5);
					_dealerSocket.Connect($"tcp://{IPAddress.Loopback}:{localPort}");
					_poller.Add(_dealerSocket);

					_heartbeatTimer = new(_heartbeatIntervalMs);
					_timeoutTimer = new(_heartbeatTimeoutMs);
					_heartbeatTimer.Elapsed += (_, _) => SendPing();
					_timeoutTimer.Elapsed += (_, _) => HostTimedOut();
					_poller.Add(_heartbeatTimer);
					_poller.Add(_timeoutTimer);
					_timeoutTimer.Enable = true;

					_localPlayer = new(RoomType: RoomType.Guest,
						Name: Main.BaseConfig.CurrentAccount,
						Version: _easyTier.LocalPlayer.Version,
						Port: 0,
						Address: _easyTier.LocalPlayer.IpV4) { Identity = _easyTier.LocalPlayer.Id };

					var message = new NetMQMessage();
					message.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestJoined));
					message.Append(Tools.PackSerializer.Serialize(_localPlayer));

					if (_dealerSocket.TrySendMultipartMessage(message)) {
						GD.Print("请求主机信息中...");
						return;
					}
				}

				GD.PrintErr("请求主机信息失败");
				OnReady?.Invoke(false);
				return;
			}
		}

		GD.PrintErr("未找到主机");
		OnReady?.Invoke(false);
	}

	private async void DealerClientOnReceiveReady(object? sender, NetMQSocketEventArgs e) {
		NetMQMessage? hostMessage = null;
		if (!e.Socket.TryReceiveMultipartMessage(ref hostMessage)) {
			return;
		}

		if (_hostEasyTierInfo is null) {
			GD.PrintErr("收到来自主机的事件，但EasyTier主机信息丢失。");
			return;
		}

		var eventCode = (RoomEventEnum)BitConverter.ToInt32(hostMessage[0].Buffer);
		GD.Print($"收到来自主机的事件: {eventCode}");
		switch (eventCode) {
			case RoomEventEnum.JoinAccepted: {
				Players =
					Tools.PackSerializer.Deserialize<List<RoomPlayerInfo>, SourceGenerationContext>(hostMessage[1].Buffer)!;
				GD.Print("已从主机接受到房间信息，准备创建端口转发...");

				var local = $"{IPAddress.Any}:{LocalPort}";
				var remote = $"{_hostEasyTierInfo.Value.IpV4}:{Players[0].Port}";
				if (await _easyTier!.AddPortForward((local, remote, "tcp"))) {
					OnReady?.Invoke(true);
					_timeoutTimer!.Enable = false;
					_timeoutTimer.Enable = true;
					_heartbeatTimer!.Enable = true;
					return;
				}

				OnReady?.Invoke(false);
				return;
			}
			case RoomEventEnum.AddGuest: {
				var playerInfo = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(hostMessage[1].Buffer)!;
				if (Players.Any(p => p.Identity == playerInfo.Identity)) {
					return;
				}

				Players.Add(playerInfo);
				GD.Print($"玩家 {playerInfo.Name} 已加入");
				return;
			}
			case RoomEventEnum.GuestLeft: {
				var playerInfo = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(hostMessage[1].Buffer)!;
				var leftPlayer = GetPlayerByIdentity(playerInfo.Identity);
				if (leftPlayer is not null) {
					Players.Remove(leftPlayer);
					GD.Print($"玩家 {playerInfo.Name} 已离开");
				}

				break;
			}
			case RoomEventEnum.HostShutdown: {
				GD.Print("主机已关闭房间。");
				_isHostAlive = false;
				Shutdown();
				OnReady?.Invoke(false);
				break;
			}
			case RoomEventEnum.Pong: {
				_timeoutTimer!.Enable = false;
				_timeoutTimer.Enable = true;
				break;
			}
			case RoomEventEnum.GuestJoined:
			case RoomEventEnum.Ping:
			case RoomEventEnum.None: break;
			default: GD.PrintErr($"未知事件: {eventCode}"); break;
		}
	}

	private void SendPing() {
		if (!_isHostAlive || _dealerSocket is null) {
			return;
		}

		var message = new NetMQMessage();
		message.Append(BitConverter.GetBytes((int)RoomEventEnum.Ping));
		_dealerSocket.TrySendMultipartMessage(message);
	}

	private void HostTimedOut() {
		if (!_isHostAlive) {
			return;
		}

		_isHostAlive = false;
		GD.PrintErr("主机连接超时。");
		Dispatcher.SynchronizationContext.Post(_ => {
				Shutdown();
				OnReady?.Invoke(false);
			},
			null);
	}

	private void ShutdownGuest() {
		_heartbeatTimer?.Enable = false;
		_heartbeatTimer = null;
		_timeoutTimer?.Enable = false;
		_timeoutTimer = null;

		if (_isHostAlive) {
			GD.Print("客机正在关闭，通知主机...");
			var message = new NetMQMessage();
			message.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestLeft));
			_dealerSocket?.TrySendMultipartMessage(message);
		}

		GD.Print("已离开房间。");
		_dealerSocket?.Close();
		_dealerSocket = null;
	}
}
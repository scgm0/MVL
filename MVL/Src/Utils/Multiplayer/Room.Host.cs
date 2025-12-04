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
	private RouterSocket? _routerSocket;
	private NetMQTimer? _clientCheckTimer;
	private readonly TimeSpan _clientCheckIntervalMs = TimeSpan.FromSeconds(6);
	private readonly TimeSpan _clientTimeoutMs = TimeSpan.FromSeconds(18);

	public async void StartHost() {
		_poller = new();
		_poller.RunAsync("MVL-Room-Host", true);
		_routerSocket = new($"tcp://{IPAddress.Any}:{HostPort}");
		_routerSocket.ReceiveReady += RouterSocketOnReceiveReady;
		_poller.Add(_routerSocket);

		var args = await ComposeArgs();
		args.AddRange([
			"--hostname", NetworkName,
			"--ipv4", "10.144.144.1",
			$"--tcp-whitelist={LocalPort}",
			$"--tcp-whitelist={HostPort}",
			$"--udp-whitelist={LocalPort}"
		]);

		_easyTier = new();
		_easyTier.OnReady += OnEasyTierReadyByHost;

		await _easyTier.Start(args);
	}

	private void OnEasyTierReadyByHost(bool ready) {
		if (!ready) {
			Log.Error("EasyTier 启动失败。");
			Shutdown();
			OnReady?.Invoke(false);
			return;
		}

		_localPlayer = new(RoomType: RoomType.Host,
			Name: Main.BaseConfig.CurrentAccount,
			Version: _easyTier!.LocalPlayer.Version,
			Port: LocalPort,
			Address: _easyTier.LocalPlayer.IpV4) { Identity = _easyTier.LocalPlayer.Id };

		Players.Add(_localPlayer);
		Log.Info($"已创建房间，房间号: {Code}");

		_clientCheckTimer = new(_clientCheckIntervalMs);
		_clientCheckTimer.Elapsed += (_, _) => CheckDisconnectedGuests();
		_clientCheckTimer.Enable = true;
		_poller?.Add(_clientCheckTimer);
		OnReady?.Invoke(true);
	}

	private void RouterSocketOnReceiveReady(object? sender, NetMQSocketEventArgs e) {
		NetMQMessage? guestMessage = null;
		if (!e.Socket.TryReceiveMultipartMessage(ref guestMessage)) {
			return;
		}

		var clientIdentity = BitConverter.ToUInt32(guestMessage[0].Buffer);
		var eventCode = (RoomEventEnum)BitConverter.ToInt32(guestMessage[1].Buffer);
		Log.Info($"收到来自客户端的事件: {eventCode}");
		var player = GetPlayerByIdentity(clientIdentity);
		player?.LastHeartbeat = DateTimeOffset.UtcNow;

		switch (eventCode) {
			case RoomEventEnum.GuestJoined: {
				player = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(guestMessage[2].Buffer)!;
				if (Players.All(p => p.Identity != player.Identity)) {
					player.LastHeartbeat = DateTimeOffset.UtcNow;
					Players.Add(player);
					Log.Info($"玩家 {player.Name} 已加入房间。 当前玩家数: {Players.Count}");
				}

				var responseMessage = new NetMQMessage();
				responseMessage.Append(guestMessage[0].Buffer);
				responseMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.JoinAccepted));
				responseMessage.Append(
					Tools.PackSerializer.Serialize<List<RoomPlayerInfo>, SourceGenerationContext>(Players));
				if (_routerSocket!.TrySendMultipartMessage(responseMessage)) {
					Log.Info($"已向新访客 {player.Name} 发送房间信息。");
				}

				foreach (var roomPlayerInfo in Players) {
					if (roomPlayerInfo.RoomType != RoomType.Guest || roomPlayerInfo.Identity == clientIdentity) {
						continue;
					}

					responseMessage = new();
					responseMessage.Append(BitConverter.GetBytes(roomPlayerInfo.Identity));
					responseMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestJoined));
					responseMessage.Append(Tools.PackSerializer.Serialize(player));

					if (_routerSocket!.TrySendMultipartMessage(responseMessage)) {
						Log.Info("已广播更新访客信息。");
					}
				}

				break;
			}
			case RoomEventEnum.GuestLeft:
				if (player != null) {
					HandleClientDisconnect(player);
				}

				return;
			case RoomEventEnum.Ping: {
				var pongMessage = new NetMQMessage();
				pongMessage.Append(guestMessage[0].Buffer);
				pongMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.Pong));
				_routerSocket!.TrySendMultipartMessage(pongMessage);
				break;
			}
			case RoomEventEnum.JoinAccepted:
			case RoomEventEnum.AddGuest:
			case RoomEventEnum.HostShutdown:
			case RoomEventEnum.Pong:
			case RoomEventEnum.None: break;
			default: Log.Warn($"未知事件: {eventCode}"); break;
		}
	}

	public void HandleClientDisconnect(RoomPlayerInfo disconnectedPlayer) {
		if (!Players.Remove(disconnectedPlayer)) {
			return;
		}

		Log.Info($"玩家 {disconnectedPlayer.Name} 已离开房间。 当前玩家数: {Players.Count}");

		foreach (var player in Players.Where(p => p.RoomType == RoomType.Guest)) {
			var message = new NetMQMessage();
			message.Append(BitConverter.GetBytes(player.Identity));
			message.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestLeft));
			message.Append(Tools.PackSerializer.Serialize(disconnectedPlayer));
			if (_routerSocket!.TrySendMultipartMessage(message)) {
				Log.Debug($"已通知 {player.Name} 有玩家离开。");
			}
		}
	}

	private void CheckDisconnectedGuests() {
		if (Players.Count <= 1) {
			return;
		}

		var timedOutPlayers = new List<RoomPlayerInfo>();
		var timeoutThreshold = DateTimeOffset.UtcNow.Add(-_clientTimeoutMs);

		foreach (var player in Players.Where(p => p.RoomType == RoomType.Guest)) {
			if (player.LastHeartbeat < timeoutThreshold) {
				timedOutPlayers.Add(player);
			}
		}

		foreach (var timedOutPlayer in timedOutPlayers) {
			Log.Info($"检测到玩家 {timedOutPlayer.Name} 超时。");
			HandleClientDisconnect(timedOutPlayer);
		}
	}

	private void ShutdownHost() {
		_clientCheckTimer?.Enable = false;
		_clientCheckTimer = null;

		if (Players.Count > 1) {
			Log.Info("主机正在关闭，通知所有客户端...");
			foreach (var player in Players.Where(p => p.RoomType == RoomType.Guest)) {
				var message = new NetMQMessage();
				message.Append(BitConverter.GetBytes(player.Identity));
				message.Append(BitConverter.GetBytes((int)RoomEventEnum.HostShutdown));
				_routerSocket?.TrySendMultipartMessage(message);
			}
		}

		Log.Info("房间已关闭。");
		_routerSocket?.Close();
		_routerSocket = null;
	}
}
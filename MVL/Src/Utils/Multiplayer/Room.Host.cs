using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using MVL.UI;
using NetMQ;
using NetMQ.Sockets;

namespace MVL.Utils.Multiplayer;

public partial record Room {
	private RouterSocket? _routerSocket;
	private NetMQTimer? _clientCheckTimer;
	private NetMQTimer? _heartbeatSendTimer;
	private readonly TimeSpan _clientCheckIntervalMs = TimeSpan.FromSeconds(6);
	private readonly TimeSpan _clientTimeoutMs = TimeSpan.FromSeconds(18);
	private readonly TimeSpan _heartbeatSendIntervalMs = TimeSpan.FromSeconds(5);

	public async void StartHost() {
		_cts?.Dispose();
		_cts = new();

		_poller = new();
		_poller.RunAsync("MVL-Room-Host", true);
		_routerSocket = new($"tcp://{IPAddress.Any}:{HostPort}");
		_routerSocket.ReceiveReady += RouterSocketOnReceiveReady;
		_poller.Add(_routerSocket);

		var args = await ComposeArgs(_cts.Token);
		args.AddRange([
			"--hostname", NetworkName,
			"--ipv4", "10.144.144.1",
			$"--tcp-whitelist={LocalPort}",
			$"--tcp-whitelist={HostPort}",
			$"--udp-whitelist={LocalPort}"
		]);

		_easyTier = new();
		_easyTier.OnReady += OnEasyTierReadyByHost;

		try {
			OnStateChanged?.Invoke(new(RoomState.Connecting));
			await _easyTier.Start(args, _cts.Token);
		} catch (OperationCanceledException) {
			Log.Info("主机连接已取消");
			Shutdown();
		} catch (Exception e) {
			Log.Error("EasyTier启动异常", e);
			OnStateChanged?.Invoke(new(RoomState.Failed, "EasyTier 启动异常"));
			Shutdown();
		}
	}

	private void OnEasyTierReadyByHost(bool ready) {
		if (!ready) {
			Log.Error("EasyTier启动失败");
			OnStateChanged?.Invoke(new(RoomState.Failed, "EasyTier 启动失败"));
			Shutdown();
			return;
		}

		_localPlayer = new(RoomType: RoomType.Host,
				Name: Main.BaseConfig.CurrentAccount,
				Version: _easyTier!.LocalPlayer.Version,
				Port: LocalPort,
				Address: _easyTier.LocalPlayer.IpV4,
				Offline: Main.Accounts.TryGetValue(Main.BaseConfig.CurrentAccount, out var acc) && acc.Offline)
			{ Identity = _easyTier.LocalPlayer.Id };

		AddPlayer(_localPlayer);
		Log.Info($"主机已就绪，房间码: {Code}");
		OnStateChanged?.Invoke(new(RoomState.EasyTierReady));

		_clientCheckTimer = new(_clientCheckIntervalMs);
		_clientCheckTimer.Elapsed += (_, _) => CheckDisconnectedGuests();
		_clientCheckTimer.Enable = true;
		_poller?.Add(_clientCheckTimer);

		_heartbeatSendTimer = new(_heartbeatSendIntervalMs);
		_heartbeatSendTimer.Elapsed += (_, _) => SendHeartbeatToGuests();
		_heartbeatSendTimer.Enable = true;
		_poller?.Add(_heartbeatSendTimer);

		OnStateChanged?.Invoke(new(RoomState.Ready));
	}

	private void RouterSocketOnReceiveReady(object? sender, NetMQSocketEventArgs e) {
		NetMQMessage? guestMessage = null;
		if (!e.Socket.TryReceiveMultipartMessage(ref guestMessage)) {
			return;
		}

		var clientIdentity = BitConverter.ToUInt32(guestMessage[0].Buffer);
		var eventCode = (RoomEventEnum)BitConverter.ToInt32(guestMessage[1].Buffer);
		var player = GetPlayerByIdentityLocked(clientIdentity);
		if (player is not null) {
			player.LastHeartbeat = DateTimeOffset.UtcNow;
		}

		Log.Debug($"收到来自 {player?.Name} 的时间: {eventCode}");

		switch (eventCode) {
			case RoomEventEnum.GuestJoined: {
				player = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(guestMessage[2].Buffer)!;
				if (GetPlayerByIdentityLocked(player.Identity) == null) {
					player.LastHeartbeat = DateTimeOffset.UtcNow;
					AddPlayer(player);
					OnPlayerListChanged?.Invoke();
					Log.Info($"玩家 {player.Name} 已加入房间。 当前玩家数: {GetPlayerCount()}");
				}

				var responseMessage = new NetMQMessage();
				responseMessage.Append(guestMessage[0].Buffer);
				responseMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.JoinAccepted));
				responseMessage.Append(
					Tools.PackSerializer.Serialize<List<RoomPlayerInfo>, SourceGenerationContext>(GetPlayersSnapshot()));
				if (_routerSocket!.TrySendMultipartMessage(responseMessage)) {
					Log.Debug($"已向新访客 {player.Name} 发送房间信息。");
				}

				foreach (var roomPlayerInfo in GetPlayersSnapshot()) {
					if (roomPlayerInfo.RoomType != RoomType.Guest || roomPlayerInfo.Identity == clientIdentity) {
						continue;
					}

					responseMessage = new();
					responseMessage.Append(BitConverter.GetBytes(roomPlayerInfo.Identity));
					responseMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestJoined));
					responseMessage.Append(Tools.PackSerializer.Serialize(player));

					if (_routerSocket!.TrySendMultipartMessage(responseMessage)) {
						Log.Debug("已广播更新访客信息。");
					}
				}

				break;
			}
			case RoomEventEnum.GuestLeft:
				if (player != null) {
					HandleClientDisconnect(player);
				}

				return;
			case RoomEventEnum.HeartbeatAck: {
				if (player is not null) {
					var sentTimestamp = BitConverter.ToInt64(guestMessage[2].Buffer);
					var rttTicks = (double)(Stopwatch.GetTimestamp() - sentTimestamp);
					var rttMs = rttTicks / Stopwatch.Frequency * 1000;
					player.Latency = TimeSpan.FromMilliseconds(rttMs / 2);

					OnPlayerListChanged?.Invoke();
					Log.Debug($"玩家 {player.Name} 心跳响应: {player.Latency.TotalMilliseconds:F0}ms");

					var serializedPlayer = Tools.PackSerializer.Serialize(player);
					foreach (var guest in GetGuestsSnapshot()) {
						var updateMessage = new NetMQMessage();
						updateMessage.Append(BitConverter.GetBytes(guest.Identity));
						updateMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.PlayerUpdate));
						updateMessage.Append(serializedPlayer);
						_routerSocket!.TrySendMultipartMessage(updateMessage);
					}
				}

				break;
			}
			case RoomEventEnum.JoinAccepted:
			case RoomEventEnum.AddGuest:
			case RoomEventEnum.HostShutdown:
			case RoomEventEnum.Heartbeat:
			case RoomEventEnum.PlayerUpdate:
			case RoomEventEnum.None: break;
			default: Log.Warn($"未知事件: {eventCode}"); break;
		}
	}

	public void HandleClientDisconnect(RoomPlayerInfo disconnectedPlayer) {
		if (!RemovePlayer(disconnectedPlayer)) {
			return;
		}

		OnPlayerListChanged?.Invoke();

		Log.Info($"玩家 {disconnectedPlayer.Name} 已离开房间。 当前玩家数: {GetPlayerCount()}");

		foreach (var player in GetGuestsSnapshot()) {
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
		if (GetPlayerCount() <= 1) {
			return;
		}

		var timedOutPlayers = new List<RoomPlayerInfo>();
		var timeoutThreshold = DateTimeOffset.UtcNow.Add(-_clientTimeoutMs);

		foreach (var player in GetGuestsSnapshot()) {
			if (player.LastHeartbeat < timeoutThreshold) {
				timedOutPlayers.Add(player);
			}
		}

		foreach (var timedOutPlayer in timedOutPlayers) {
			Log.Info($"检测到玩家 {timedOutPlayer.Name} 超时。");
			HandleClientDisconnect(timedOutPlayer);
		}
	}

	private void SendHeartbeatToGuests() {
		var guests = GetGuestsSnapshot();
		if (guests.Count == 0) {
			return;
		}

		var nowTicks = Stopwatch.GetTimestamp();

		foreach (var guest in guests) {
			var message = new NetMQMessage();
			message.Append(BitConverter.GetBytes(guest.Identity));
			message.Append(BitConverter.GetBytes((int)RoomEventEnum.Heartbeat));
			message.Append(BitConverter.GetBytes(nowTicks));
			_routerSocket!.TrySendMultipartMessage(message);
		}
	}

	private void ShutdownHost() {
		if (_clientCheckTimer != null) {
			_poller?.Remove(_clientCheckTimer);
			_clientCheckTimer = null;
		}

		if (_heartbeatSendTimer != null) {
			_poller?.Remove(_heartbeatSendTimer);
			_heartbeatSendTimer = null;
		}

		if (GetPlayerCount() > 1) {
			Log.Info("主机正在关闭，通知所有客户端...");
			foreach (var player in GetGuestsSnapshot()) {
				var message = new NetMQMessage();
				message.Append(BitConverter.GetBytes(player.Identity));
				message.Append(BitConverter.GetBytes((int)RoomEventEnum.HostShutdown));
				_routerSocket?.TrySendMultipartMessage(message);
			}
		}

		Log.Info("房间已关闭。");
		if (_routerSocket != null) {
			_poller?.RemoveAndDispose(_routerSocket);
			_routerSocket = null;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MVL.UI;
using NetMQ;
using NetMQ.Sockets;

namespace MVL.Utils.Multiplayer;

public partial record Room {
	private DealerSocket? _dealerSocket;
	private EasyTierPlayerInfo? _hostEasyTierInfo;
	private NetMQTimer? _hostTimeoutTimer;
	private readonly TimeSpan _hostTimeoutMs = TimeSpan.FromSeconds(15);
	private volatile bool _isHostAlive = true;

	public async void StartGuest() {
		_cts?.Dispose();
		_cts = new();

		var args = await ComposeArgs(_cts.Token);
		args.AddRange([
			"-d",
			"--tcp-whitelist=0",
			"--udp-whitelist=0"
		]);

		_easyTier = new();
		_easyTier.OnReady += OnEasyTierReadyByGuest;

		try {
			OnStateChanged?.Invoke(new(RoomState.Connecting));
			await _easyTier.Start(args, _cts.Token);
		} catch (OperationCanceledException) {
			Log.Info("客机连接已取消");
			Shutdown();
		} catch (Exception e) {
			Log.Error("EasyTier启动异常", e);
			OnStateChanged?.Invoke(new(RoomState.Failed, "EasyTier 启动异常"));
			Shutdown();
		}
	}

	private async void OnEasyTierReadyByGuest(bool ready) {
		if (!ready) {
			Log.Error("EasyTier 启动失败");
			OnStateChanged?.Invoke(new(RoomState.Failed, "EasyTier 启动失败"));
			return;
		}

		try {
			_isHostAlive = true;
			OnStateChanged?.Invoke(new(RoomState.EasyTierReady));
			OnStateChanged?.Invoke(new(RoomState.RoomJoining));

			for (var i = 0; i < 5; i++) {
				Log.Info($"正在查找主机... (第 {i + 1}/5 次)");
				var players = await _easyTier!.GetPlayers();
				Log.Debug($"当前可见节点数: {players.Count}");

				foreach (var playerInfo in players) {
					if (!playerInfo.HostName.Equals(NetworkName, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}

					Log.Info($"已找到主机: {playerInfo.IpV4}");
					_hostEasyTierInfo = playerInfo;

					var localPort = Tools.GetAvailablePort();
					var local = $"{IPAddress.Any}:{localPort}";
					var remote = $"{playerInfo.IpV4}:{HostPort}";
					Log.Info($"正在创建端口转发 {local} -> {remote}...");

					if (!await _easyTier.AddPortForward((local, remote, "tcp"))) {
						Log.Error($"端口转发失败: {local} -> {remote}");
						OnStateChanged?.Invoke(new(RoomState.Failed, "端口转发失败"));
						return;
					}

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

					_hostTimeoutTimer = new(_hostTimeoutMs);
					_hostTimeoutTimer.Elapsed += (_, _) => HostTimedOut();
					_poller.Add(_hostTimeoutTimer);
					_hostTimeoutTimer.Enable = true;

					_localPlayer = new(RoomType: RoomType.Guest,
							Name: Main.BaseConfig.CurrentAccount,
							Version: _easyTier.LocalPlayer.Version,
							Port: 0,
							Address: _easyTier.LocalPlayer.IpV4,
							Offline: Main.Accounts.TryGetValue(Main.BaseConfig.CurrentAccount, out var acc) && acc.Offline)
						{ Identity = _easyTier.LocalPlayer.Id };

					var message = new NetMQMessage();
					message.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestJoined));
					message.Append(Tools.PackSerializer.Serialize(_localPlayer));

					if (_dealerSocket.TrySendMultipartMessage(message)) {
						Log.Info("已向主机发送加入请求");
						return;
					}

					Log.Error("加入请求发送失败，DealerSocket 未就绪");
					Shutdown();
					OnStateChanged?.Invoke(new(RoomState.Failed, "连接主机失败"));
					return;
				}

				if (i < 4) {
					await Task.Delay(2000, _cts?.Token ?? CancellationToken.None);
				}
			}

			Log.Error($"未找到主机 (已尝试 5 次，网络: {NetworkName})");
			OnStateChanged?.Invoke(new(RoomState.Failed, "未找到主机"));
		} catch (TaskCanceledException) {
			Log.Debug("取消连接主机");
		} catch (Exception e) {
			Log.Error("客机连接主机时发生异常", e);
			Shutdown();
			OnStateChanged?.Invoke(new(RoomState.Failed, "连接主机异常"));
		}
	}

	private async void DealerClientOnReceiveReady(object? sender, NetMQSocketEventArgs e) {
		NetMQMessage? hostMessage = null;
		if (!e.Socket.TryReceiveMultipartMessage(ref hostMessage)) {
			return;
		}

		if (_hostEasyTierInfo is null) {
			Log.Error("收到来自主机的事件，但EasyTier主机信息丢失");
			return;
		}

		var eventCode = (RoomEventEnum)BitConverter.ToInt32(hostMessage[0].Buffer);
		Log.Debug($"收到来自主机的事件: {eventCode}");

		switch (eventCode) {
			case RoomEventEnum.JoinAccepted: {
				var playerList =
					Tools.PackSerializer.Deserialize<List<RoomPlayerInfo>, SourceGenerationContext>(hostMessage[1].Buffer)!;
				lock (_playersLock) {
					_players.Clear();
					_players.AddRange(playerList);
				}

				OnPlayerListChanged?.Invoke();
				Log.Info("已从主机接受到房间信息，准备创建端口转发...");

				var local = $"{IPAddress.Any}:{LocalPort}";
				var snapshot = GetPlayersSnapshot();
				var remote = $"{_hostEasyTierInfo.Value.IpV4}:{snapshot[0].Port}";
				if (await _easyTier!.AddPortForward((local, remote, "tcp"))) {
					OnStateChanged?.Invoke(new(RoomState.Ready));
					_hostTimeoutTimer!.Enable = false;
					_hostTimeoutTimer.Enable = true;
					return;
				}

				OnStateChanged?.Invoke(new(RoomState.Failed, "端口转发失败"));
				return;
			}
			case RoomEventEnum.AddGuest: {
				var playerInfo = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(hostMessage[1].Buffer)!;
				if (GetPlayerByIdentityLocked(playerInfo.Identity) != null) {
					return;
				}

				AddPlayer(playerInfo);
				OnPlayerListChanged?.Invoke();
				Log.Info($"玩家 {playerInfo.Name} 已加入");
				return;
			}
			case RoomEventEnum.GuestLeft: {
				var playerInfo = Tools.PackSerializer.Deserialize<RoomPlayerInfo>(hostMessage[1].Buffer)!;
				if (GetPlayerByIdentityLocked(playerInfo.Identity) is { } leftPlayer) {
					RemovePlayer(leftPlayer);
					OnPlayerListChanged?.Invoke();
					Log.Info($"玩家 {playerInfo.Name} 已离开");
				}

				break;
			}
			case RoomEventEnum.HostShutdown: {
				Log.Info("主机已关闭房间。");
				_isHostAlive = false;
				OnStateChanged?.Invoke(new(RoomState.Disconnected));
				break;
			}
			case RoomEventEnum.Heartbeat: {
				var ackMessage = new NetMQMessage();
				ackMessage.Append(BitConverter.GetBytes((int)RoomEventEnum.HeartbeatAck));
				ackMessage.Append(hostMessage[1].Buffer);
				_dealerSocket!.TrySendMultipartMessage(ackMessage);

				_hostTimeoutTimer!.Enable = false;
				_hostTimeoutTimer.Enable = true;
				break;
			}
			case RoomEventEnum.PlayerUpdate: {
				var updatedPlayer =
					Tools.PackSerializer.Deserialize<RoomPlayerInfo>(hostMessage[1].Buffer);
				if (updatedPlayer is not null && GetPlayerByIdentityLocked(updatedPlayer.Identity) is { } existing) {
					existing.Latency = updatedPlayer.Latency;
					OnPlayerListChanged?.Invoke();
				}

				break;
			}
			case RoomEventEnum.GuestJoined:
			case RoomEventEnum.HeartbeatAck:
			case RoomEventEnum.None: break;
			default: Log.Warn($"未知事件: {eventCode}"); break;
		}
	}

	private void HostTimedOut() {
		if (!_isHostAlive) {
			return;
		}

		_isHostAlive = false;
		Log.Error("主机连接超时");
		Dispatcher.SynchronizationContext.Post(_ => {
				Shutdown();
				OnStateChanged?.Invoke(new(RoomState.Failed, "主机连接超时"));
			},
			null);
	}

	private void ShutdownGuest() {
		if (_hostTimeoutTimer != null) {
			_poller?.Remove(_hostTimeoutTimer);
			_hostTimeoutTimer = null;
		}

		if (_isHostAlive) {
			Log.Info("客机正在关闭，通知主机...");
			var message = new NetMQMessage();
			message.Append(BitConverter.GetBytes((int)RoomEventEnum.GuestLeft));
			_dealerSocket?.TrySendMultipartMessage(message);
		}

		Log.Info("已离开房间。");
		if (_dealerSocket != null) {
			_poller?.RemoveAndDispose(_dealerSocket);
			_dealerSocket = null;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using NetMQ;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace MVL.Utils.Multiplayer;

public partial record Room : IDisposable {
	public string Code { get; set; }
	public string NetworkName { get; set; }
	public string NetworkSecret { get; set; }
	public int HostPort { get; set; }
	public int LocalPort { get; set; }
	public List<RoomPlayerInfo> Players { get; set; } = [];
	public event Action<bool>? OnReady;

	
	private EasyTier? _easyTier;
	private RoomPlayerInfo? _localPlayer;
	private NetMQPoller? _poller;

	[GeneratedRegex(@"^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{8}$")]
	static private partial Regex CodeRegex();

	static private readonly string[] PreArgs = [
		"--no-tun",
		"--compression=zstd",
		"--multi-thread",
		"--latency-first",
		"--enable-kcp-proxy",
		"-l", $"tcp://{IPAddress.Any}:0",
		"-l", $"udp://{IPAddress.Any}:0",
	];

	private Room(string code, string networkName, string networkSecret) {
		Code = code;
		NetworkName = networkName;
		NetworkSecret = networkSecret;
		AsyncIO.ForceDotNet.Force();
	}

	public void Shutdown() {
		_poller?.Dispose();
		if (_routerSocket is not null) {
			ShutdownHost();
		} else if (_dealerSocket is not null) {
			ShutdownGuest();
		}

		Dispatcher.SynchronizationContext.Send(_ => { _easyTier?.Dispose(); }, null);

		Players.Clear();
		_localPlayer = null;
	}

	public RoomPlayerInfo? GetPlayerByIdentity(uint identity) {
		foreach (var playerInfo in Players) {
			if (playerInfo.Identity == identity) {
				return playerInfo;
			}
		}

		return null;
	}

	public async Task<List<string>> ComposeArgs() {
		var args = new List<string>(PreArgs);
		args.AddRange([
			"--network-name", NetworkName,
			"--network-secret", NetworkSecret
		]);

		var nodes = await EasyTier.FetchPublicNodes(10);
		foreach (var node in nodes) {
			args.AddRange(["-p", node]);
		}

		return args;
	}

	public void Dispose() {
		Shutdown();
		GC.SuppressFinalize(this);
	}

	public static Room Create(int localPort) {
		if (localPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort) {
			throw new ArgumentOutOfRangeException(nameof(localPort),
				$"端口号必须介于{IPEndPoint.MinPort}和{IPEndPoint.MaxPort}之间。");
		}

		var hostPort = Tools.GetAvailablePort();
		var uniqueIdBytes = RandomNumberGenerator.GetBytes(4);
		var networkSecretBytes = RandomNumberGenerator.GetBytes(4);
		var portBytes = BitConverter.GetBytes((ushort)hostPort);
		if (BitConverter.IsLittleEndian) {
			Array.Reverse(portBytes);
		}

		var uniqueIdHex = Convert.ToHexString(uniqueIdBytes)[..4];
		var portHex = Convert.ToHexString(portBytes);
		var networkSecretHex = Convert.ToHexString(networkSecretBytes);
		var networkName = $"mvl-vs-server-{uniqueIdHex}{portHex}";
		var code = $"{uniqueIdHex}{portHex}-{networkSecretHex}";

		return new(code, networkName, networkSecretHex) {
			HostPort = hostPort,
			LocalPort = localPort
		};
	}

	public static Room? Parse(string code) {
		if (string.IsNullOrEmpty(code) || !CodeRegex().IsMatch(code)) {
			return null;
		}

		try {
			var parts = code.Split('-');
			var combinedPart = parts[0];
			var networkSecret = parts[1];

			var uniqueIdHex = combinedPart[..4];
			var portHex = combinedPart.Substring(4, 4);

			var networkName = $"mvl-vs-server-{uniqueIdHex}{portHex}";
			var port = Convert.ToInt32(portHex, 16);
			if (port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort) {
				return null;
			}

			return new(code, networkName, networkSecret) {
				HostPort = port,
				LocalPort = Tools.GetAvailablePort()
			};
		} catch (Exception) {
			return null;
		}
	}
}
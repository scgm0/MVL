using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;
using Godot;
using MVL.UI;
using NetMQ;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace MVL.Utils.Multiplayer;

public enum RoomState {
	Disconnected,
	Connecting,
	EasyTierReady,
	RoomJoining,
	Ready,
	Failed,
}

public readonly record struct RoomStateChange(RoomState State, string? ErrorMessage = null);

public partial record Room : IDisposable {
	public string Code { get; }
	public string NetworkName { get; }
	public string NetworkSecret { get; }
	public ushort HostPort { get; private set; }
	public ushort LocalPort { get; private set; }
	private readonly Lock _playersLock = new();
	private readonly List<RoomPlayerInfo> _players = [];
	public event Action<RoomStateChange>? OnStateChanged;
	public event Action? OnPlayerListChanged;

	private EasyTier? _easyTier;
	private RoomPlayerInfo? _localPlayer;
	private NetMQPoller? _poller;
	// private NetMQMonitor? _monitor;
	private CancellationTokenSource? _cts;
	private volatile bool _shuttingDown;

	public List<RoomPlayerInfo> GetPlayersSnapshot() {
		lock (_playersLock) {
			return [.. _players];
		}
	}

	public void AddPlayer(RoomPlayerInfo player) {
		lock (_playersLock) {
			_players.Add(player);
		}
	}

	public int GetPlayerCount() {
		lock (_playersLock) {
			return _players.Count;
		}
	}

	public RoomPlayerInfo? GetPlayerByIdentityLocked(uint identity) {
		lock (_playersLock) {
			return identity == _localPlayer?.Identity ? _localPlayer : _players.Find(p => p.Identity == identity);
		}
	}

	public bool RemovePlayer(RoomPlayerInfo player) {
		lock (_playersLock) {
			return _players.Remove(player);
		}
	}

	public void ClearPlayers() {
		lock (_playersLock) {
			_players.Clear();
		}
	}

	public List<RoomPlayerInfo> GetGuestsSnapshot() {
		lock (_playersLock) {
			return [.. _players.Where(p => p.RoomType == RoomType.Guest)];
		}
	}

	[GeneratedRegex(@"^[0-9a-f]+-[0-9a-z]{9}-[0-9a-z]{9}$", RegexOptions.IgnoreCase)]
	static private partial Regex CodeRegex();

	public static bool IsValidCode(string code) { return !string.IsNullOrEmpty(code) && CodeRegex().IsMatch(code); }

	private const string NetworkNameFormat = "{0}-vs-server-{1}";
	private const int UniqueIdB36Length = 7;
	private const int CombinedB36Length = 11;
	private const int FinalPartLength = 9;

	static private readonly string[] PreArgs = [
		"--no-tun",
		"--compression=zstd",
		"--multi-thread",
		"--latency-first",
		"--enable-kcp-proxy",
		"-l", $"tcp://{IPAddress.Any}:0",
		"-l", $"udp://{IPAddress.Any}:0",
	];

	private Room(string code, string networkName, string networkSecret, ushort hostPort, ushort localPort) {
		Code = code;
		NetworkName = networkName;
		NetworkSecret = networkSecret;
		HostPort = hostPort;
		LocalPort = localPort;
		ForceDotNet.Force();
		Log.Debug($"初始化房间数据: Code={Code}");
	}

	public void Shutdown() {
		if (_shuttingDown) {
			return;
		}

		_shuttingDown = true;

		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;

		if (_routerSocket is not null) {
			ShutdownHost();
		} else if (_dealerSocket is not null) {
			ShutdownGuest();
		}

		_poller?.Dispose();
		_poller = null;

		Dispatcher.SynchronizationContext.Send(_ => { _easyTier?.Dispose(); }, null);

		ClearPlayers();
		_localPlayer = null;
	}

	public async Task<List<string>> ComposeArgs(CancellationToken token = default) {
		var args = new List<string>(PreArgs);
		args.AddRange([
			"--network-name", NetworkName,
			"--network-secret", NetworkSecret
		]);

		var nodes = await EasyTier.FetchPublicNodes(10, token);
		foreach (var node in nodes) {
			args.AddRange(["-p", node]);
		}

		foreach (var node in Main.BaseConfig.CustomEasyTierNodes) {
			args.AddRange(["-p", node]);
		}

		return args;
	}

	public void Dispose() {
		Shutdown();
		GC.SuppressFinalize(this);
	}

	public static Room Create(ushort localPort, string prefix) {
		if (string.IsNullOrEmpty(prefix) || prefix.Contains('-')) {
			throw new ArgumentException("前缀无效。", nameof(prefix));
		}

		var hostPort = Tools.GetAvailablePort();
		var uniqueIdBytes = RandomNumberGenerator.GetBytes(4);
		var secretBytes = RandomNumberGenerator.GetBytes(4);
		Span<byte> combinedBytes = stackalloc byte[6];
		BitConverter.TryWriteBytes(combinedBytes[..2], hostPort);
		if (BitConverter.IsLittleEndian) {
			combinedBytes[..2].Reverse();
		}

		secretBytes.CopyTo(combinedBytes[2..]);

		var uniqueIdBigInt = new BigInteger(uniqueIdBytes, isUnsigned: true, isBigEndian: true);
		var combinedBigInt = new BigInteger(combinedBytes, isUnsigned: true, isBigEndian: true);

		var uniqueIdB36Padded = Base36Converter.ToBase36String(uniqueIdBigInt).PadLeft(UniqueIdB36Length, '0');
		var combinedB36Padded = Base36Converter.ToBase36String(combinedBigInt).PadLeft(CombinedB36Length, '0');

		var fullB36Data = string.Concat(uniqueIdB36Padded, combinedB36Padded);
		var part1 = fullB36Data.AsSpan(0, FinalPartLength);
		var part2 = fullB36Data.AsSpan(FinalPartLength);

		var prefixHex = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(prefix));
		var uniqueIdHex = Convert.ToHexStringLower(uniqueIdBytes);

		var code = $"{prefixHex}-{part1.ToString()}-{part2.ToString()}".ToUpperInvariant();
		var networkName = string.Format(NetworkNameFormat, prefix, uniqueIdHex);
		var networkSecret = Convert.ToHexStringLower(secretBytes);

		return new(code, networkName, networkSecret, hostPort, localPort);
	}

	public static Room? Parse(string code) {
		if (string.IsNullOrEmpty(code) || !CodeRegex().IsMatch(code)) {
			return null;
		}

		try {
			var parts = code.Split('-');
			if (parts.Length != 3) {
				return null;
			}

			var prefixHex = parts[0];
			var part1 = parts[1];
			var part2 = parts[2];

			var fullB36Data = string.Concat(part1, part2);
			var uniqueIdB36 = fullB36Data.AsSpan(0, UniqueIdB36Length);
			var combinedB36 = fullB36Data.AsSpan(UniqueIdB36Length);

			var uniqueIdBigInt = Base36Converter.ParseBase36(uniqueIdB36.ToString());
			var combinedBigInt = Base36Converter.ParseBase36(combinedB36.ToString());

			var uniqueIdBytes = uniqueIdBigInt.ToByteArray(isUnsigned: true, isBigEndian: true);
			var combinedBytes = combinedBigInt.ToByteArray(isUnsigned: true, isBigEndian: true);

			Span<byte> paddedUniqueId = stackalloc byte[4];
			uniqueIdBytes.CopyTo(paddedUniqueId[(4 - uniqueIdBytes.Length)..]);

			Span<byte> paddedCombined = stackalloc byte[6];
			combinedBytes.CopyTo(paddedCombined[(6 - combinedBytes.Length)..]);

			if (BitConverter.IsLittleEndian) {
				paddedCombined[..2].Reverse();
			}

			var port = BitConverter.ToUInt16(paddedCombined[..2]);
			var secretBytes = paddedCombined[2..].ToArray();

			var prefix = Encoding.UTF8.GetString(Convert.FromHexString(prefixHex));
			var networkName = string.Format(NetworkNameFormat, prefix, Convert.ToHexStringLower(paddedUniqueId));
			var networkSecret = Convert.ToHexStringLower(secretBytes);

			return new(code, networkName, networkSecret, port, Tools.GetAvailablePort());
		} catch (Exception) {
			return null;
		}
	}
}
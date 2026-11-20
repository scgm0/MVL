using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AsyncIO;
using Godot;
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
			return _players.Find(p => p.Identity == identity);
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

	public RoomPlayerInfo[] GetGuestsSnapshot() {
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

		var poller = _poller;
		var easyTier = _easyTier;
		_easyTier = null;
		_poller = null;
		Dispatcher.SynchronizationContext.Send(_ => {
				poller?.Dispose();
				easyTier?.Dispose();
			},
			null);

		ClearPlayers();
		_localPlayer = null;
	}

	public List<string> ComposeArgs(ReadOnlySpan<SharedNode> nodes) {
		var args = new List<string>(PreArgs);
		args.AddRange([
			"--network-name", NetworkName,
			"--network-secret", NetworkSecret
		]);

		foreach (var node in nodes) {
			args.AddRange(["-p", node.GetDecodedAddress()]);
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
		Span<byte> uniqueIdBytes = stackalloc byte[4];
		Span<byte> secretBytes = stackalloc byte[4];
		RandomNumberGenerator.Fill(uniqueIdBytes);
		RandomNumberGenerator.Fill(secretBytes);
		Span<byte> combinedBytes = stackalloc byte[6];
		BitConverter.TryWriteBytes(combinedBytes[..2], hostPort);
		if (BitConverter.IsLittleEndian) {
			combinedBytes[..2].Reverse();
		}

		secretBytes.CopyTo(combinedBytes[2..]);

		var uniqueIdBigInt = new BigInteger(uniqueIdBytes, isUnsigned: true, isBigEndian: true);
		var combinedBigInt = new BigInteger(combinedBytes, isUnsigned: true, isBigEndian: true);

		Span<char> fullB36Data = stackalloc char[UniqueIdB36Length + CombinedB36Length];
		PadLeft(Base36Converter.ToBase36String(uniqueIdBigInt), fullB36Data[..UniqueIdB36Length]);
		PadLeft(Base36Converter.ToBase36String(combinedBigInt), fullB36Data[UniqueIdB36Length..]);

		var prefixHex = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(prefix));
		var uniqueIdHex = Convert.ToHexStringLower(uniqueIdBytes);

		var code = $"{prefixHex}-{fullB36Data[..FinalPartLength]}-{fullB36Data[FinalPartLength..]}".ToUpperInvariant();
		var networkName = string.Format(NetworkNameFormat, prefix, uniqueIdHex);
		var networkSecret = Convert.ToHexStringLower(secretBytes);

		return new(code, networkName, networkSecret, hostPort, localPort);
	}

	static private void PadLeft(ReadOnlySpan<char> value, Span<char> destination) {
		destination.Fill('0');
		value.CopyTo(destination[(destination.Length - value.Length)..]);
	}

	public static Room? Parse(string code) {
		if (string.IsNullOrEmpty(code) || !CodeRegex().IsMatch(code)) {
			return null;
		}

		try {
			var span = code.AsSpan();
			var firstDash = span.IndexOf('-');
			var secondDash = span[(firstDash + 1)..].IndexOf('-') + firstDash + 1;

			var prefixHex = span[..firstDash];
			var part1 = span[(firstDash + 1)..secondDash];
			var part2 = span[(secondDash + 1)..];

			Span<char> fullB36Data = stackalloc char[UniqueIdB36Length + CombinedB36Length];
			part1.CopyTo(fullB36Data);
			part2.CopyTo(fullB36Data[part1.Length..]);

			var uniqueIdB36 = fullB36Data[..UniqueIdB36Length];
			var combinedB36 = fullB36Data[UniqueIdB36Length..];

			var uniqueIdBigInt = Base36Converter.ParseBase36(uniqueIdB36);
			var combinedBigInt = Base36Converter.ParseBase36(combinedB36);

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

			return new(code,
				networkName,
				networkSecret,
				port,
				Tools.PortIsAvailable(42420) ? (ushort)42420 : Tools.GetAvailablePort());
		} catch (Exception) {
			return null;
		}
	}
}
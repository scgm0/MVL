using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Godot;

namespace MVL.Utils.Multiplayer;

public class EasyTier : IDisposable {
	public EasyTierLogLevel LogLevel { get; set; } = EasyTierLogLevel.Info;
	public int RpcPort { get; set; }
	public Process? Process { get; set; }
	public EasyTierPlayerInfo LocalPlayer { get; set; }
	public bool IsActive => Process?.HasExited == false;

	public event Action<bool>? OnReady;

	public async Task Start(List<string> args) {
		RpcPort = Tools.GetAvailablePort();
		var processStartInfo = new ProcessStartInfo(CorePath) {
			WorkingDirectory = Paths.EasyTierFolder,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};

		foreach (var arg in args) {
			processStartInfo.ArgumentList.Add(arg);
		}

		processStartInfo.ArgumentList.Add("-r");
		processStartInfo.ArgumentList.Add(RpcPort.ToString());
		processStartInfo.ArgumentList.Add("--file-log-size");
		processStartInfo.ArgumentList.Add("1");
		processStartInfo.ArgumentList.Add("--file-log-level");
		processStartInfo.ArgumentList.Add(LogLevel.ToString());

		Process?.Kill();
		Process?.Dispose();
		Process = new();
		Process.StartInfo = processStartInfo;
		Process.Start();
		Process.BeginOutputReadLine();
		Process.BeginErrorReadLine();

		var i = 0;
		await Task.Run(async () => {
			while (true) {
				try {
					var players = await GetPlayers();
					if (players.Count <= 1) {
						continue;
					}

					LocalPlayer = await GetLocalPlayer();
					Log.Info("已连接EasyTier服务器");
					OnReady?.Invoke(true);
					return;
				} catch {
					// ignored
				}

				i++;
				if (i > 6) {
					Log.Error("EasyTier连接超时");
					Kill();
					OnReady?.Invoke(false);
					return;
				}

				await Task.Delay(1000);
			}
		});
	}

	public async Task<List<EasyTierPlayerInfo>> GetPlayers() {
		var json = await RunCli(["-p", $"127.0.0.1:{RpcPort}", "-o", "json", "peer"], false);
		var players = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.ListEasyTierPlayerInfo);
		return players ?? [];
	}

	public async Task<EasyTierPlayerInfo> GetLocalPlayer() {
		var players = await GetPlayers();
		foreach (var easyTierPlayerInfo in players) {
			if (easyTierPlayerInfo.Cost == "Local") {
				return easyTierPlayerInfo;
			}
		}

		return default;
	}

	public async Task<bool> AddPortForward((string local, string remote, string protocol) portForwarding) {
		var output = await RunCli([
			"-p", $"127.0.0.1:{RpcPort}", "port-forward", "add",
			portForwarding.protocol, portForwarding.local, portForwarding.remote
		]);
		if (output.StartsWith("Port forward rule added", StringComparison.OrdinalIgnoreCase)) {
			Log.Info($"已创建端口转发 {portForwarding.local} -> {portForwarding.remote}");
			return true;
		}

		Log.Warn($"无法创建端口转发 {portForwarding.local} -> {portForwarding.remote}");
		return false;
	}

	public void Kill() {
		Process?.Kill(true);
		Process?.Dispose();
		Process = null;
	}

	public void Dispose() {
		Kill();
		GC.SuppressFinalize(this);
	}

#if GODOT_LINUXBSD
	public static string CorePath { get; } = Paths.EasyTierFolder.PathJoin("easytier-core").Normalize();
	public static readonly string CliPath = Paths.EasyTierFolder.PathJoin("easytier-cli").Normalize();
#elif GODOT_WINDOWS
	public static string CorePath { get; } = Paths.EasyTierFolder.PathJoin("easytier-core.exe").Normalize();
	public static readonly string CliPath = Paths.EasyTierFolder.PathJoin("easytier-cli.exe").Normalize();
#endif

	public static async Task<string> RunCli(List<string> args, bool print = true, string? path = null) {
		path ??= CliPath;
		var processStartInfo = new ProcessStartInfo(path) {
			WorkingDirectory = Paths.EasyTierFolder,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
		};

		foreach (var arg in args) {
			processStartInfo.ArgumentList.Add(arg);
		}

		using var process = new Process();
		process.StartInfo = processStartInfo;
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync();
		if (print) {
			Log.Debug(output.Trim());
		}

		await process.WaitForExitAsync();
		return output;
	}

	static private readonly string[] FallbackServers = [
		"tcp://public.easytier.top:11010",
		"tcp://public2.easytier.cn:54321"
	];

	public static async Task<List<string>> FetchPublicNodes(int limit = 5) {
		var activeServers = new List<string>(200);
		try {
			var response = await "https://uptime.easytier.cn/api/nodes"
				.SetQueryParams(new {
					is_active = "true",
					page = "1",
					per_page = "200"
				})
				.WithTimeout(TimeSpan.FromSeconds(10))
				.GetStreamAsync();

			var apiResponse = await JsonSerializer.DeserializeAsync(response, SourceGenerationContext.Default.ApiResponse);
			foreach (var item in apiResponse.Data.Items) {
				if (item is { IsActive: true, AllowRelay: true } && !FallbackServers.Contains(item.Address)) {
					activeServers.Add(item.Address);
				}
			}
		} catch (Exception ex) {
			Log.Error("获取公共节点失败", ex);
		}

		var count = activeServers.Count;
		var n = Math.Min(limit, count);
		var finalServers = new List<string>(n + FallbackServers.Length);
		for (var i = 0; i < n; i++) {
			var j = Random.Shared.Next(i, count);
			finalServers.Add(activeServers[j]);

			(activeServers[i], activeServers[j]) = (activeServers[j], activeServers[i]);
		}

		finalServers.AddRange(FallbackServers);
		return finalServers;
	}

	public static async Task<string?> GetCoreVersion() {
		try {
			var output = await RunCli(["-V"], true, CorePath);
			var version = output.Split(' ');
			return version[0].Equals("easytier-core", StringComparison.OrdinalIgnoreCase) ? version[1] : null;
		} catch (Exception ex) {
			Log.Error("获取EasyTier Core版本失败", ex);
			return null;
		}
	}

	public static async Task<string?> GetCliVersion() {
		try {
			var output = await RunCli(["-V"], true, CliPath);
			var version = output.Split(' ');
			return version[0].Equals("easytier-cli", StringComparison.OrdinalIgnoreCase) ? version[1] : null;
		} catch (Exception ex) {
			Log.Error("获取EasyTier CLI版本失败", ex);
			return null;
		}
	}
}
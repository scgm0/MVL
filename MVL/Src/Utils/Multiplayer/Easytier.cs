using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace MVL.Utils.Multiplayer;

public class EasyTier : IDisposable {
	public EasyTierLogLevel LogLevel { get; set; } = EasyTierLogLevel.Info;
	public int RpcPort { get; set; }
	public Process? Process { get; set; }
	public EasyTierPlayerInfo LocalPlayer { get; set; }
	public bool IsActive => Process?.HasExited == false;

	public event Action<bool>? OnReady;

	public async Task Start(List<string> args, CancellationToken token = default) {
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
		Log.Info($"EasyTier 启动参数: {string.Join(" ", processStartInfo.ArgumentList)}");

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
					token.ThrowIfCancellationRequested();
					await Task.Delay(1000, token);
					try {
						var players = await GetPlayers();
						if (players.Count > 1) {
							LocalPlayer = await GetLocalPlayer();
							Log.Info("已连接EasyTier服务器");
							OnReady?.Invoke(true);
							return;
						}
					} catch (OperationCanceledException) {
						throw;
					} catch {
						// ignored
					} finally {
						i++;
					}

					if (i > 6) {
						Log.Error("EasyTier连接超时");
						Kill();
						OnReady?.Invoke(false);
						return;
					}

					Log.Debug($"正在等待EasyTier服务器，已等待{i}秒");
				}
			},
			token);
	}

	public async Task<List<EasyTierPlayerInfo>> GetPlayers() {
		var json = await RunCli(["-p", $"127.0.0.1:{RpcPort}", "-o", "json", "peer"]);
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
		if (output.StartsWith("Port forward rule add", StringComparison.OrdinalIgnoreCase)) {
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
			Console.WriteLine(output.Trim());
		}

		await process.WaitForExitAsync();
		return output;
	}

	public static Task<string?> GetCoreVersion() {
		return GetVersion(CorePath, "easytier-core", "EasyTier Core");
	}

	public static Task<string?> GetCliVersion() {
		return GetVersion(CliPath, "easytier-cli", "EasyTier CLI");
	}

	static private async Task<string?> GetVersion(string path, string executableName, string displayName) {
		Log.Debug($"获取{displayName}版本");
		if (!File.Exists(path)) {
			Log.Warn($"{displayName}不存在: {path}");
			return null;
		}

		try {
			var output = await RunCli(["-V"], true, path);
			var span = output.AsSpan().Trim();
			var space = span.IndexOf(' ');
			if (space < 0 || !span[..space].Equals(executableName, StringComparison.OrdinalIgnoreCase)) {
				return null;
			}

			return span[(space + 1)..].Trim().ToString();
		} catch (Exception ex) {
			Log.Error($"获取{displayName}版本失败", ex);
			return null;
		}
	}
}
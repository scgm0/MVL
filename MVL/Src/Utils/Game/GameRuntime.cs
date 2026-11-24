using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace MVL.Utils.Game;

internal sealed record GameRuntime(string ConfigPath, string Target, string RollForward) {
	public static GameRuntime Read(string gamePath, string mainAssembly) {
		gamePath = Path.GetFullPath(gamePath);
		var path = Path.ChangeExtension(Path.Combine(gamePath, mainAssembly), ".runtimeconfig.json");
		if (!File.Exists(path)) {
			path = Path.Combine(gamePath, "Vintagestory.runtimeconfig.json");
		}

		using var stream = File.OpenRead(path);
		using var config = JsonDocument.Parse(stream, new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
		if (!config.RootElement.TryGetProperty("runtimeOptions", out var options)) {
			throw new InvalidDataException(string.Format(TranslationServer.Translate("游戏配置 {0} 缺少 runtimeOptions。"), path));
		}
		string? version = null;
		if (options.TryGetProperty("framework", out var framework)) {
			version = GetCoreVersion(framework);
		} else if (options.TryGetProperty("frameworks", out var frameworks)) {
			foreach (var entry in frameworks.EnumerateArray()) {
				version ??= GetCoreVersion(entry);
			}
		} else {
			throw new InvalidDataException(string.Format(TranslationServer.Translate("游戏配置 {0} 未声明共享运行时。"), path));
		}

		if (version is null && options.TryGetProperty("tfm", out var tfm) && tfm.GetString() is { } name &&
			name.StartsWith("net", StringComparison.Ordinal)) {
			version = name[3..];
		}

		if (!Version.TryParse(version?.Split('-')[0], out var parsed)) {
			throw new InvalidDataException(string.Format(TranslationServer.Translate("无法从 {0} 确定游戏要求的 .NET 版本。"), path));
		}

		var exact = options.TryGetProperty("rollForward", out var policy) &&
			string.Equals(policy.GetString(), "Disable", StringComparison.OrdinalIgnoreCase);
		return new(path, $"net{parsed.Major}.{parsed.Minor}", exact ? "Disable" : "LatestPatch");
	}

	private static string? GetCoreVersion(JsonElement framework) =>
		framework.GetProperty("name").GetString() == "Microsoft.NETCore.App" ? framework.GetProperty("version").GetString() : null;

	public string[] GetHostArguments(string runner) => ["exec", "--runtimeconfig", ConfigPath, "--roll-forward", RollForward, runner];

	public async Task<string> CheckAsync(string dotnet, string runner, string gamePath, IReadOnlyDictionary<string, string?>? environment) {
		var start = new ProcessStartInfo(dotnet) {
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = gamePath,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		foreach (var argument in GetHostArguments(runner)) {
			start.ArgumentList.Add(argument);
		}
		start.ArgumentList.Add("--mvl-check-runtime");
		if (environment is not null) {
			foreach (var (name, value) in environment) {
				start.Environment[name] = value;
			}
		}
		start.Environment.Remove("DOTNET_STARTUP_HOOKS");
		start.Environment["VINTAGE_STORY_PATH"] = gamePath;
		using var process = new Process { StartInfo = start };
		try {
			process.Start();
		} catch (Win32Exception e) {
			var message = string.Format(TranslationServer.Translate("无法运行 {0}，请检查是否已安装 {1} (x64) 运行时。"), dotnet, Target);
			throw new InvalidOperationException($"{message}\n{e.Message}", e);
		}
		var output = process.StandardOutput.ReadToEndAsync();
		var error = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();
		var result = (await output).Trim();
		if (process.ExitCode != 0) {
			var message = string.Format(TranslationServer.Translate("无法启动游戏所需的 {0} (x64)。"), Target);
			throw new InvalidOperationException($"{message}\n{error.Trim()}\n{result}");
		}
		return result;
	}
}

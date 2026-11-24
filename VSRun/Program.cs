using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using SharedLibrary;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

// ReSharper disable UnusedMember.Global

namespace VSRun;

public static class Program {
	public static RunConfig Config { get; set; }

	static private readonly JsonSerializerOptions ConfigJson = new(JsonSerializerDefaults.Web) {
		AllowTrailingCommas = true,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	static private Harmony Harmony { get; } = new("VSRun");

	static private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		LogExceptionDetails(e.ExceptionObject as Exception, "UnhandledException: " + sender);
	}

	public static void Run(string[] args) {
		Console.WriteLine("VS Run Main");
		Console.WriteLine($"Dotnet Version: {RuntimeInformation.FrameworkDescription}");
		try {
			Config = JsonSerializer.Deserialize<RunConfig>(Environment.GetEnvironmentVariable("RUN_CONFIG")!, ConfigJson);

			GamePaths.DataPath = Config.VintageStoryDataPath;
			ClientSettings.ModPaths = ["Mods", GamePaths.DataPathMods];

			switch (Config.ExecutableType) {
				case ExecutableTypeEnum.InitData: {
					InitData();
					break;
				}

				case ExecutableTypeEnum.StartGame: {
					StartGame(args);
					break;
				}
				default: throw new ArgumentOutOfRangeException();
			}
		} catch (Exception? ex) {
			LogExceptionDetails(ex, "Main");
		} finally {
			SafeConsoleErrorWriteLine("VSRun: Main finished.");
		}
	}

	public static void InitData() {
		GamePaths.EnsurePathsExist();
		DidDeserialize(ClientSettings.Inst);
	}

	[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "DidDeserialize")]
	static extern void DidDeserialize(ClientSettings inst);

	static private void ApplyAccountAndPatches() {
		Harmony.PatchCategory(typeof(Program).Assembly, "ServerSystemLoadConfig");

		if (Config.UseAnsiLogger) {
			Harmony.PatchCategory(typeof(Program).Assembly, nameof(Console));
		}

		if (Config.Account is not null) {
			var account = Config.Account;
			if (!string.IsNullOrEmpty(account.Email)) {
				ClientSettings.UserEmail = account.Email;
			}

			if (!string.IsNullOrEmpty(account.SessionKey)) {
				ClientSettings.Sessionkey = account.SessionKey;
			}

			if (!string.IsNullOrEmpty(account.SessionSignature)) {
				ClientSettings.SessionSignature = account.SessionSignature;
			}

			if (account.HasGameServer) {
				ClientSettings.HasGameServer = account.HasGameServer;
			}

			if (!string.IsNullOrEmpty(account.Uid)) {
				ClientSettings.PlayerUID = account.Uid;
			}

			if (!string.IsNullOrEmpty(account.PlayerName)) {
				ClientSettings.PlayerName = account.PlayerName;
			}

			if (!string.IsNullOrEmpty(account.Entitlements)) {
				ClientSettings.Entitlements = account.Entitlements;
			}

			if (account.Offline) {
				ClientSettings.PlayerUID = account.PlayerName!;
				Console.WriteLine("离线模式");
				Harmony.PatchCategory(typeof(Program).Assembly, "Offline");
			}
		}
	}

#if NET10_0_OR_GREATER
	[UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "state")]
	static extern ref Dictionary<MethodBase, byte[]>? HarmonySharedState(
		[UnsafeAccessorType("HarmonyLib.HarmonySharedState, 0Harmony")] object? _);
#endif

	public static void StartGame(string[] args) {
		try {
#if NET10_0_OR_GREATER
			HarmonySharedState(null)?.Clear();
#else
			var state = AccessTools.Field(AccessTools.TypeByName("HarmonyLib.HarmonySharedState"), "state");
			(state?.GetValue(null) as Dictionary<MethodBase, byte[]>)?.Clear();
#endif
		} catch (Exception e) {
			Console.Error.WriteLine("VSRun: harmony shared state reset failed: " + e.Message);
		}

		var assembly = Assembly.LoadFrom(Path.Combine(Config.VintageStoryPath, Config.AssemblyPath));
		var assemblyName = assembly.GetName().Name;

		var mainMethod = assembly.EntryPoint;
		switch (assemblyName) {
			case "EarlyModToolKit": {
				var field = AccessTools.Field(mainMethod?.DeclaringType, "basepath");
				field?.SetValue(null, Config.VintageStoryPath);
				break;
			}
		}

		Console.WriteLine("VS Run StartGame");
		mainMethod?.Invoke(null, [args]);
	}

	internal static void OnStartupHook() {
		try {
			AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

			var raw = Environment.GetEnvironmentVariable("RUN_CONFIG");
			if (string.IsNullOrEmpty(raw)) {
				return;
			}

			Config = JsonSerializer.Deserialize<RunConfig>(raw, ConfigJson);
			var assemblyPath = string.IsNullOrEmpty(Config.AssemblyPath) ? "Vintagestory.dll" : Config.AssemblyPath;
			var game = Assembly.LoadFrom(Path.Combine(Config.VintageStoryPath, assemblyPath));
			AdoptGameIdentity(game, Config.VintageStoryPath);

			if (Config.ExecutableType != ExecutableTypeEnum.StartGame) {
				return;
			}

			GamePaths.DataPath = Config.VintageStoryDataPath;
			ClientSettings.ModPaths = ["Mods", GamePaths.DataPathMods];
			ApplyAccountAndPatches();
		} catch (Exception e) {
			LogExceptionDetails(e, "StartupHook");
		}
	}

	static private void AdoptGameIdentity(Assembly game, string gameDirectory) {
		try {
#if NET10_0_OR_GREATER
			Assembly.SetEntryAssembly(game);
#else
			typeof(Assembly).GetMethod("SetEntryAssembly", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
				[typeof(Assembly)])?.Invoke(null, [game]);
#endif
		} catch (Exception e) {
			Console.Error.WriteLine("VSRun: SetEntryAssembly failed: " + e.Message);
		}

		OverrideProcessPath(gameDirectory);
	}

	static private void OverrideProcessPath(string gameDirectory) {
		try {
			var executable = Path.Combine(gameDirectory,
				RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Vintagestory.exe" : "Vintagestory");
#if NET10_0_OR_GREATER
			ProcessPath(null) = executable;
#else
			typeof(Environment).GetField("s_processPath", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, executable);
#endif
		} catch (Exception e) {
			Console.Error.WriteLine("VSRun: process path override failed: " + e.Message);
		}
	}

#if NET10_0_OR_GREATER
	[UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "s_processPath")]
	static extern ref string? ProcessPath([UnsafeAccessorType("System.Environment")] object? _);
#endif

	static private string GenerateSha256Uid(string input) {
		var inputBytes = Encoding.UTF8.GetBytes(input);

		var hashBytes = SHA256.HashData(inputBytes);

		var sb = new StringBuilder(hashBytes.Length * 2);
		foreach (var b in hashBytes) {
			sb.Append(b.ToString("x2"));
		}

		return sb.ToString();
	}

	static private void SafeConsoleErrorWriteLine(string message) {
		try {
			Console.WriteLine(message);
		} catch {
			// ignored
		}
	}

	static private void LogExceptionDetails(Exception? ex, string context, bool isCritical = true) {
		var title = isCritical ? "CRITICAL EXCEPTION" : "EXCEPTION";
		SafeConsoleErrorWriteLine($"--- VSRun: {title} in {context} ---");

		var currentEx = ex;
		var level = 0;
		while (currentEx != null) {
			if (level > 0) {
				SafeConsoleErrorWriteLine($"--- VSRun: Inner Exception (Level {level}) ---");
			}

			SafeConsoleErrorWriteLine($"VSRun: Type: {currentEx.GetType().FullName}");
			SafeConsoleErrorWriteLine($"VSRun: Message: {currentEx.Message}");
			SafeConsoleErrorWriteLine($"VSRun: StackTrace: {currentEx.StackTrace}");

			if (currentEx is ReflectionTypeLoadException rtle) {
				SafeConsoleErrorWriteLine("VSRun: ReflectionTypeLoadException LoaderExceptions:");
				if (rtle.LoaderExceptions != null) {
					foreach (var loaderEx in rtle.LoaderExceptions) {
						if (loaderEx == null) {
							continue;
						}

						SafeConsoleErrorWriteLine($"  LoaderEx Type: {loaderEx.GetType().FullName}");
						SafeConsoleErrorWriteLine($"  LoaderEx Message: {loaderEx.Message}");
						SafeConsoleErrorWriteLine($"  LoaderEx StackTrace: {loaderEx.StackTrace}");
					}
				} else {
					SafeConsoleErrorWriteLine("  (LoaderExceptions array is null)");
				}
			}

			currentEx = currentEx.InnerException;
			level++;
		}

		SafeConsoleErrorWriteLine($"--- VSRun: End of {title} in {context} ---");

		try {
			var tempPath = Environment.GetEnvironmentVariable("TMP") ??
				Environment.GetEnvironmentVariable("TEMP") ??
				(OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp");
			if (string.IsNullOrEmpty(AppContext.BaseDirectory) && string.IsNullOrEmpty(tempPath)) {
				tempPath = ".";
			} else if (string.IsNullOrEmpty(tempPath)) {
				tempPath = AppContext.BaseDirectory;
			}


			var errorFilePath = Path.Combine(tempPath, $"VSRUN_CRASH_REPORT_{DateTime.UtcNow:yyyyMMddHHmmssfff}.txt");

			var sb = new StringBuilder();
			sb.AppendLine($"Timestamp: {DateTime.UtcNow}");
			sb.AppendLine($"Context: {context}");
			currentEx = ex;
			level = 0;
			while (currentEx != null) {
				if (level > 0) {
					sb.AppendLine($"--- Inner Exception (Level {level}) ---");
				}

				sb.AppendLine($"Type: {currentEx.GetType().FullName}");
				sb.AppendLine($"Message: {currentEx.Message}");
				sb.AppendLine($"StackTrace: {currentEx.StackTrace}");
				if (currentEx is ReflectionTypeLoadException rtle && rtle.LoaderExceptions != null) {
					sb.AppendLine("ReflectionTypeLoadException LoaderExceptions:");
					foreach (var loaderEx in rtle.LoaderExceptions) {
						if (loaderEx == null) {
							continue;
						}

						sb.AppendLine($"  LoaderEx Type: {loaderEx.GetType().FullName}");
						sb.AppendLine($"  LoaderEx Message: {loaderEx.Message}");
					}
				}

				currentEx = currentEx.InnerException;
				level++;
			}

			File.WriteAllText(errorFilePath, sb.ToString());
			SafeConsoleErrorWriteLine($"VSRun: Detailed error report saved to {errorFilePath}");
		} catch (Exception logEx) {
			SafeConsoleErrorWriteLine($"VSRun: Failed to write crash report to file: {logEx.Message}");
		}
	}
}

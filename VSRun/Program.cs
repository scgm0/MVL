using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using SharedLibrary;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

// ReSharper disable UnusedMember.Global

namespace VSRun;

public static class Program {
	public static RunConfig Config { get; } =
		JsonSerializer.Deserialize<RunConfig>(Environment.GetEnvironmentVariable("RUN_CONFIG")!,
			new JsonSerializerOptions(JsonSerializerDefaults.Web) {
				AllowTrailingCommas = true,
				WriteIndented = true,
				Converters = {
					new JsonStringEnumConverter()
				}
			});

	public static string[] AssemblyPaths { get; } = [
		Config.VintageStoryPath,
		Path.Combine(Config.VintageStoryPath, "Mods"),
		Path.Combine(Config.VintageStoryPath, "Lib"),
		Path.Combine(Config.VintageStoryDataPath, "Mods")
	];

	public static Harmony Harmony { get; } = new("VSRun");

	[ModuleInitializer]
	public static void Initialize() { AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve; }

	public static void Main(string[] args) {
		AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Config.VintageStoryPath);
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
	}

	public static void InitData() {
		GamePaths.EnsurePathsExist();
		var type = typeof(ClientSettings);
		var didDeserialize = type.GetMethod("DidDeserialize", BindingFlags.NonPublic | BindingFlags.Instance)!;
		didDeserialize.Invoke(ClientSettings.Inst, null);
	}

	public static void StartGame(string[] args) {
		Harmony.PatchCategory("ServerSystemLoadConfig");

		if (Config.UseAnsiLogger) {
			Harmony.PatchCategory(nameof(Console));
		}

		if (Config.Account is not null) {
			var account = Config.Account;
			ClientSettings.UserEmail = account.Email;
			ClientSettings.Sessionkey = account.SessionKey;
			ClientSettings.SessionSignature = account.SessionSignature;
			ClientSettings.HasGameServer = account.HasGameServer;
			ClientSettings.PlayerUID = account.Uid;
			ClientSettings.PlayerName = account.PlayerName;
			ClientSettings.Entitlements = account.Entitlements;

			if (account.Offline) {
				Console.WriteLine("VS Run: 离线模式");
				Harmony.PatchCategory("Offline");
			}
		}

		var harmonyAssembly = typeof(Harmony).Assembly;
		var harmonySharedStateType = harmonyAssembly.GetType("HarmonyLib.HarmonySharedState");
		var stateFieldInfo = harmonySharedStateType?.GetField("state", BindingFlags.Static | BindingFlags.NonPublic);
		var stateInstance = stateFieldInfo?.GetValue(null) as Dictionary<MethodBase, byte[]>;
		stateInstance?.Clear();

		var assembly = Assembly.LoadFile(Path.Combine(Config.VintageStoryPath, Config.AssemblyPath));
		var assemblyName = assembly.GetName().Name;

		var mainMethod = assembly.EntryPoint;
		switch (assemblyName) {
			case "EarlyModToolKit": {
				var field = AccessTools.Field(mainMethod?.DeclaringType, "basepath");
				field?.SetValue(null, Config.VintageStoryPath);
				break;
			}
		}

		Console.WriteLine("VS Run");
		mainMethod?.Invoke(null, [args]);
	}

	public static Assembly? AssemblyResolve(object? sender, ResolveEventArgs args) {
		var dll = new AssemblyName(args.Name).Name + ".dll";
		foreach (var folder in AssemblyPaths) {
			var dllPath = Path.Combine(folder, dll);
			var path = File.Exists(dllPath) ? dllPath : null;
			if (path == null) continue;
			try {
				return Assembly.LoadFrom(path);
			} catch (Exception ex) {
				throw new($"无法从'{path}'加载程序集'{args.Name}'", ex);
			}
		}

		return null;
	}
}
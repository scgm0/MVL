using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using SharedLibrary;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

// ReSharper disable UnusedMember.Global

namespace VSRun;

public static class Program {
	public static RunConfig Config { get; } =
		JsonSerializer.Deserialize<RunConfig>(Environment.GetEnvironmentVariable("RUN_CONFIG")!);

	public static string[] AssemblyPaths { get; } = {
		Config.VintageStoryPath,
		Path.Combine(Config.VintageStoryPath, "Mods"),
		Path.Combine(Config.VintageStoryPath, "Lib"),
		Path.Combine(Config.VintageStoryDataPath, "Mods")
	};

	public static Harmony? Harmony { get; set; }

	[ModuleInitializer]
	public static void Initialize() { AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve; }

	public static void Main(string[] args) {
		Harmony = new("VSRun");
		AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Config.VintageStoryPath);
		GamePaths.DataPath = Config.VintageStoryDataPath;
		switch (Config.ExecutableType) {
			case ExecutableTypeEnum.InitData: {
				InitData();
				break;
			}

			case ExecutableTypeEnum.StartGame: {
				if (Config.UseAnsiLogger) {
					Harmony.PatchCategory(nameof(Console));
				}
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
		mainMethod?.Invoke(null,
			new object?[] { args });
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
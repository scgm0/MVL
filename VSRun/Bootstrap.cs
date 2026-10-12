using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace VSRun;

static class Bootstrap {
	static private string[] AssemblyPaths { get; set; } = [];

	[ModuleInitializer]
	internal static void Initialize() {
		Console.OutputEncoding = Encoding.UTF8;
		Console.WriteLine("VS Run Initialize");

		var vsPath = Environment.GetEnvironmentVariable("VINTAGE_STORY_PATH") ?? string.Empty;
		if (vsPath.Length > 0) {
			AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", vsPath);
			Directory.SetCurrentDirectory(vsPath);
		}

		AssemblyPaths = [
			Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location) ?? vsPath,
			vsPath,
			Path.Combine(vsPath, "Mods"),
			Path.Combine(vsPath, "Lib")
		];
		AppDomain.CurrentDomain.AssemblyResolve += Resolve;

		RemoveOwnHook();
	}

	static private void RemoveOwnHook() {
		var ownPath = typeof(Bootstrap).Assembly.Location;
		var previous = Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS");
		var filtered = WithoutOwnHook(previous, ownPath);
		if (!string.Equals(previous, filtered, StringComparison.Ordinal)) {
			Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", filtered);
		}
	}

	static private string? WithoutOwnHook(string? hooks, string ownPath) {
		if (hooks is null) {
			return null;
		}

		var entries = hooks.Split(Path.PathSeparator);
		return string.Join(Path.PathSeparator,
			entries.Where(entry =>
				!Path.IsPathFullyQualified(entry) ||
				!string.Equals(Path.GetFullPath(entry),
					Path.GetFullPath(ownPath),
					OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)));
	}

	static private Assembly? Resolve(object? sender, ResolveEventArgs args) {
		var dll = new AssemblyName(args.Name).Name + ".dll";
		foreach (var folder in AssemblyPaths) {
			var dllPath = Path.Combine(folder, dll);
			if (!File.Exists(dllPath)) {
				continue;
			}

			try {
				return Assembly.LoadFrom(dllPath);
			} catch (Exception ex) {
				throw new($"无法从'{dllPath}'加载程序集'{args.Name}'", ex);
			}
		}

		return null;
	}
}
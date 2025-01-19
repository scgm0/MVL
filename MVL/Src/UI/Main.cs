using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using SharedLibrary;

namespace MVL.UI;

public partial class Main : NativeWindowUtility {
	private Vector2I _rootMinSize = new(960, 540);

	public static BaseConfig BaseConfig { get; } =
		BaseConfig.Load(OS.GetUserDataDir().PathJoin("data.json"));

	private Window.InstalledGamesImport? _installedGamesImport;

	[Export]
	private Texture2D? _iconTexture;

	[Export]
	private PackedScene? _installedGamesImportScene;

	[Export]
	private MarginContainer? _marginContainer;

	[Export]
	private Button? MinButton { get; set; }

	[Export]
	private Button? CloseButton { get; set; }

	[Export]
	private ShaderMaterial? _roundMaterial;

	public int ShadowSize {
		get;
		set {
			field = value;
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginLeft, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginRight, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginTop, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginBottom, value);
			_roundMaterial?.SetShaderParameter(StringNames.ShadowSize, value);
		}
	} = 5;

	public static Main? Instance { get; private set; }

	public static Process? GameProcess { get; set; }
	public static Dictionary<string, ReleaseInfo> ReleaseInfos { get; } = new();

	public static Dictionary<string, ModpackConfig> ModpackConfigs { get; } = new();
	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();

	public Main() { Instance = this; }

	public override void _Ready() {
		base._Ready();
		Utils.Help.NullExceptionHelper.NotNull(_iconTexture, _installedGamesImportScene, MinButton, CloseButton);
		DisplayServer.SetIcon(_iconTexture.GetImage());

		_rootMinSize = SceneTree.Root.Size - new Vector2I(90, 90);
		SceneTree.Root.MinSize = _rootMinSize;
		SceneTree.Root.SizeChanged += RootOnSizeChanged;

		Tween? shadowTween = null;
		var shadowColor = Colors.Black;
		var shadowCallable = Callable.From((Color color) => {
			shadowColor = color;
			_roundMaterial!.SetShaderParameter(StringNames.ColorShadow, color);
		});
		SceneTree.Root.FocusEntered += () => {
			shadowTween?.Stop();
			shadowTween = CreateTween();
			shadowTween.TweenMethod(
				shadowCallable,
				shadowColor,
				Colors.Black,
				0.3f
			);
		};
		SceneTree.Root.FocusExited += () => {
			shadowTween?.Stop();
			shadowTween = CreateTween();
			shadowTween.TweenMethod(
				shadowCallable,
				shadowColor,
				Colors.Transparent,
				0.3f
			);
		};

		MinButton.Pressed += SceneTree.Root.Minimize;
		CloseButton.Pressed += () => SceneTree.Quit();
	}

	public void Start() {
		CheckReleaseInfo();
		CheckModpackConfig();

		if (BaseConfig.Release.Count == 0 && Window.InstalledGamesImport.InstalledGamePaths.Length > 0) {
			_ = ImportInstalledGames();
		}
	}

	private void RootOnSizeChanged() {
		_roundMaterial!.SetShaderParameter(StringNames.WindowSize, SceneTree.Root.Size);
		if (ShadowSize != 5 || SceneTree.Root.Size != DisplayServer.ScreenGetUsableRect().Size) {
			ShadowSize = 5;
			_roundMaterial!.SetShaderParameter(StringNames.CornerRadius, new Vector4(12, 12, 12, 12));
		} else {
			ShadowSize = 0;
			_roundMaterial!.SetShaderParameter(StringNames.CornerRadius, Vector4.Zero);
		}
	}

	public async Task<Window.InstalledGamesImport> ImportInstalledGames(IEnumerable<string>? gamePaths = null) {
		if (_installedGamesImport is null) {
			_installedGamesImport = _installedGamesImportScene!.Instantiate<Window.InstalledGamesImport>();
			AddChild(_installedGamesImport);
			_installedGamesImport.Import += paths => {
				if (paths.Length == 0) {
					return;
				}

				BaseConfig.Release.AddRange(paths);
				CheckReleaseInfo();
			};
		}

		if (gamePaths != null) {
			await _installedGamesImport.ShowInstalledGames(gamePaths);
		} else {
			await _installedGamesImport.ShowInstalledGames();
		}

		return _installedGamesImport;
	}

	public static void CheckReleaseInfo() {
		var list = BaseConfig.Release.ToList();
		foreach (var path in list) {
			var gameVersion = GameVersion.FromGamePath(path);
			if (gameVersion is null) {
				ReleaseInfos.Remove(path);
				BaseConfig.Release.Remove(path);
			} else {
				var info = ReleaseInfos.GetValueOrDefault(path,
					new() {
						Path = path,
						Name = path.GetFile()
					});
				info.Version = gameVersion.Value;
				ReleaseInfos[path] = info;
			}
		}

		BaseConfig.Save(BaseConfig);
	}

	public static void CheckModpackConfig() {
		var list = BaseConfig.Modpack.ToList();
		foreach (var path in list) {
			if (!DirAccess.DirExistsAbsolute(path)) {
				ModpackConfigs.Remove(path);
				BaseConfig.Modpack.Remove(path);
				continue;
			}

			var modPack = ModpackConfigs.GetValueOrDefault(path, ModpackConfig.Load(path));
			if (modPack.ReleasePath is not null && (!ReleaseInfos.TryGetValue(modPack.ReleasePath, out var value) ||
				value.Version != modPack.Version)) {
				modPack.ReleasePath = null;
			}

			if (modPack.Version is null) {
				ModpackConfigs.Remove(path);
				BaseConfig.Modpack.Remove(path);
				continue;
			}

			var info = ReleaseInfos.Values.FirstOrDefault(i => i?.Version == modPack.Version, null);
			modPack.Path = path;
			modPack.Version = info?.Version;
			modPack.ReleasePath ??= info?.Path;
			modPack.Name ??= path.GetFile();

			ModpackConfig.Save(modPack);
			ModpackConfigs[path] = modPack;
		}

		BaseConfig.Save(BaseConfig);
	}

	public static string? GetGameConfigName(string gamePath) {
		try {
			var assembly = AssemblyDefinition.ReadAssembly(gamePath.PathJoin("VintagestoryLib.dll"));
			var type = assembly.MainModule.GetType("Vintagestory.Client.NoObf.ClientSettings");
			var properties = type.Properties.ToDictionary(definition => definition.Name, definition => definition.GetMethod);
			return properties["FileName"].Body.Instructions.ToArray()
				.Where(instruction => instruction.OpCode.OperandType == OperandType.InlineString)
				.Select(instruction => (string)instruction.Operand).FirstOrDefault();
		} catch {
			return null;
		}
	}

	public static DirAccess CopyVsRun() {
		var tmp = DirAccess.CreateTemp("VSRun");
		var tmpRunPath = tmp.GetCurrentDir();
		const string vsRunPath = "res://Misc/VSRun";
		foreach (var file in DirAccess.GetFilesAt(vsRunPath)) {
			var fromPath = vsRunPath.PathJoin(file);
			tmp.Copy(fromPath, tmpRunPath.PathJoin(file));
		}

		return tmp;
	}

	public static Process VsRun(RunConfig runConfig, string command) {
		GD.PrintS("Execute:", command);
		GD.Print(runConfig);

		var startInfo = new ProcessStartInfo {
#if GODOT_WINDOWS
			FileName = "cmd",
			Arguments = $"/c \"{command}\"",
#else
			FileName = "bash",
			Arguments = $"-c \"{command}\"",
#endif

			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = runConfig.VintageStoryPath,
			Environment = {
				["VINTAGE_STORY"] = runConfig.VintageStoryPath,
				["RUN_CONFIG"] = JsonSerializer.Serialize(runConfig, SourceGenerationContext.Default.RunConfig)
			}
		};

		var process = new Process {
			StartInfo = startInfo
		};

		process.ErrorDataReceived += (_, args) => { GD.PrintErr(args.Data); };

		process.OutputDataReceived += (_, args) => {
			if (args.Data == null) return;
			GD.PrintRich(args.Data.ConvertAnsiToBbCode());
			if (args.Data.EndsWith("Client logger started.")) {
				Dispatcher.SynchronizationContext.Post(_ => {
					SceneTree.Root.Minimize();
				}, null);
			}
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		return process;
	}

	public static async Task InitData(string gamePath, string dataPath) {
		try {
			using var tmp = CopyVsRun();
			using var process = VsRun(new() {
					VintageStoryPath = gamePath,
					VintageStoryDataPath = dataPath,
					ExecutableType = ExecutableTypeEnum.InitData
				},
				$"dotnet {tmp.GetCurrentDir().PathJoin("VSRun.dll")}");
			GameProcess = process;
			await process.WaitForExitAsync();
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}

	public static async Task StartGame(
		string gamePath,
		string dataPath,
		string command = "%command%",
		string assembleName = "Vintagestory.dll") {
		try {
			using var tmp = CopyVsRun();
			using var process = VsRun(new() {
					VintageStoryPath = gamePath,
					VintageStoryDataPath = dataPath,
					AssemblyPath = assembleName.Replace("%game_path%", gamePath).Replace("%data_path", dataPath),
					ExecutableType = ExecutableTypeEnum.StartGame
				},
				command.Replace("%command%", $"dotnet {tmp.GetCurrentDir().PathJoin("VSRun.dll")}"));
			GameProcess = process;
			await process.WaitForExitAsync();
			GameProcess = null;
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}
}
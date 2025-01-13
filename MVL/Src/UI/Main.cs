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

	static private readonly BaseConfig BaseConfig =
		BaseConfig.Load(OS.GetUserDataDir().PathJoin("data.json"));

	private InstalledGamesImport? _installedGamesImport;

	[Export]
	private Texture2D? _iconTexture;

	[Export]
	private PackedScene? _installedGamesImportScene;

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
			AddThemeConstantOverride(StringNames.MarginLeft, value);
			AddThemeConstantOverride(StringNames.MarginRight, value);
			AddThemeConstantOverride(StringNames.MarginTop, value);
			AddThemeConstantOverride(StringNames.MarginBottom, value);
			_roundMaterial!.SetShaderParameter(StringNames.ShadowSize, value);
		}
	} = 5;

	public static Main? Instance { get; private set; }

	public static Dictionary<string, ReleaseInfo> Release { get; } = new();

	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();

	public Main() { Instance = this; }

	public override void _Ready() {
		base._Ready();
		Utils.Help.NullExceptionHelper.NotNull(_iconTexture, _installedGamesImportScene, MinButton, CloseButton);
		_rootMinSize = SceneTree.Root.Size - new Vector2I(90, 90);
		SceneTree.Root.MinSize = _rootMinSize;
		SceneTree.Root.SizeChanged += RootOnSizeChanged;
		DisplayServer.SetIcon(_iconTexture.GetImage());
		MinButton.Pressed += SceneTree.Root.Minimize;
		CloseButton.Pressed += () => SceneTree.Quit();
		if (BaseConfig.Release.Count == 0 && InstalledGamesImport.InstalledGamePaths.Length > 0) {
			_ = ImportInstalledGames();
		}

		CheckGameVersion();
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

	public async Task<InstalledGamesImport> ImportInstalledGames() {
		if (_installedGamesImport is null) {
			_installedGamesImport = _installedGamesImportScene!.Instantiate<InstalledGamesImport>();
			AddChild(_installedGamesImport);
			_installedGamesImport.Import += paths => {
				if (paths.Length == 0) {
					return;
				}

				foreach (var gamePath in paths) {
					var info = new ReleaseInfo {
						Path = gamePath,
						Version = GameVersion.FromGamePath(gamePath)!.Value
					};
					BaseConfig.Release.Add(info);
				}

				BaseConfig.Save(BaseConfig);
				CheckGameVersion();
			};
		}

		await _installedGamesImport.Show();
		_ = _installedGamesImport.ShowInstalledGames();
		return _installedGamesImport;
	}

	public static void CheckGameVersion() {
		var list = BaseConfig.Release.ToList();
		foreach (var info in list) {
			var path = info.Path;
			var gameVersion = GameVersion.FromGamePath(path);
			if (gameVersion is null) {
				Release.Remove(path);
				BaseConfig.Release.Remove(info);
			} else {
				info.Version = gameVersion.Value;
				Release[path] = info;
			}
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
			if (args.Data != null) {
				GD.PrintRich(args.Data.ConvertAnsiToBbCode());
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
			await process.WaitForExitAsync();
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}
}
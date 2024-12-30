using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharedLibrary;
using 复古物语启动器.Utils;
using 复古物语启动器.Utils.Extensions;
using 复古物语启动器.Utils.Game;
using 复古物语启动器.Utils.Help;

namespace 复古物语启动器.UI;

public partial class Main : Control {
	private Vector2I _rootMinSize = new(960, 540);
	static private readonly ConfigData BaseConfig = new(AppDomain.CurrentDomain.BaseDirectory.PathJoin("data.cfg"));
	private InstalledGamesImport? _installedGamesImport;

	[Export]
	private Texture2D? _iconTexture;

	[Export]
	private PackedScene? _installedGamesImportScene;

	public static Main? Instance { get; private set; }
	public static Dictionary<string, GameVersion> GameVersions { get; } = new();

	public Main() { Instance = this; }

	public override void _Ready() {
		NullExceptionHelper.NotNull(_iconTexture, _installedGamesImportScene);
		_rootMinSize = GetTree().Root.Size - new Vector2I(100, 100);
		GetTree().Root.MinSize = _rootMinSize;
		DisplayServer.SetIcon(_iconTexture.GetImage());
		if (BaseConfig.GamePaths.Length == 0 && InstalledGamesImport.InstalledGamePaths.Length > 0) {
			_ = ImportInstalledGames();
		}

		CheckGameVersion();
	}

	public async Task<InstalledGamesImport> ImportInstalledGames() {
		if (_installedGamesImport is null) {
			_installedGamesImport = _installedGamesImportScene!.Instantiate<InstalledGamesImport>();
			AddChild(_installedGamesImport);
			_installedGamesImport.Import += paths => {
				if (paths.Length == 0) {
					return;
				}

				var gamePaths = BaseConfig.GamePaths.ToList();
				gamePaths.AddRange(paths);
				BaseConfig.GamePaths = gamePaths.Distinct().ToArray();
				CheckGameVersion();
			};
		}

		await _installedGamesImport.Show();
		_ = _installedGamesImport.ShowInstalledGames();
		return _installedGamesImport;
	}

	public static void CheckGameVersion() {
		foreach (var gamePath in BaseConfig.GamePaths) {
			var gameVersion = GameVersion.FromGamePath(gamePath);
			if (gameVersion is null) {
				GameVersions.Remove(gamePath);
			} else {
				GameVersions[gamePath] = gameVersion.Value;
			}
		}
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
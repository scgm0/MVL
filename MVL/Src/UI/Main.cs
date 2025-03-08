using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MVL.UI.Item;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Config;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using MVL.Utils.Help;
using SharedLibrary;

namespace MVL.UI;

public partial class Main : NativeWindowUtility {
	public static BaseConfig BaseConfig { get; } =
		BaseConfig.Load(Paths.BaseConfigPath);

	private InstalledGamesImport? _installedGamesImport;

	[Export]
	private PackedScene? _loginWindowScene;

	[Export]
	private Texture2D? _iconTexture;

	[Export]
	private PackedScene? _installedGamesImportScene;

	[Export]
	private PackedScene? _accountSelectItemScene;

	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private MarginContainer? _marginContainer;

	[Export]
	private Button? _minButton;

	[Export]
	private Button? _closeButton;

	[Export]
	private ShaderMaterial? _roundMaterial;

	[Export]
	private Button? _accountButton;

	[Export]
	private Button? _accountSelectButton;

	[Export]
	private PanelContainer? _accountSelectContainer;

	[Export]
	private ScrollContainer? _accountSelectScrollContainer;

	[Export]
	private Button? _accountSelectAddButton;

	[Export]
	private VBoxContainer? _accountSelectListContainer;

	[Export]
	private AnimationPlayer? _accountSelectAnimationPlayer;

	public int ShadowSize {
		get;
		set {
			field = value;
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginLeft, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginRight, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginTop, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginBottom, value);
			_roundMaterial?.SetShaderParameter(StringNames.WindowTopLeft, new Vector2(value, value));
		}
	} = 5;

	public static Main? Instance { get; private set; }

	public static Process? CurrentGameProcess { get; set; }
	public static ModpackConfig? CurrentModpack { get; set; }

	public static Dictionary<string, ReleaseInfo> ReleaseInfos { get; } = new();

	public static Dictionary<string, ModpackConfig> ModpackConfigs { get; } = new();

	public static Dictionary<string, Account> Accounts { get; } = new();

	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();

	public Main() { Instance = this; }

	public override void _Ready() {
		if (!Start.IsRunning) {
			QueueFree();
			return;
		}

		base._Ready();

		_loginWindowScene.NotNull();
		_iconTexture.NotNull();
		_installedGamesImportScene.NotNull();
		_accountSelectItemScene.NotNull();
		_minButton.NotNull();
		_closeButton.NotNull();
		_accountButton.NotNull();
		_accountSelectButton.NotNull();
		_accountSelectContainer.NotNull();
		_accountSelectScrollContainer.NotNull();
		_accountSelectAddButton.NotNull();
		_accountSelectListContainer.NotNull();
		_accountSelectAnimationPlayer.NotNull();

		DisplayServer.SetIcon(_iconTexture.GetImage());

		SceneTree.Root.MinSize = SceneTree.Root.Size - new Vector2I(40, 40);
		SceneTree.Root.SizeChanged += RootOnSizeChanged;

		Tween? shadowTween = null;
		var shadowColor = Colors.Black;
		var shadowCallable = Callable.From((Color color) => {
			shadowColor = color;
			_roundMaterial!.SetShaderParameter(StringNames.ShadowColor, color);
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
				Colors.Black with { A = 0.5f },
				0.3f
			);
		};

		_minButton.Pressed += SceneTree.Root.Minimize;
		_closeButton.Pressed += () => SceneTree.Quit();
		_accountButton.Pressed += AccountButtonOnPressed;
		_accountSelectButton.Pressed += AccountSelectButtonOnPressed;
		_accountSelectAddButton.Pressed += AccountSelectAddButtonOnPressed;

		FlurlHttp.Clients.WithDefaults(builder => {
			builder.ConfigureInnerHandler(handler => {
				handler.Proxy = string.IsNullOrEmpty(BaseConfig.ProxyUrl) ? null : new WebProxy(BaseConfig.ProxyUrl);
			});
		});
	}

	private async void AccountSelectButtonOnPressed() {
		_accountSelectAnimationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_accountSelectAnimationPlayer, AnimationMixer.SignalName.AnimationFinished);
		_accountButton!.ButtonPressed = false;
		_accountSelectButton!.Hide();
		foreach (var child in _accountSelectListContainer!.GetChildren()) {
			child.QueueFree();
		}

		CheckAccount();
	}

	private async void AccountSelectAddButtonOnPressed() {
		var loginWindow = await OpenAccountSelectWindow();
		loginWindow.Login += async _ => {
			foreach (var child in _accountSelectListContainer!.GetChildren()) {
				child.QueueFree();
				await ToSignal(_accountSelectListContainer!, Container.SignalName.SortChildren);
			}
			await ToSignal(SceneTree, SceneTree.SignalName.ProcessFrame);
			AccountButtonOnPressed();
		};
	}

	private async void AccountButtonOnPressed() {
		if (BaseConfig.Account.Count == 0) {
			var loginWindow = await OpenAccountSelectWindow();
			loginWindow.Login += window => {
				BaseConfig.CurrentAccount = window.Account?.PlayerName ?? string.Empty;
				CheckAccount();
			};
			loginWindow.Hidden += () => { _accountButton!.ButtonPressed = false; };
		} else {
			_accountSelectContainer!.Size = _accountSelectContainer!.Size with { Y = 0 };
			_accountSelectContainer.Modulate = Colors.Transparent;
			_accountSelectScrollContainer!.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
			_accountSelectButton!.Show();
			_accountSelectContainer.GlobalPosition = _accountButton!.GlobalPosition with {
				Y = _accountButton.GlobalPosition.Y + _accountButton.Size.Y + 3
			};

			foreach (var account in BaseConfig.Account) {
				var accountItem = _accountSelectItemScene!.Instantiate<AccountSelectItem>();
				accountItem.Account = account;
				accountItem.Select += AccountItemOnSelect;
				accountItem.Edit += AccountItemOnEdit;
				accountItem.Remove += AccountItemOnRemove;
				_accountSelectListContainer!.AddChild(accountItem);
				await ToSignal(_accountSelectListContainer!, Container.SignalName.SortChildren);
			}
			await ToSignal(SceneTree, SceneTree.SignalName.ProcessFrame);

			_accountSelectScrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
			_accountSelectContainer.Modulate = Colors.White;

			var animation = _accountSelectAnimationPlayer!.GetAnimationLibrary("").GetAnimation(StringNames.Show);
			animation.TrackSetKeyValue(0, 0, new Vector2(_accountSelectContainer.Size.X, 0));
			animation.TrackSetKeyValue(0, 1, _accountSelectContainer.Size);
			animation.Length = (_accountSelectListContainer!.GetChildCount() + 1) * 0.05f;
			animation.TrackSetKeyTime(0, 1, animation.Length);
			_accountSelectAnimationPlayer.Play(StringNames.Show);
		}
	}

	private async Task<LoginWindow> OpenAccountSelectWindow(Account? account = null) {
		var loginWindow = _loginWindowScene!.Instantiate<LoginWindow>();
		loginWindow.Account = account;
		loginWindow.Visible = false;
		loginWindow.Login += AddAccount;
		loginWindow.Hidden += loginWindow.QueueFree;
		AddChild(loginWindow);
		await loginWindow.Show();
		return loginWindow;
	}

	private void AccountItemOnRemove(AccountSelectItem item) {
		var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
		confirmationWindow.Visible = false;
		confirmationWindow.Message = $"确定要删除账号 [color=#0078d7][b]{item.Account!.PlayerName}[/b][/color] 吗？";
		confirmationWindow.Confirm += async () => {
			await confirmationWindow.Hide();
			BaseConfig.Account.Remove(item.Account);

			if (BaseConfig.Account.Count > 0) {
				foreach (var child in _accountSelectListContainer!.GetChildren()) {
					child.QueueFree();
					await ToSignal(_accountSelectListContainer!, Container.SignalName.SortChildren);
				}
				await ToSignal(SceneTree, SceneTree.SignalName.ProcessFrame);
				AccountButtonOnPressed();
			} else {
				AccountSelectButtonOnPressed();
			}
		};
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		AddChild(confirmationWindow);
		_ = confirmationWindow.Show();
	}

	private async void AccountItemOnEdit(AccountSelectItem item) {
		var loginWindow = await OpenAccountSelectWindow(item.Account);
		loginWindow.Login += async _ => {
			foreach (var child in _accountSelectListContainer!.GetChildren()) {
				child.QueueFree();
				await ToSignal(_accountSelectListContainer!, Container.SignalName.SortChildren);
			}
			await ToSignal(SceneTree, SceneTree.SignalName.ProcessFrame);
			AccountButtonOnPressed();
		};
	}

	private void AccountItemOnSelect(AccountSelectItem item) {
		BaseConfig.CurrentAccount = item.Account!.PlayerName!;
		CheckAccount();
	}

	private void AddAccount(LoginWindow window) {
		var account = window.Account!;
		AddAccount(account);
	}

	private void AddAccount(Account account) {
		if (!BaseConfig.Account.Contains(account)) {
			BaseConfig.Account.Add(account);
		}

		CheckAccount();
	}

	public void CheckAccount() {
		Accounts.Clear();
		var list = BaseConfig.Account.ToList();
		foreach (var account in list) {
			var name = account.PlayerName!;
			if (Accounts.TryAdd(name, account) || account == Accounts[name]) continue;
			BaseConfig.Account.Remove(Accounts[name]);
			Accounts[name] = account;
		}

		if (BaseConfig.Account.Count == 0 || !Accounts.ContainsKey(BaseConfig.CurrentAccount)) {
			BaseConfig.CurrentAccount = string.Empty;
			_accountButton!.Text = "点击登录账号";
		} else {
			_accountButton!.Text = BaseConfig.CurrentAccount;
		}

		BaseConfig.Save(BaseConfig);
	}

	public void Init() {
		CheckReleaseInfo();
		CheckModpackConfig();
		CheckAccount();

		if (BaseConfig.Release.Count == 0 && InstalledGamesImport.InstalledGamePaths.Length > 0) {
			_ = ImportInstalledGames();
		}
	}

	private void RootOnSizeChanged() {
		_roundMaterial!.SetShaderParameter(StringNames.WindowExpandedSize, SceneTree.Root.Size);
		if (ShadowSize != 5 || SceneTree.Root.Mode != Godot.Window.ModeEnum.Maximized) {
			ShadowSize = 5;
			_roundMaterial!.SetShaderParameter(StringNames.HasRoundCorners, true);
		} else {
			ShadowSize = 0;
			_roundMaterial!.SetShaderParameter(StringNames.HasRoundCorners, false);
		}
	}

	public async Task<InstalledGamesImport> ImportInstalledGames(IEnumerable<string>? gamePaths = null) {
		if (_installedGamesImport is null) {
			_installedGamesImport = _installedGamesImportScene!.Instantiate<InstalledGamesImport>();
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
		BaseConfig.Release = BaseConfig.Release.Distinct().ToList();
		var snapshotPaths = BaseConfig.Release.ToList();

		foreach (var path in snapshotPaths.Select(lPath => lPath.NormalizePath())) {
			if (!GameVersion.TryFromGamePath(path, out var gameVersion)) {
				RemoveRelease(path);
				continue;
			}

			if (!ReleaseInfos.TryGetValue(path, out var releaseInfo)) {
				releaseInfo = new() {
					Path = path,
					Name = path.GetFile(),
					Version = gameVersion
				};
				ReleaseInfos[path] = releaseInfo;
			} else {
				releaseInfo.Version = gameVersion;
			}
		}

		BaseConfig.Save(BaseConfig);
	}


	static private void RemoveRelease(string path) {
		ReleaseInfos.Remove(path);
		BaseConfig.Release.Remove(path);
	}

	public static void CheckModpackConfig() {
		BaseConfig.Modpack = BaseConfig.Modpack.Distinct().ToList();
		var list = BaseConfig.Modpack.ToList();

		var versionLookup = ReleaseInfos.Values
			.ToLookup(info => info.Version);

		foreach (var path in list.Select(lPath => lPath.NormalizePath())) {
			if (!DirAccess.DirExistsAbsolute(path)) {
				RemoveModpack(path);
				continue;
			}

			var modPack = ModpackConfigs.GetValueOrDefault(path, ModpackConfig.Load(path));
			ModpackConfigs[path] = modPack;
			modPack.Path = path;
			modPack.Name ??= path.GetFile();

			if (modPack.ReleasePath != null &&
				(!ReleaseInfos.TryGetValue(modPack.ReleasePath, out var releaseInfo) ||
					releaseInfo.Version != modPack.Version)) {
				modPack.ReleasePath = null;
			}

			if (modPack.Version is null) {
				modPack.ReleasePath = null;
				continue;
			}

			var info = versionLookup[modPack.Version.Value].FirstOrDefault();
			modPack.ReleasePath ??= info?.Path;

			ModpackConfig.Save(modPack);
		}

		BaseConfig.Save(BaseConfig);
	}

	static private void RemoveModpack(string path) {
		ModpackConfigs.Remove(path);
		BaseConfig.Modpack.Remove(path);
	}

	public static string? GetGameConfigName(string gamePath) {
		try {
			var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(gamePath, "VintagestoryLib.dll"));
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
			tmp.Copy(fromPath, tmpRunPath.PathJoin(file).NormalizePath());
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
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};

		process.ErrorDataReceived += (_, args) => { GD.PrintErr(args.Data); };

		process.OutputDataReceived += (_, args) => {
			if (args.Data == null) return;
			GD.PrintRich(args.Data.ConvertAnsiToBbCode());
			if (args.Data.EndsWith("Client logger started.")) {
				Dispatcher.SynchronizationContext.Post(_ => { SceneTree.Root.Minimize(); }, null);
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
				$"dotnet \"{Path.Combine(tmp.GetCurrentDir(), "VSRun.dll").NormalizePath()}\"");
			CurrentGameProcess = process;
			await process.WaitForExitAsync();
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}

	public static void StartGame(
		string gamePath,
		string dataPath,
		string command = "%command%",
		string assembleName = "Vintagestory.dll") {
		try {
			var tmp = CopyVsRun();
			var process = VsRun(new() {
					VintageStoryPath = gamePath,
					VintageStoryDataPath = dataPath,
					AssemblyPath = assembleName.Replace("%game_path%", gamePath).Replace("%data_path", dataPath),
					ExecutableType = ExecutableTypeEnum.StartGame,
					Account = string.IsNullOrEmpty(BaseConfig.CurrentAccount) ? null : Accounts[BaseConfig.CurrentAccount]
				},
				command.Replace("%command%", $"dotnet \"{Path.Combine(tmp.GetCurrentDir(), "VSRun.dll").NormalizePath()}\""));
			CurrentGameProcess = process;
			process.Exited += (_, _) => {
				tmp.Dispose();
				process.Dispose();
				CurrentGameProcess = null;
				CurrentModpack = null;
			};
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}
}
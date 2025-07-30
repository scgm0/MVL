using System;
using System.Collections.Concurrent;
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
using HttpClient = System.Net.Http.HttpClient;

namespace MVL.UI;

public partial class Main : NativeWindowUtility {
	public static BaseConfig BaseConfig { get; } =
		BaseConfig.Load(Paths.BaseConfigPath);

	private InstalledGamesImport? _installedGamesImport;

	[Export]
	private PackedScene? _loginWindowScene;

	[Export]
	private PackedScene? _installedGamesImportScene;

	[Export]
	private PackedScene? _accountSelectItemScene;

	[Export]
	private PackedScene? _confirmationWindowScene;

	[Export]
	private MarginContainer? _marginContainer;

	[Export]
	private SubViewport? _subViewport;

	[Export]
	private Button? _minButton;

	[Export]
	private Button? _closeButton;

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

	[Export]
	public ShaderMaterial? WindowMaterial { get; set; }

	public int ShadowSize {
		get;
		set {
			field = value;
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginLeft, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginRight, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginTop, value);
			_marginContainer?.AddThemeConstantOverride(StringNames.MarginBottom, value);
			WindowMaterial?.SetShaderParameter(StringNames.WindowTopLeft, new Vector2(value, value));
		}
	} = 5;

	public static Main? Instance { get; private set; }

	public static Process? CurrentGameProcess { get; set; }
	public static ModpackConfig? CurrentModpack { get; set; }

	public static ConcurrentDictionary<string, ReleaseInfo> ReleaseInfos { get; } = new();

	public static ConcurrentDictionary<string, ModpackConfig> ModpackConfigs { get; } = new();

	public static ConcurrentDictionary<string, Account> Accounts { get; } = new();

	public static SceneTree SceneTree { get; } = (SceneTree)Engine.GetMainLoop();

	public static event Action? GameExitEvent;
	public Main() { Instance = this; }

	public override void _Ready() {
		if (!Start.IsRunning) {
			QueueFree();
			return;
		}

		base._Ready();

		_loginWindowScene.NotNull();
		_installedGamesImportScene.NotNull();
		_accountSelectItemScene.NotNull();
		_confirmationWindowScene.NotNull();
		_marginContainer.NotNull();
		_subViewport.NotNull();
		_minButton.NotNull();
		_closeButton.NotNull();
		_accountButton.NotNull();
		_accountSelectButton.NotNull();
		_accountSelectContainer.NotNull();
		_accountSelectScrollContainer.NotNull();
		_accountSelectAddButton.NotNull();
		_accountSelectListContainer.NotNull();
		_accountSelectAnimationPlayer.NotNull();

		SceneTree.Root.SizeChanged += RootOnSizeChanged;

		Tween? shadowTween = null;
		var shadowColor = Colors.Black;
		var shadowCallable = Callable.From((Color color) => {
			shadowColor = color;
			WindowMaterial!.SetShaderParameter(StringNames.ShadowColor, color);
		});
		SceneTree.Root.FocusEntered += () => {
			shadowTween?.Stop();
			shadowTween?.Dispose();
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
			shadowTween?.Dispose();
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
				handler.Proxy = string.IsNullOrWhiteSpace(BaseConfig.ProxyAddress) ? HttpClient.DefaultProxy : new WebProxy(BaseConfig.ProxyAddress);
			});
		});
	}

	private async void AccountSelectButtonOnPressed() {
		_accountSelectAnimationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_accountSelectAnimationPlayer, AnimationMixer.SignalName.AnimationFinished);
		_accountButton!.ButtonPressed = false;
		_accountSelectButton!.Hide();
		foreach (var child in _accountSelectListContainer!.GetChildren()) {
			child.Free();
		}

		CheckAccount();
	}

	private async void AccountSelectAddButtonOnPressed() {
		var loginWindow = await OpenAccountSelectWindow();
		loginWindow.Login += _ => {
			foreach (var child in _accountSelectListContainer!.GetChildren()) {
				child.Free();
			}

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

			var maxHeight = 0f;
			for (var index = 0; index < BaseConfig.Account.Count; index++) {
				var account = BaseConfig.Account[index];
				var accountItem = _accountSelectItemScene!.Instantiate<AccountSelectItem>();
				accountItem.Account = account;
				accountItem.Select += AccountItemOnSelect;
				accountItem.Edit += AccountItemOnEdit;
				accountItem.Remove += AccountItemOnRemove;
				_accountSelectListContainer!.AddChild(accountItem);
				if (index == 4) {
					maxHeight = _accountSelectContainer!.GetCombinedMinimumSize().Y;
				}
			}

			_accountSelectContainer.Size = _accountSelectContainer!.GetCombinedMinimumSize();
			_accountSelectScrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;

			if (maxHeight > 0 && _accountSelectContainer!.Size.Y > maxHeight) {
				_accountSelectScrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
				_accountSelectContainer!.Size = _accountSelectContainer!.Size with { Y = maxHeight };
			}

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
		confirmationWindow.Message =
			string.Format(Tr("确定要删除账号 [color=#0078d7][b]{0}[/b][/color] 吗？"), item.Account!.PlayerName);
		confirmationWindow.Confirm += async () => {
			await confirmationWindow.Hide();
			BaseConfig.Account.Remove(item.Account);

			if (BaseConfig.Account.Count > 0) {
				foreach (var child in _accountSelectListContainer!.GetChildren()) {
					child.Free();
				}

				AccountButtonOnPressed();
				CheckAccount();
			} else {
				AccountSelectButtonOnPressed();
			}
		};
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		AddChild(confirmationWindow);
		_ = confirmationWindow.Show();
	}

	private async void AccountItemOnEdit(AccountSelectItem item) {
		var editCurrent = item.Account?.PlayerName == BaseConfig.CurrentAccount;
		GD.Print("editCurrent: " + editCurrent);
		var loginWindow = await OpenAccountSelectWindow(item.Account);
		loginWindow.Login += _ => {
			foreach (var child in _accountSelectListContainer!.GetChildren()) {
				child.Free();
			}

			if (editCurrent) {
				BaseConfig.CurrentAccount = item.Account!.PlayerName!;
				CheckAccount();
			}

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
		var list = BaseConfig.Account.Distinct().ToList();
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

	public async Task<bool> ValidateSessionKeyWithServer(Account account) {
		if (account.Offline) {
			return true;
		}

		var response = await "https://auth3.vintagestory.at/clientvalidate".PostUrlEncodedAsync(new {
			uid = account.Uid,
			sessionkey = account.SessionKey,
		});

		if (!response.ResponseMessage.IsSuccessStatusCode) {
			return false;
		}

		var token = await response.GetStringAsync();
		GD.Print(token);
		var validateResponse = JsonSerializer.Deserialize(token, SourceGenerationContext.Default.ValidateResponse);
		return validateResponse.Valid == 1;
	}

	public async void Init() {
		await Task.Run(() => {
			CheckReleaseInfo();
			CheckModpackConfig();
		});
		CheckAccount();

		if (BaseConfig.Release.Count != 0 || InstalledGamesImport.InstalledGamePaths.Length <= 0) {
			return;
		}

		_ = InstantiateInstalledGamesImport();
		await _installedGamesImport!.ShowInstalledGames();
	}

	public void RootOnSizeChanged() {
		WindowMaterial!.SetShaderParameter(StringNames.WindowExpandedSize, SceneTree.Root.Size);
		if (SceneTree.Root.Mode != Godot.Window.ModeEnum.Maximized) {
			ShadowSize = 5;
			WindowMaterial!.SetShaderParameter(StringNames.HasRoundCorners, true);
		} else {
			ShadowSize = 0;
			WindowMaterial!.SetShaderParameter(StringNames.HasRoundCorners, false);
		}

		_subViewport!.Size2DOverride = new(Mathf.CeilToInt((SceneTree.Root.Size.X - ShadowSize * 2) / BaseConfig.DisplayScale),
			Mathf.CeilToInt((SceneTree.Root.Size.Y - ShadowSize * 2) / BaseConfig.DisplayScale));
	}

	public InstalledGamesImport InstantiateInstalledGamesImport() {
		if (_installedGamesImport is not null) {
			return _installedGamesImport;
		}

		_installedGamesImport = _installedGamesImportScene!.Instantiate<InstalledGamesImport>();
		AddChild(_installedGamesImport);
		_installedGamesImport.Import += paths => {
			if (paths.Length == 0) {
				return;
			}

			BaseConfig.Release.AddRange(paths);
			CheckReleaseInfo();
		};

		return _installedGamesImport;
	}

	public static void CheckReleaseInfo() {
		BaseConfig.Release = BaseConfig.Release.Select(p => p.NormalizePath()).Distinct().ToList();
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
		ReleaseInfos.Remove(path, out _);
		BaseConfig.Release.Remove(path);
	}

	public static void CheckModpackConfig() {
		BaseConfig.Modpack = BaseConfig.Modpack.Select(p => p.NormalizePath()).Distinct().ToList();
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
		ModpackConfigs.Remove(path, out _);
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
		var vsRunPath = $"res://Misc/VSRun/net{CurrentModpack!.ReleaseInfo!.TargetFrameworkVersion}";
		foreach (var file in DirAccess.GetFilesAt(vsRunPath)) {
			var fromPath = vsRunPath.PathJoin(file);
			tmp.Copy(fromPath, tmpRunPath.PathJoin(file).NormalizePath());
		}

		return tmp;
	}

	public static Process? VsRun(RunConfig runConfig, string command) {
		GD.PrintS("Execute:", command);
		GD.Print(runConfig);

		try {
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
					["VINTAGE_STORY_PATH"] = runConfig.VintageStoryPath,
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
		} catch (Exception e) {
			GD.PrintErr(e.Message);
			return null;
		}
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
			if (process != null) {
				await process.WaitForExitAsync();
			}
		} catch (Exception e) {
			GD.PrintErr(e);
		}
	}

	public async Task StartGame(ModpackConfig modpackConfig) {
		CurrentModpack = modpackConfig;
		var releaseInfo = modpackConfig.ReleaseInfo;
		if (releaseInfo is null) {
			GameExitEvent?.Invoke();
			CurrentModpack = null;
			GD.PrintErr("ReleaseInfo is null");
			return;
		}

		var hasDotnet =
			await Tools.HasRequiredDotNetVersionInstalled(releaseInfo.TargetFrameworkName!,
				releaseInfo.TargetFrameworkVersion!);
		if (!hasDotnet) {
			var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
			confirmationWindow.Modulate = Colors.Transparent;
			confirmationWindow.Message =
				string.Format(
					Tr(
						"""
						[b]{0}[/b] 需要安装 [b].NET {1} 运行时[/b]
						请点击链接下载并安装：
						[color=#3c7fe1][url]https://dotnet.microsoft.com/download/dotnet/{2}[/url][/color]
						是否尝试强制启动游戏？
						"""),
					releaseInfo.Version.ShortGameVersion,
					releaseInfo.TargetFrameworkVersion,
					releaseInfo.TargetFrameworkVersion);
			confirmationWindow.Hidden += confirmationWindow.QueueFree;
			confirmationWindow.Confirm += async () => {
				await confirmationWindow.Hide();
				StartGame(releaseInfo.Path, modpackConfig.Path!);
			};
			confirmationWindow.Cancel += () => {
				GameExitEvent?.Invoke();
				CurrentModpack = null;
			};
			AddChild(confirmationWindow);
			await confirmationWindow.Show();
			return;
		}

		if (!string.IsNullOrEmpty(BaseConfig.CurrentAccount)) {
			var validate = await ValidateSessionKeyWithServer(Accounts[BaseConfig.CurrentAccount]);
			if (!validate) {
				var confirmationWindow = _confirmationWindowScene!.Instantiate<ConfirmationWindow>();
				confirmationWindow.Modulate = Colors.Transparent;
				confirmationWindow.Message =
					string.Format(
						Tr(
							"""
							[b]{0}[/b] 的账号信息已失效，需要重新登录
							是否尝试强制启动游戏？
							"""),
						BaseConfig.CurrentAccount);
				confirmationWindow.Hidden += confirmationWindow.QueueFree;
				confirmationWindow.Confirm += async () => {
					await confirmationWindow.Hide();
					StartGame(releaseInfo.Path, modpackConfig.Path!);
				};
				confirmationWindow.Cancel += () => {
					GameExitEvent?.Invoke();
					CurrentModpack = null;
				};
				AddChild(confirmationWindow);
				await confirmationWindow.Show();
				return;
			}
		}

		StartGame(releaseInfo.Path, modpackConfig.Path!, modpackConfig.Command, modpackConfig.GameAssembly);
	}

	public static void StartGame(
		string gamePath,
		string dataPath,
		string command = "%command%",
		string assembleName = "Vintagestory.dll") {
		try {
			var tmp = CopyVsRun();
			command = command.Replace("%game_path%", gamePath)
				.Replace("%tmp_path%", tmp.GetCurrentDir())
				.Replace("%data_path%", dataPath)
				.Replace("%command%",
					$"dotnet \"{Path.Combine(tmp.GetCurrentDir(), "VSRun.dll").NormalizePath()}\"");
			var process = VsRun(new() {
					VintageStoryPath = gamePath,
					VintageStoryDataPath = dataPath,
					AssemblyPath = assembleName.Replace("%game_path%", gamePath).Replace("%data_path", dataPath),
					ExecutableType = ExecutableTypeEnum.StartGame,
					Account = string.IsNullOrEmpty(BaseConfig.CurrentAccount) ? null : Accounts[BaseConfig.CurrentAccount]
				},
				command);
			if (process is null) {
				return;
			}

			CurrentGameProcess = process;
			process.Exited += (_, _) => {
				GameExitEvent?.Invoke();
				tmp.Dispose();
				process.Dispose();
				CurrentGameProcess = null;
				CurrentModpack = null;
			};
		} catch (Exception e) {
			GameExitEvent?.Invoke();
			GD.PrintErr(e);
		}
	}
}
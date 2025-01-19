using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils;
using MVL.Utils.Game;

namespace MVL.UI.Window;

public partial class InstalledGamesImport : Control {
	[Export]
	private PackedScene? _installedGameItemScene;

	[Export]
	private PanelContainer? _installedGameItemContainer;

	[Export]
	private Control? _installedGameList;

	[Export]
	private Button? _cancelButton;

	[Export]
	private Button? _importButton;

	[Export]
	private AnimationPlayer? _animationPlayer;

	[Signal]
	public delegate void CancelEventHandler();

	[Signal]
	public delegate void ImportEventHandler(string[] gamePaths);

	public bool SingleSelect { get; set; }

	public override void _Ready() {
		Utils.Help.NullExceptionHelper.NotNull(_installedGameItemScene,
			_installedGameItemContainer,
			_installedGameList,
			_cancelButton,
			_importButton,
			_animationPlayer);
		_cancelButton.Pressed += CancelButtonOnPressed;
		_importButton.Pressed += ImportButtonOnPressed;
		base.Hide();
	}

	private async void ImportButtonOnPressed() {
		try {
			await Hide();
			List<string> gamePaths = [];
			gamePaths.AddRange(from InstalledGameItem? installedGameItem in _installedGameList!.GetChildren()
							   where installedGameItem.Check
							   select installedGameItem.GamePath);
			EmitSignalImport(gamePaths.ToArray());
		} catch (Exception e) {
			GD.PrintErr(e.ToString());
		}
	}

	private async void CancelButtonOnPressed() {
		try {
			await Hide();
			EmitSignalCancel();
		} catch (Exception e) {
			GD.PrintErr(e.ToString());
		}
	}

	public new async Task Show() {
		Modulate = Colors.Transparent;
		_installedGameItemContainer!.Scale = Vector2.Zero;
		base.Show();
		_animationPlayer!.Play(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
	}

	public new async Task Hide() {
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		foreach (var child in _installedGameList!.GetChildren()) {
			child.QueueFree();
		}

		base.Hide();
	}

	public async Task ShowInstalledGames() { await ShowInstalledGames(InstalledGamePaths); }

	public async Task ShowInstalledGames(IEnumerable<string> gamePaths) {
		var list = new List<ReleaseInfo>();
		foreach (var installedGamePath in gamePaths) {
			var gameVersion = GameVersion.FromGamePath(installedGamePath);
			if (gameVersion is null) {
				continue;
			}

			list.Add(new() {
				Path = installedGamePath,
				Version = gameVersion.Value,
				Name = installedGamePath.GetFile()
			});
		}

		if (list.Count <= 0) return;
		await ShowInstalledGames(list);
	}

	public async Task ShowInstalledGames(IEnumerable<ReleaseInfo> games) {
		if (!Visible) {
			await Show();
		}

		foreach (var installedGame in games) {
			var installedGameItem = _installedGameItemScene!.Instantiate<InstalledGameItem>();
			installedGameItem.GameVersion = installedGame.Version;
			installedGameItem.GamePath = installedGame.Path;
			installedGameItem.Check = !Main.ReleaseInfos.ContainsKey(installedGame.Path) && !SingleSelect;
			installedGameItem.SingleSelect = SingleSelect;
			installedGameItem.Modulate = Colors.Transparent;
			_installedGameList!.AddChild(installedGameItem);
			using var tween = installedGameItem.CreateTween();
			tween.TweenProperty(installedGameItem, "modulate:a", 1f, 0.3f);
			tween.Parallel().TweenProperty(installedGameItem, "scale:x", 1f, 0.3f).From(0f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
	}

	[field: AllowNull, MaybeNull]
	public static string[] InstalledGamePaths {
		get {
			if (field is not null) {
				return field;
			}

			List<string> paths = [
				AppDomain.CurrentDomain.BaseDirectory,
				OS.GetDataDir(),
#if GODOT_WINDOWS
				System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
				System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
#else
				"/opt",
				"/usr/share",
#endif
				OS.GetSystemDir(OS.SystemDir.Desktop),
				OS.GetSystemDir(OS.SystemDir.Downloads),
			];
			field = paths.Select(path => {
#if GODOT_WINDOWS
				path = path.PathJoin("Vintagestory").SimplifyPath();
#else
				path = path.PathJoin("vintagestory").SimplifyPath();
#endif
				return File.Exists(path.PathJoin("Vintagestory.dll")) ? path : string.Empty;
			}).Where(path => !string.IsNullOrEmpty(path)).ToArray();
			return field;
		}
	}
}
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.UI.Item;
using MVL.Utils.Extensions;
using MVL.Utils.Game;

namespace MVL.UI.Window;

public partial class InstalledGamesImport : BaseWindow {

	[Export]
	private PackedScene? _installedGameItemScene;

	[Export]
	private Control? _installedGameList;

	[Signal]
	public delegate void ImportEventHandler(string[] gamePaths);

	public bool SingleSelect { get; set; }

	public override void _Ready() {
		base._Ready();
		Utils.Help.NullExceptionHelper.NotNull(_installedGameItemScene,
			_installedGameList,
			CancelButton,
			OkButton);
		CancelButton.Pressed += CancelButtonOnPressed;
		OkButton.Pressed += ImportButtonOnPressed;
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

	public override async Task Hide() {
		await base.Hide();
		foreach (var child in _installedGameList!.GetChildren()) {
			child.QueueFree();
		}
	}

	public async Task ShowInstalledGames() { await ShowInstalledGames(InstalledGamePaths); }

	public async Task ShowInstalledGames(IEnumerable<string> gamePaths) {
		var list = new List<ReleaseInfo>();
		foreach (var installedGamePath in gamePaths) {
			if (!GameVersion.TryFromGamePath(installedGamePath, out var gameVersion)) continue;
			list.Add(new() {
				Path = installedGamePath,
				Version = gameVersion,
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

		var i = 1;
		foreach (var installedGame in games) {
			var installedGameItem = _installedGameItemScene!.Instantiate<InstalledGameItem>();
			installedGameItem.GameVersion = installedGame.Version;
			installedGameItem.GamePath = installedGame.Path;
			installedGameItem.Check = !Main.ReleaseInfos.ContainsKey(installedGame.Path) && !SingleSelect;
			installedGameItem.SingleSelect = SingleSelect;
			installedGameItem.Modulate = Colors.Transparent;
			_installedGameList!.AddChild(installedGameItem);
			var tween = installedGameItem.CreateTween();
			tween.TweenProperty(installedGameItem, "modulate:a", 1f, 0.2f).SetDelay(i * 0.1);
			tween.Parallel().TweenProperty(installedGameItem, "scale:x", 1f, 0.2f).From(0f).SetDelay(i * 0.1);
			i++;
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
				path = Path.Combine(path, "Vintagestory").NormalizePath();
#else
				path = Path.Combine(path, "vintagestory").NormalizePath();
#endif
				return File.Exists(Path.Combine(path, "Vintagestory.dll")) ? path : string.Empty;
			}).Where(path => !string.IsNullOrEmpty(path)).ToArray();
			return field;
		}
	}
}
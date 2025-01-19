using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class AddModpackWindow : ColorRect {
	[Export]
	private PanelContainer? _container;

	[Export]
	private LineEdit? _modpackPath;

	[Export]
	private Button? _folderButton;

	[Export]
	private FileDialog? _fileDialog;

	[Export]
	private OptionButton? _gameVersion;

	[Export]
	private Button? _cancelButton;

	[Export]
	private Button? _okButton;

	[Export]
	private AnimationPlayer? _animationPlayer;

	[Signal]
	public delegate void CancelEventHandler();

	[Signal]
	public delegate void AddModpackEventHandler(string modpackPath, string gameVersion);

	public override void _Ready() {
		NullExceptionHelper.NotNull(_modpackPath,
			_folderButton,
			_fileDialog,
			_gameVersion,
			_cancelButton,
			_okButton,
			_animationPlayer);
		Main.CheckReleaseInfo();

		var list = Main.ReleaseInfos.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);
		foreach (var releaseInfo in list) {
			_gameVersion.AddItem(releaseInfo.Version.ShortGameVersion);
		}

		var pop = _gameVersion.GetPopup();
		pop.AboutToPopup += () => { pop.Size = pop.Size with { X = (int)_gameVersion.Size.X }; };
		_modpackPath.TextChanged += text => { _okButton.Disabled = !DirAccess.DirExistsAbsolute(text); };
		_folderButton.Pressed += _fileDialog.Show;
		_fileDialog.DirSelected += path => {
			_modpackPath.Text = path;
			_okButton.Disabled = false;
		};
		_cancelButton.Pressed += CancelButtonOnPressed;
		_okButton.Pressed += OkButtonOnPressed;
	}

	private async void OkButtonOnPressed() {
		await Hide();
		EmitSignalAddModpack(Path.TrimEndingDirectorySeparator(_modpackPath!.Text), _gameVersion!.GetItemText(_gameVersion.Selected));
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
		_container!.Scale = Vector2.Zero;
		base.Show();
		_animationPlayer!.Play(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
	}

	public new async Task Hide() {
		_animationPlayer!.PlayBackwards(StringNames.Show);
		await ToSignal(_animationPlayer, AnimationMixer.SignalName.AnimationFinished);
		base.Hide();
	}
}
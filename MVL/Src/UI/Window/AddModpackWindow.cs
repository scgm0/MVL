using System;
using System.Collections.Generic;
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
	private LineEdit? _modpackName;

	[Export]
	private LineEdit? _modpackPath;

	[Export]
	private CheckButton? _createPath;

	[Export]
	private Button? _folderButton;

	[Export]
	private FileDialog? _fileDialog;

	[Export]
	private OptionButton? _gameVersion;

	[Export]
	private OptionButton? _releasePath;

	[Export]
	private Label? _tooltip;

	[Export]
	private Button? _cancelButton;

	[Export]
	private Button? _okButton;

	[Export]
	private AnimationPlayer? _animationPlayer;

	[Signal]
	public delegate void CancelEventHandler();

	[Signal]
	public delegate void AddModpackEventHandler(string modpackName, string modpackPath, string gameVersion, string releasePath);

	private Dictionary<string, List<string>> _dictionary = [];

	public override void _Ready() {
		NullExceptionHelper.NotNull(
			_modpackName,
			_modpackPath,
			_createPath,
			_folderButton,
			_fileDialog,
			_gameVersion,
			_releasePath,
			_tooltip,
			_cancelButton,
			_okButton,
			_animationPlayer);
		Main.CheckReleaseInfo();

		_gameVersion.ItemSelected += OnGameVersionOnItemSelected;

		var list = Main.ReleaseInfos.Values.OrderByDescending(info => info.Version, GameVersion.Comparer);
		_dictionary = [];
		foreach (var releaseInfo in list) {
			var version = releaseInfo.Version.ShortGameVersion;
			var path = releaseInfo.Path;
			if (!_dictionary.TryGetValue(version, out var paths)) {
				paths = [];
				_dictionary[version] = paths;
			}

			paths.Add(path);
		}

		foreach (var version in _dictionary.Keys) {
			_gameVersion!.AddItem(version);
		}

		var name = _modpackName.Text;
		_modpackName.TextChanged += text => {
			if (string.IsNullOrEmpty(text)) {
				_okButton!.Disabled = true;
				_tooltip!.Text = "请输入名称";
				return;
			}

			if (_createPath!.ButtonPressed) {
				var path = _modpackPath!.Text;
				path = path.GetFile() == name ? Path.Combine(path.GetBaseDir(), text) : path;
				SetModpackPath(path);
			}

			name = text;
		};

		_modpackPath.TextChanged += OnModpackPathOnTextChanged;
		_createPath.Toggled += CreatePathOnToggled;
		_folderButton.Pressed += _fileDialog.Show;
		_fileDialog.DirSelected += path => {
			path = Path.TrimEndingDirectorySeparator(path);
			if (_createPath!.ButtonPressed) {
				path = Path.Combine(path, _modpackName!.Text);
			}

			SetModpackPath(path);
		};
		_cancelButton.Pressed += CancelButtonOnPressed;
		_okButton.Pressed += OkButtonOnPressed;

		OnGameVersionOnItemSelected(0);
		SetModpackPath(Path.Combine(Paths.ModpackPath, _modpackName.Text));
	}

	private void OnGameVersionOnItemSelected(long index) {
		_releasePath!.Clear();
		var paths = _dictionary[_gameVersion!.GetItemText((int)index)];
		foreach (var path in paths) {
			_releasePath.AddItem(path);
		}

		_releasePath.Select(0);
	}

	private void SetModpackPath(string path) {
		_modpackPath!.Text = path;
		OnModpackPathOnTextChanged(path);
	}

	private void CreatePathOnToggled(bool toggledOn) {
		var name = _modpackName!.Text;
		var path = _modpackPath!.Text;
		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) {
			return;
		}

		path = toggledOn switch {
			true when path.GetFile() != name => Path.Combine(path, name),
			false when path.GetFile() == name => path.GetBaseDir(),
			_ => path
		};

		SetModpackPath(path);
	}

	private void OnModpackPathOnTextChanged(string text) {
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_modpackName!.Text)) {
			_okButton!.Disabled = true;
			return;
		}

		_tooltip!.Text = "将自动创建文件夹";
		if (Directory.Exists(text)) {
			_tooltip!.Text = Directory.GetFileSystemEntries(text).Length > 0 ? "文件夹不为空，确定选择它吗？" : "文件夹存在且为空";
		}

		switch (_createPath!.ButtonPressed) {
			case true: {
				_okButton!.Disabled = !DirAccess.DirExistsAbsolute(text.GetBaseDir());
				if (_okButton!.Disabled) {
					_tooltip!.Text = "父文件夹不存在";
				}

				break;
			}
			case false: {
				_okButton!.Disabled = !DirAccess.DirExistsAbsolute(text);
				if (_okButton!.Disabled) {
					_tooltip!.Text = "文件夹不存在";
				}

				break;
			}
		}
	}

	private async void OkButtonOnPressed() {
		await Hide();
		if (_createPath!.ButtonPressed) {
			DirAccess.MakeDirAbsolute(_modpackPath!.Text);
		}

		EmitSignalAddModpack(_modpackName!.Text,
			Path.TrimEndingDirectorySeparator(_modpackPath!.Text),
			_gameVersion!.GetItemText(_gameVersion.Selected),
			_releasePath!.GetItemText(_releasePath.Selected));
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
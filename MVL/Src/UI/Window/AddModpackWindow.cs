using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class AddModpackWindow : BaseWindow {

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

	[Signal]
	public delegate void AddModpackEventHandler(string modpackName, string modpackPath, string gameVersion, string releasePath);

	private Dictionary<string, List<string>> _dictionary = [];

	public override void _Ready() {
		base._Ready();
		NullExceptionHelper.NotNull(
			_modpackName,
			_modpackPath,
			_createPath,
			_folderButton,
			_fileDialog,
			_gameVersion,
			_releasePath,
			_tooltip,
			CancelButton,
			OkButton);
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
				OkButton!.Disabled = true;
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
		_fileDialog.CurrentPath = Paths.ModpackFolder;
		_fileDialog.CurrentDir = Paths.ModpackFolder;
		_fileDialog.DirSelected += OnFileDialogOnDirSelected;
		CancelButton.Pressed += CancelButtonOnPressed;
		OkButton.Pressed += OkButtonOnPressed;

		if (_gameVersion.ItemCount > 0) {
			OnGameVersionOnItemSelected(0);
		} else {
			_gameVersion.Select(-1);
		}

		SetModpackPath(Path.Combine(Paths.ModpackFolder, _modpackName.Text));
	}

	private void OnFileDialogOnDirSelected(string path) {
		if (_createPath!.ButtonPressed) {
			path = Path.Combine(path, _modpackName!.Text);
		}

		SetModpackPath(path.NormalizePath());
	}

	private void OnGameVersionOnItemSelected(long index) {
		_releasePath!.Clear();
		var s = _gameVersion!.GetItemText((int)index);
		if (_dictionary.TryGetValue(s, out var paths)) {
			foreach (var path in paths) {
				_releasePath.AddItem(path);
			}
		}

		if (_releasePath.ItemCount > 0) {
			_releasePath.Select(0);
		} else {
			_releasePath.Select(-1);
		}
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
			OkButton!.Disabled = true;
			return;
		}

		_tooltip!.Text = "将自动创建文件夹";
		if (Directory.Exists(text)) {
			_tooltip!.Text = Directory.GetFileSystemEntries(text).Length > 0 ? "文件夹不为空，确定选择它吗？" : "文件夹存在且为空";
		}

		switch (_createPath!.ButtonPressed) {
			case true: {
				OkButton!.Disabled = !DirAccess.DirExistsAbsolute(text.GetBaseDir());
				if (OkButton!.Disabled) {
					_tooltip!.Text = "父文件夹不存在";
				}

				break;
			}
			case false: {
				OkButton!.Disabled = !DirAccess.DirExistsAbsolute(text);
				if (OkButton!.Disabled) {
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
}
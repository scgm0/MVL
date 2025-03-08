using System;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl.Http;
using Godot;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;
using SharedLibrary;

namespace MVL.UI.Window;

public partial class LoginWindow : BaseWindow {
	private string _preLoginToken = string.Empty;

	[Export]
	private LineEdit? _emailInput;

	[Export]
	private VBoxContainer? _loginContainer;

	[Export]
	private LineEdit? _passwordInput;

	[Export]
	private Button? _eyeButton;

	[Export]
	private VBoxContainer? _accessCodeContainer;

	[Export]
	private LineEdit? _accessCodeInput;

	[Export]
	private VBoxContainer? _nameContainer;

	[Export]
	private LineEdit? _nameInput;

	[Export]
	private Label? _tooltip;

	[Export]
	private CheckButton? _offlineCheckbox;

	[Export]
	private ColorRect? _loadingControl;

	[Signal]
	public delegate void LoginEventHandler(LoginWindow window);

	public Account? Account { get; set; }

	public override void _Ready() {
		base._Ready();
		_emailInput.NotNull();
		_passwordInput.NotNull();
		_eyeButton.NotNull();
		_accessCodeContainer.NotNull();
		_accessCodeInput.NotNull();
		_nameContainer.NotNull();
		_nameInput.NotNull();
		_tooltip.NotNull();
		_offlineCheckbox.NotNull();
		_loadingControl.NotNull();

		Account ??= new();
		_emailInput.Text = Account.Email;
		_nameInput.Text = Account.PlayerName;
		_offlineCheckbox.ButtonPressed = Account.Offline;

		_emailInput.TextChanged += _ => UpdateInfo();
		_passwordInput.TextChanged += _ => UpdateInfo();
		_accessCodeInput.TextChanged += _ => UpdateInfo();
		_eyeButton.Toggled += EyeButtonOnToggled;
		_nameInput.TextChanged += _ => UpdateInfo();
		_offlineCheckbox.Toggled += OfflineCheckboxOnToggled;
		OkButton!.Pressed += OkButtonOnPressed;
		CancelButton!.Pressed += CancelButtonOnPressed;
		OfflineCheckboxOnToggled(_offlineCheckbox.ButtonPressed);
	}

	private void OfflineCheckboxOnToggled(bool toggledOn) {
		Account!.Offline = toggledOn;
		_nameContainer!.Visible = toggledOn;
		_loginContainer!.Modulate = toggledOn ? Colors.Transparent : Colors.White;
		UpdateInfo();
	}

	public void UpdateInfo() {
		UpdateInfo(
			text: string.Empty,
			color: Colors.White);
	}

	public void UpdateInfo(string? text, Color? color) {
		if (_offlineCheckbox!.ButtonPressed) {
			if (string.IsNullOrEmpty(_nameInput!.Text)) {
				_tooltip!.Text = "请输入名称";
				_tooltip.Modulate = Colors.Red;
				OkButton!.Disabled = true;
				return;
			}

			if (Main.Accounts.TryGetValue(_nameInput!.Text, out var value) && value != Account) {
				_tooltip!.Text = "该名称已存在";
				_tooltip.Modulate = Colors.Red;
				OkButton!.Disabled = true;
				return;
			}
		} else {
			if (string.IsNullOrEmpty(_emailInput!.Text)) {
				_tooltip!.Text = "请输入邮箱";
				_tooltip.Modulate = Colors.Red;
				OkButton!.Disabled = true;
				return;
			}

			if (string.IsNullOrEmpty(_passwordInput!.Text)) {
				_tooltip!.Text = "请输入密码";
				_tooltip.Modulate = Colors.Red;
				OkButton!.Disabled = true;
				return;
			}

			if (_accessCodeContainer!.Visible && string.IsNullOrEmpty(_accessCodeInput!.Text)) {
				_tooltip!.Text = "请输入访问代码";
				_tooltip.Modulate = Colors.Red;
				OkButton!.Disabled = true;
				return;
			}
		}

		_tooltip!.Text = text ?? string.Empty;
		_tooltip.Modulate = color ?? Colors.White;
		OkButton!.Disabled = false;
	}

	private async void OkButtonOnPressed() {
		if (_offlineCheckbox!.ButtonPressed) {
			Account!.PlayerName = _nameInput!.Text;
			Account.Email = _emailInput!.Text;
			Account.Uid ??= _nameInput!.Text;
			Account.SessionKey ??= string.Empty;
			Account.SessionSignature ??= string.Empty;
			Account.Entitlements ??= string.Empty;
			Account!.Offline = true;
			await Hide();
			EmitSignalLogin(this);
			return;
		}

		_tooltip!.Text = "正在登录...";
		OkButton!.Disabled = true;
		CancelButton!.Disabled = true;
		_loadingControl!.Show();
		_loginContainer!.Modulate = Colors.Transparent;
		_accessCodeContainer!.Modulate = Colors.Transparent;
		_offlineCheckbox!.Hide();

		var loginResponse = await DoLogin(email: _emailInput!.Text,
			password: _passwordInput!.Text,
			accessCode: _accessCodeInput!.Text,
			preLoginToken: _preLoginToken);

		CancelButton.Disabled = false;
		_loadingControl.Hide();
		_accessCodeContainer.Hide();
		_offlineCheckbox!.Show();
		_loginContainer!.Modulate = Colors.White;
		_accessCodeContainer.Modulate = Colors.White;

		if (loginResponse is null) {
			UpdateInfo("请求失败", Colors.Red);
			return;
		}

		if (loginResponse.Valid == 1) {
			_tooltip!.Text = "登录成功";
			await Hide();
			Account!.Email = _emailInput!.Text;
			Account.PlayerName = loginResponse.PlayerName ?? string.Empty;
			Account.Uid = loginResponse.Uid ?? string.Empty;
			Account.SessionKey = loginResponse.SessionKey ?? string.Empty;
			Account.SessionSignature = loginResponse.SessionSignature ?? string.Empty;
			Account.Entitlements = loginResponse.Entitlements ?? string.Empty;
			Account.HasGameServer = loginResponse.HasGameServer;
			Account!.Offline = false;
			EmitSignalLogin(this);
		} else if (loginResponse.Reason is "requiretotpcode" or "wrongtotpcode") {
			_loginContainer.Modulate = Colors.Transparent;
			_accessCodeContainer!.Show();
			_offlineCheckbox!.Hide();
			if (loginResponse.PreLoginToken != null) {
				_preLoginToken = loginResponse.PreLoginToken;
			}

			if (loginResponse.Reason == "wrongtotpcode") {
				UpdateInfo("访问代码错误", Colors.Yellow);
				return;
			}
		} else {
			UpdateInfo($"登录失败: {loginResponse.Reason}", Colors.Yellow);
			return;
		}

		UpdateInfo();
	}

	private void EyeButtonOnToggled(bool toggledOn) {
		_passwordInput!.Secret = !toggledOn;
		var icon = (IconTexture2D)_eyeButton!.Icon;
		icon.IconName = toggledOn ? "eye-off-outline" : "eye-outline";
	}

	public async Task<LoginResponse?> DoLogin(
		string email,
		string password,
		string accessCode = "",
		string? preLoginToken = "",
		string uri = "https://auth3.vintagestory.at/v2/gamelogin") {
		return await Task.Run(async () => {
			var response = await uri.PostUrlEncodedAsync(new {
				email,
				password,
				totpcode = accessCode,
				prelogintoken = preLoginToken
			});
			if (!response.ResponseMessage.IsSuccessStatusCode) {
				return null;
			}

			var token = await response.GetStringAsync();
			GD.Print(token);
			var loginResponse = JsonSerializer.Deserialize(token, SourceGenerationContext.Default.LoginResponse);
			return loginResponse;
		});
	}
}
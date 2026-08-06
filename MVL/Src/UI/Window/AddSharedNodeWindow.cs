using Godot;
using System;
using MVL.Utils.Help;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Window;

public partial class AddSharedNodeWindow : BaseWindow {
	[Export]
	private LineEdit? _address;

	[Export]
	private LineEdit? _name;

	[Export]
	private LineEdit? _remark;

	[Export]
	private OptionButton? _codec;

	[Export]
	private Label? _tip;

	public event Action<SharedNode>? AddSharedNode;

	public override void _Ready() {
		base._Ready();
		_address.NotNull();
		_name.NotNull();
		_remark.NotNull();
		_codec.NotNull();
		_tip.NotNull();

		_address.TextChanged += AddressOnTextChanged;
		OkButton!.Pressed += OkButtonOnPressed;
		CancelButton!.Pressed += CancelButtonOnPressed;

		foreach (var codecType in Enum.GetNames<CodecType>()) {
			_codec.AddItem(codecType);
		}
	}

	private async void OkButtonOnPressed() {
		OkButton!.Disabled = true;
		var address = _address!.Text;
		var name = _name!.Text;
		var remark = _remark!.Text;
		var codec = (CodecType)_codec!.Selected;
		var sharedNode = new SharedNode {
			Address = string.Empty,
			Name = string.IsNullOrEmpty(name) ? null : name,
			Remark = string.IsNullOrEmpty(remark) ? null : remark,
			Codec = codec == CodecType.None ? null : codec
		};
		sharedNode.UpdateAddress(address);
		await Hide();
		AddSharedNode?.Invoke(sharedNode);
	}

	private void AddressOnTextChanged(string newText) {
		if (string.IsNullOrEmpty(newText)) {
			_tip?.Text = "节点地址不可为空";
			OkButton?.Disabled = true;
			return;
		}

		if (!Uri.TryCreate(newText, UriKind.Absolute, out var uri) || uri.HostNameType is UriHostNameType.Unknown ||
			string.IsNullOrEmpty(uri.Host)) {
			_tip?.Text = "节点地址格式不正确";
			OkButton?.Disabled = true;
			return;
		}

		foreach (var customPublicServer in Main.BaseConfig.CustomSharedNodes) {
			if (customPublicServer.GetDecodedAddress() != newText) {
				continue;
			}

			_tip?.Text = "节点地址已存在";
			OkButton?.Disabled = true;
			return;
		}

		_tip?.Text = "";
		OkButton?.Disabled = false;
	}
}
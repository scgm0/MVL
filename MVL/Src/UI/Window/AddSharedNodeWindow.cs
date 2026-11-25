using Godot;
using System;
using MVL.Utils;
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

	private readonly AsyncDebouncer _debouncer = new(TimeSpan.FromMilliseconds(150));

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
		var address = _address!.Text.Trim();
		var name = _name!.Text;
		var remark = _remark!.Text;
		var codec = (CodecType)_codec!.Selected;
		SharedNode.TryDetectCodec(address, out var detectedCodec, out _);
		if (detectedCodec is not CodecType.None) {
			codec = detectedCodec;
		}

		var sharedNode = new SharedNode {
			Address = address,
			Name = string.IsNullOrEmpty(name) ? LocalizedString.Empty : new LocalizedString(name),
			Remark = string.IsNullOrEmpty(remark) ? LocalizedString.Empty : new LocalizedString(remark),
			Codec = codec == CodecType.None ? null : codec
		};
		if (detectedCodec is CodecType.None) {
			sharedNode.UpdateAddress(address);
		}

		await Hide();
		AddSharedNode?.Invoke(sharedNode);
	}

	private void AddressOnTextChanged(string newText) {
		_ = _debouncer.DebounceAsync(() => {
			var address = newText.Trim();
			if (string.IsNullOrEmpty(address)) {
				UnlockCodec();
				_tip!.Text = "节点地址不可为空";
				OkButton!.Disabled = true;
				return;
			}

			if (!SharedNode.TryDetectCodec(address, out var codec, out var decodedAddress)) {
				UnlockCodec();
				_tip!.Text = "节点地址格式不正确";
				OkButton!.Disabled = true;
				return;
			}

			if (codec is CodecType.None) {
				UnlockCodec();
			} else {
				LockCodec(codec);
			}

			foreach (var customPublicServer in Main.BaseConfig.CustomSharedNodes) {
				if (!SharedNode.TryDecode(customPublicServer.Address, customPublicServer.Codec ?? CodecType.None,
					    out var existingAddress) || existingAddress != decodedAddress) {
					continue;
				}

				_tip!.Text = "节点地址已存在";
				OkButton!.Disabled = true;
				return;
			}

			_tip!.Text = codec is CodecType.None ? string.Empty : string.Format(Tr("已识别为 {0} 编码"), codec);
			OkButton!.Disabled = false;
		});
	}

	private void LockCodec(CodecType codec) {
		_codec!.Selected = (int)codec;
		_codec.Disabled = true;
	}

	private void UnlockCodec() {
		if (!_codec!.Disabled) {
			return;
		}

		_codec.Disabled = false;
		_codec.Selected = (int)CodecType.None;
	}
}
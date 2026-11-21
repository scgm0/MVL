using Godot;
using MVL.Utils.Help;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Item;

public partial class SharedNodeItem : PanelContainer {
	[Export]
	private Label? _name;

	[Export]
	private Label? _remark;

	[Export]
	private Button? _removeButton;

	[Export]
	public bool IsCustomized { get; set; }

	public SharedNode? SharedNode { get; set; }

	public override void _Ready() {
		_name.NotNull();
		_remark.NotNull();
		_removeButton.NotNull();
		SharedNode.NotNull();

		UpdateText();
		_removeButton.Visible = IsCustomized;
		_removeButton.Pressed += RemoveButtonOnPressed;
	}

	public override void _Notification(int what) {
		if (what != (int)NotificationTranslationChanged || !IsInstanceValid(this)) {
			return;
		}

		if (SharedNode is null || _name is null || _remark is null) {
			return;
		}

		UpdateText();
	}

	private void UpdateText() {
		var name = SharedNode!.Name.GetText();
		_name!.Text = string.IsNullOrEmpty(name)
			? SharedNode.Codec is CodecType.None or null ? SharedNode.Address : Tr("加密节点")
			: name;

		var rawName = SharedNode.Name.Value;
		_name.TooltipText = string.IsNullOrEmpty(rawName) ? _name.Text : rawName;

		var remark = SharedNode.Remark.GetText();
		_remark!.Text = string.IsNullOrEmpty(remark) ? Tr("无备注") : remark;

		var rawRemark = SharedNode.Remark.Value;
		_remark.TooltipText = string.IsNullOrEmpty(rawRemark) ? _remark.Text : rawRemark;
	}

	private void RemoveButtonOnPressed() {
		var confirmationWindow = Main.Instance!.OpenConfirmationWindow(string.Format(Tr("确定要删除节点 {0} 吗？"), _name!.Text));
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		confirmationWindow.Confirm += async () => {
			confirmationWindow.OkButton?.Disabled = true;
			await confirmationWindow.Hide();
			Main.BaseConfig.CustomSharedNodes.Remove(SharedNode!);
			await Main.BaseConfig.SaveAsync();
			QueueFree();
		};
		_ = confirmationWindow.Show();
	}
}

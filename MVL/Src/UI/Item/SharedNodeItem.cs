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

		if (string.IsNullOrEmpty(SharedNode.Name)) {
			_name.Text = SharedNode.Codec is CodecType.None or null ? SharedNode.Address : "加密节点";
		} else {
			_name.Text = SharedNode.Name;
		}

		_remark.Text = string.IsNullOrEmpty(SharedNode.Remark) ? "无备注" : SharedNode.Remark;
		_removeButton.Visible = IsCustomized;
		_removeButton.Pressed += RemoveButtonOnPressed;
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
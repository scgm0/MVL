using System;
using System.Text.Json;
using Flurl.Http;
using Godot;
using MVL.UI.Item;
using MVL.UI.Window;
using MVL.Utils;
using MVL.Utils.Help;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Panel;

public partial class SubscriptionPanel : FoldableContainer {
	[Export]
	private PackedScene? _sharedNodeItemScene;

	[Export]
	private PackedScene? _addSharedNodeWindowScene;

	[Export]
	private IconTexture2D? _warningIcon;

	[Export]
	private IconTexture2D? _removeIcon;

	[Export]
	private IconTexture2D? _addIcon;

	[Export]
	private IconTexture2D? _reloadIcon;

	[Export]
	private VBoxContainer? NodeList { get; set; }

	[Export]
	private Label? _tip;

	private Button? _warningButton;
	private Button? _reloadButton;
	private Button? _removeButton;

	[Export]
	public bool IsCustomized { get; set; }

	public bool CanRemove { get; set; } = true;
	public string? SubscriptionUrl { get; set; }
	public NodeSubscription? Subscription { get; private set; }
	public event Action? SharedNodesChanged;

	public override void _Ready() {
		_sharedNodeItemScene.NotNull();
		_addSharedNodeWindowScene.NotNull();
		_removeIcon.NotNull();
		_addIcon.NotNull();
		_reloadIcon.NotNull();
		NodeList.NotNull();

		NodeList.ChildOrderChanged += Update;

		if (!IsCustomized) {
			SubscriptionUrl.NotNull();
			Title = GetLastValidPartSpan(SubscriptionUrl);

			var hBox = new HBoxContainer();
			hBox.AddThemeConstantOverride(StringNames.Separation, 0);

			_warningButton = new() {
				CustomMinimumSize = new(28, 28),
				Disabled = true,
				Visible = false,
				Icon = _warningIcon,
				ExpandIcon = true,
				TooltipText = "订阅节点获取失败",
				Flat = true,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill
			};
			_warningButton.AddThemeColorOverride(StringNames.IconDisabledColor, Colors.Red);
			hBox.AddChild(_warningButton);

			_reloadButton = new() {
				CustomMinimumSize = new(28, 28),
				Icon = _reloadIcon,
				ExpandIcon = true,
				TooltipText = "刷新订阅",
				Flat = true,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill
			};
			_reloadButton.Pressed += GetSubscription;
			hBox.AddChild(_reloadButton);

			if (CanRemove) {
				_removeButton = new() {
					CustomMinimumSize = new(28, 28),
					Icon = _removeIcon,
					ExpandIcon = true,
					TooltipText = "删除订阅",
					Flat = true,
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					SizeFlagsVertical = SizeFlags.ExpandFill
				};
				_removeButton.Pressed += RemoveButtonOnPressed;
				hBox.AddChild(_removeButton);
			}

			AddTitleBarControl(hBox);

			GetSubscription();
		} else {
			var addButton = new Button {
				CustomMinimumSize = new(28, 28),
				Icon = _addIcon,
				ExpandIcon = true,
				TooltipText = "添加节点",
				Flat = true,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill
			};
			addButton.Pressed += AddButtonOnPressed;

			foreach (var customPublicServer in Main.BaseConfig.CustomSharedNodes) {
				var sharedNodeItem = _sharedNodeItemScene!.Instantiate<SharedNodeItem>();
				sharedNodeItem.SharedNode = customPublicServer;
				sharedNodeItem.IsCustomized = true;
				NodeList.AddChild(sharedNodeItem);
			}

			AddTitleBarControl(addButton);
		}
	}

	private void RemoveButtonOnPressed() {
		var confirmationWindow = Main.Instance!.OpenConfirmationWindow(string.Format(Tr("确定要删除订阅 {0} 吗？"), Title));
		confirmationWindow.Hidden += confirmationWindow.QueueFree;
		confirmationWindow.Confirm += async () => {
			confirmationWindow.OkButton?.Disabled = true;
			await confirmationWindow.Hide();
			if (SubscriptionUrl != null) {
				Main.BaseConfig.CustomNodeSubscriptions.Remove(SubscriptionUrl);
			}

			await Main.BaseConfig.SaveAsync();
			QueueFree();
		};
		_ = confirmationWindow.Show();
	}

	public override void _ExitTree() { NodeList!.ChildOrderChanged -= Update; }

	private void AddButtonOnPressed() {
		var addSharedNodeWindow = _addSharedNodeWindowScene!.Instantiate<AddSharedNodeWindow>();
		addSharedNodeWindow.AddSharedNode += AddSharedNodeWindowOnAddSharedNode;
		addSharedNodeWindow.Hidden += addSharedNodeWindow.QueueFree;
		Main.Instance?.AddChild(addSharedNodeWindow);
		_ = addSharedNodeWindow.Show();
	}

	private void AddSharedNodeWindowOnAddSharedNode(SharedNode sharedNode) {
		Main.BaseConfig.CustomSharedNodes.Insert(0, sharedNode);
		Main.BaseConfig.Save();

		var sharedNodeItem = _sharedNodeItemScene!.Instantiate<SharedNodeItem>();
		sharedNodeItem.SharedNode = sharedNode;
		sharedNodeItem.IsCustomized = true;
		sharedNodeItem.TreeExited += () => SharedNodesChanged?.Invoke();
		NodeList!.AddChild(sharedNodeItem);
		NodeList.MoveChild(sharedNodeItem, 0);
		SharedNodesChanged?.Invoke();
	}

	private async void GetSubscription() {
		foreach (var child in NodeList!.GetChildren()) {
			child.Free();
		}

		Subscription = null;
		_tip!.Text = "获取订阅节点中...";
		_warningButton!.Visible = false;
		_reloadButton!.Disabled = true;
		_removeButton?.Disabled = true;
		try {
			Log.Debug($"正在获取订阅节点：{GetLastValidPartSpan(SubscriptionUrl)}");
			await using var result = await SubscriptionUrl.GetStreamAsync();
			Subscription = await JsonSerializer.DeserializeAsync(result, SourceGenerationContext.Default.NodeSubscription);
			Log.Debug(
				$"获取订阅节点成功：{Subscription!.Name ?? GetLastValidPartSpan(SubscriptionUrl)}，节点数量：{Subscription.Nodes.Length}");
			if (!IsInstanceValid(this)) {
				return;
			}

			Title = Subscription.Name ?? GetLastValidPartSpan(SubscriptionUrl);
			foreach (var sharedNode in Subscription.Nodes) {
				var sharedNodeItem = _sharedNodeItemScene!.Instantiate<SharedNodeItem>();
				sharedNodeItem.SharedNode = sharedNode;
				NodeList!.AddChild(sharedNodeItem);
			}

			Update();
		} catch (Exception e) {
			_warningButton!.Visible = true;
			_tip!.Text = "获取订阅节点失败";
			Log.Error("获取订阅节点失败", e);
		}

		_reloadButton!.Disabled = false;
		_removeButton?.Disabled = false;
		SharedNodesChanged?.Invoke();
	}

	private void Update() {
		if (!IsInstanceValid(this)) {
			return;
		}

		if (IsCustomized) {
			_tip!.Text = Main.BaseConfig.CustomSharedNodes.Count == 0 ? "暂无可用节点" : "";
			return;
		}

		_tip!.Text = Subscription?.Nodes.Length == 0 ? "暂无可用节点" : "";
	}

	static private string GetLastValidPartSpan(ReadOnlySpan<char> urlSpan) {
		var queryOrHashIndex = urlSpan.IndexOfAny('?', '#');
		if (queryOrHashIndex >= 0) {
			urlSpan = urlSpan[..queryOrHashIndex];
		}

		urlSpan = urlSpan.TrimEnd('/');

		var schemeIndex = urlSpan.IndexOf("://");
		var contentStartIndex = schemeIndex >= 0 ? schemeIndex + 3 : 0;

		var lastSlashIndex = urlSpan.LastIndexOf('/');

		return lastSlashIndex >= contentStartIndex
			? urlSpan[(lastSlashIndex + 1)..].ToString()
			: urlSpan[contentStartIndex..].ToString();
	}
}
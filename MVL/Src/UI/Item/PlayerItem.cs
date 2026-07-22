using System;
using Godot;
using MVL.Utils.Multiplayer;

namespace MVL.UI.Item;

public partial class PlayerItem : HBoxContainer {
	[Export]
	private Label? _hostBadge;
	[Export]
	private Label? _nameLabel;
	[Export]
	private Label? _versionLabel;
	[Export]
	private Label? _pingLabel;

	public RoomPlayerInfo? PlayerInfo { get; private set; }

	public void Update(RoomPlayerInfo player, bool showWarning) {
		PlayerInfo = player;

		_hostBadge!.Visible = player.RoomType == RoomType.Host;

		_nameLabel!.Text = showWarning ? $"{player.Name} ⚠" : player.Name;
		_nameLabel.Modulate = showWarning ? Colors.Yellow : Colors.White;
		_nameLabel.TooltipText = showWarning ? "名称可能引起冲突" : string.Empty;

		_versionLabel!.Text = $"v{player.Version}";

		UpdatePing();
	}

	public void UpdatePing() {
		if (PlayerInfo is not { } p || _pingLabel is null) {
			return;
		}

		if (p.Latency == TimeSpan.Zero) {
			_pingLabel.Visible = false;
			return;
		}

		_pingLabel.Visible = true;
		var ping = p.Latency;
		var color = ping.TotalMilliseconds switch { >= 150 => Colors.LightCoral, >= 60 => Colors.Yellow, _ => Colors.DarkSeaGreen };
		_pingLabel.Text = $"{ping.TotalMilliseconds:F0}ms";
		_pingLabel.LabelSettings.FontColor = color;
	}
}
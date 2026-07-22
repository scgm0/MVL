using System;
using System.Text.Json.Serialization;
using PolyType;

namespace MVL.Utils.Multiplayer;

[GenerateShape]
public partial record RoomPlayerInfo(
	RoomType RoomType,
	string Name,
	ushort Port,
	string Address,
	string Version,
	bool Offline) {
	public uint Identity { get; set; }
	public TimeSpan Latency { get; set; }
	[JsonIgnore]
	[PropertyShape(Ignore = true)]
	public DateTimeOffset LastHeartbeat { get; set; }
}
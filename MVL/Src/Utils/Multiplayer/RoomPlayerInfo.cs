using System;
using System.Text.Json.Serialization;
using PolyType;

namespace MVL.Utils.Multiplayer;

[GenerateShape]
public partial record RoomPlayerInfo(
	RoomType RoomType,
	string Name,
	int Port,
	string Address,
	string Version) {
	public uint Identity { get; set; }

	[JsonIgnore]
	[PropertyShape(Ignore = true)]
	public DateTimeOffset LastHeartbeat { get; set; }
}
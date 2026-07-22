namespace MVL.Utils.Multiplayer;

public enum RoomEventEnum {
	GuestJoined,
	JoinAccepted,
	AddGuest,
	GuestLeft,
	HostShutdown,
	Heartbeat,
	HeartbeatAck,
	PlayerUpdate,
	None = -1
}
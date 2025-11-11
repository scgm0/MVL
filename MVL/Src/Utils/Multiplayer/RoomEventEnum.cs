namespace MVL.Utils.Multiplayer;

public enum RoomEventEnum {
	GuestJoined,
	JoinAccepted,
	AddGuest,
	GuestLeft,
	HostShutdown,
	Ping,
	Pong,
	None = -1
}
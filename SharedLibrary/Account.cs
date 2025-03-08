namespace SharedLibrary;

public class Account {
	public string? PlayerName { get; set; }
	public string? Uid { get; set; }
	public string? SessionKey { get; set; }
	public string? SessionSignature { get; set; }
	public string? Email { get; set; }
	public string? Entitlements { get; set; }
	public bool HasGameServer { get; set; }
	public bool Offline { get; set; }
}
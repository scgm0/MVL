namespace MVL.Utils.Game;

public record LoginResponse {
	public int Valid { get; set; }
	public string? Reason { get; set; }
	public string? ReasonData { get; set; }
	public string? SessionKey { get; set; }
	public string? SessionSignature { get; set; }
	public string? Uid { get; set; }
	public string? PlayerName { get; set; }
	public string? Entitlements { get; set; }
	public string? PreLoginToken { get; set; }
	public bool HasGameServer { get; set; }
}
namespace MVL.Utils.Game;

public struct ValidateResponse {
	public int Valid { get; set; }
	public string Entitlements { get; set; }
	public string Reason { get; set; }
	public bool HasGameServer { get; set; }
}
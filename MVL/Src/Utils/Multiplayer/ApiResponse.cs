namespace MVL.Utils.Multiplayer;

public record struct ApiResponse {
	public bool Success { get; set; }

	public ApiData Data { get; set; }
}
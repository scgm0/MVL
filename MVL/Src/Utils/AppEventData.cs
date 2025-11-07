using System.Text.Json;

namespace MVL.Utils;

public record struct AppEventData(AppEventEnum EventCode, string? Message = null) {
	public override string ToString() {
		return JsonSerializer.Serialize(this, SourceGenerationContext.Default.AppEventData);
	}

	public static AppEventData Parse(byte[] data) {
		try {
			var appEventData = JsonSerializer.Deserialize(data, SourceGenerationContext.Default.AppEventData);
			return appEventData;
		} catch {
			return new (AppEventEnum.None);
		}
	}
}
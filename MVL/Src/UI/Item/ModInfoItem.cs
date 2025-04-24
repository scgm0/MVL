using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl.Http;
using MVL.Utils;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class ModInfoItem : PanelContainer {
	[Export]
	private TextureRect? _icon;

	[Export]
	private Label? _name;

	[Export]
	private Label? _version;

	[Export]
	private Label? _description;

	public ModInfo? Mod { get; set; }

	public override async void _Ready() {
		_icon.NotNull();
		_name.NotNull();
		_version.NotNull();
		_description.NotNull();
		_icon.Texture = Mod?.Icon;
		_name.Text = Mod?.Name;
		_version.Text = Mod?.Version;
		_description.Text = Mod?.Description;

		if (ModInfo.IsValidModId(Mod.ModId)) {
			var url = $"https://mods.vintagestory.at/api/mod/{Mod.ModId}";
			var result = await url.GetStringAsync();
			var status = JsonSerializer.Deserialize(result, SourceGenerationContext.Default.ApiStatusModInfo);
			GD.Print(status.Mod);
		}
	}
}

public class ApiStatusModInfo {
	public required string StatusCode { get; init; }
	public ApiModInfo? Mod { get; init; }
}

public record ApiModInfo {
	public int ModId { get; init; }
	public int AssetId { get; init; }
	public string Name { get; init; }
	public string Text { get; init; }
	public string Author { get; init; }
	public string UrlAlias { get; init; }
	public string LogoFileName { get; init; }
	public string LogoFile { get; init; }
	public string HomePageUrl { get; init; }
	public string Sourcecodeurl { get; init; }
	public string TrailerVideoUrl { get; init; }
	public string IssueTrackerUrl { get; init; }
	public string WikiUrl { get; init; }
	public int Downloads { get; init; }
	public int Follows { get; init; }
	public int TrendingPoints { get; init; }
	public int Comments { get; init; }
	public string Side { get; init; }
	public string Type { get; init; }
	public DateTimeOffset Created { get; init; }
	public DateTimeOffset LastModified { get; init; }
	public string[] Tags { get; init; }
	public ApiModRelease[] Releases { get; init; }
}

public record ApiModRelease {
	public int ReleaseId { get; init; }
	public string Mainfile { get; init; }
	public string Filename { get; init; }
	public int FileId { get; init; }
	public int Downloads { get; init; }
	public string[] Tags { get; init; }
	public string Modidstr { get; init; }
	public string Modversion { get; init; }
	public DateTimeOffset Created { get; init; }
}

public class DateTimeOffsetConverter : JsonConverter<DateTimeOffset> {
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return DateTimeOffset.Parse(reader.GetString() ?? string.Empty);
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}
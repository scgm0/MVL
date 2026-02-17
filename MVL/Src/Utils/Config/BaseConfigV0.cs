using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SharedLibrary;
using Environment = System.Environment;

namespace MVL.Utils.Config;

public class BaseConfigV0 {
	[JsonPropertyOrder(0)]
	public string CurrentModpack { get; set; } = "";

	[JsonPropertyOrder(0)]
	public string CurrentAccount { get; set; } = "";

	[JsonPropertyOrder(1)]
	public string DisplayLanguage { get; set; } = OS.GetLocale();

	[JsonPropertyOrder(1)]
	public double DisplayScale { get; set; } = Tools.GetAutoDisplayScale();

	[JsonPropertyOrder(1)]
	public bool MenuExpand { get; set; } = true;

	[JsonPropertyOrder(2)]
	public string ProxyAddress { get; set; } = "";

	[JsonPropertyOrder(2)]
	public int DownloadThreads { get; set; } = Environment.ProcessorCount;

	[JsonPropertyOrder(3)]
	public string ModpackFolder { get; set; } = Paths.ModpackFolder;

	[JsonPropertyOrder(3)]
	public string ReleaseFolder { get; set; } = Paths.ReleaseFolder;

	[JsonPropertyOrder(int.MaxValue)]
	public List<string> Release { get; set; } = [];

	[JsonPropertyOrder(int.MaxValue)]
	public List<string> Modpack { get; set; } = [];

	[JsonPropertyOrder(int.MaxValue)]
	public List<Account> Account { get; set; } = [];

	[JsonExtensionData]
	public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
}
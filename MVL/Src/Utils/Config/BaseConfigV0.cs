using Godot;
using SharedLibrary;
using Environment = System.Environment;

namespace MVL.Utils.Config;

public partial class BaseConfig {
	public class V0 {
		public string CurrentModpack { get; set; } = "";
		public string CurrentAccount { get; set; } = "";
		public string DisplayLanguage { get; set; } = OS.GetLocale();
		public double DisplayScale { get; set; } = Tools.GetAutoDisplayScale();
		public bool MenuExpand { get; set; } = true;
		public string ProxyAddress { get; set; } = "";
		public int DownloadThreads { get; set; } = Environment.ProcessorCount;
		public string ModpackFolder { get; set; } = Paths.ModpackFolder;
		public string ReleaseFolder { get; set; } = Paths.ReleaseFolder;
		public string[] Release { get; set; } = [];
		public string[] Modpack { get; set; } = [];
		public Account[] Account { get; set; } = [];
	}
}
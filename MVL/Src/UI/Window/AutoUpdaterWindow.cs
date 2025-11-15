using System.Threading.Tasks;
using Godot;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.GitHub;
using MVL.Utils.Help;

namespace MVL.UI.Window;

public partial class AutoUpdaterWindow : BaseWindow {
	[Export]
	private RichTextLabel? _richTextLabel;

	public override void _Ready() {
		base._Ready();
		_richTextLabel.NotNull();

		_richTextLabel.MetaClicked += Tools.RichTextOpenUrl;
		CancelButton!.Pressed += CancelButtonOnPressed;
		Hidden += QueueFree;
	}

	public async Task GetLatestRelease() {
		await Show();
		var apiRelease = await GitHubTool.GetLatestReleaseAsync("scgm0/MVL", GhProxyEnum.V6);
		var body = apiRelease.Body.SplitAndConvert().chinese;
		_richTextLabel!.Text = body.ConvertMarkdownToBbcode();
	}
}
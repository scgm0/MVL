using Godot;
using System.Threading.Tasks;
using MVL.Utils;
using MVL.Utils.Extensions;
using MVL.Utils.Game;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class BrowseItem : Button {
	[Export]
	private TextureRect? _modIconTextureRect;

	[Export]
	private Label? _modNameLabel;

	[Export]
	private Label? _modDescriptionLabel;

	[Export]
	private Button? _modAuthorButton;

	[Export]
	private Button? _modDownloadsButton;

	[Export]
	private Button? _modFollowsButton;

	[Export]
	private Button? _modCommentsButton;

	public ApiModSummary? ModSummary { get; set; }

	public override void _Ready() {
		_modIconTextureRect.NotNull();
		_modNameLabel.NotNull();
		_modDescriptionLabel.NotNull();
		_modAuthorButton.NotNull();
		_modDownloadsButton.NotNull();
		_modFollowsButton.NotNull();
		_modCommentsButton.NotNull();
		ModSummary.NotNull();

		_modAuthorButton.GetParent<Container>().MouseFilter = MouseFilterEnum.Stop;
		_modNameLabel.Text = ModSummary.Value.Name;
		_modDescriptionLabel.Text = ModSummary.Value.Summary;
		_modAuthorButton.Text = ModSummary.Value.Author;
		_modDownloadsButton.Text = ModSummary.Value.Downloads.FormatNumber();
		_modFollowsButton.Text = ModSummary.Value.Follows.FormatNumber();
		_modCommentsButton.Text = ModSummary.Value.Comments.FormatNumber();

		_modAuthorButton.Pressed += OpenModDbPage;
		_modDownloadsButton.Pressed += OpenModDbPage;
		_modFollowsButton.Pressed += OpenModDbPage;
		_modCommentsButton.Pressed += OpenModDbPage;

		GetLogoTexture();
	}

	private void OpenModDbPage() {
		Tools.RichTextOpenUrl(ModSummary!.Value.UrlAlias is null
			? $"https://mods.vintagestory.at/show/mod/{ModSummary!.Value.AssetId}"
			: $"https://mods.vintagestory.at/{ModSummary!.Value.UrlAlias}");
	}

	private async void GetLogoTexture() {
		if (ModSummary!.Value.Logo is null) {
			return;
		}

		ImageTexture? texture = null;
		await Task.Run(async () => { texture = await Tools.LoadTextureFromUrl(ModSummary.Value.Logo); });
		if (IsInstanceValid(this) && texture is not null) {
			_modIconTextureRect!.Texture = texture;
		}
	}
}
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
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
		_modNameLabel.Text = ModSummary.Name;
		_modDescriptionLabel.Text = ModSummary.Summary;
		_modAuthorButton.Text = ModSummary.Author;
		_modDownloadsButton.Text = ModSummary.Downloads.FormatNumber();
		_modFollowsButton.Text = ModSummary.Follows.FormatNumber();
		_modCommentsButton.Text = ModSummary.Comments.FormatNumber();

		_modAuthorButton.Pressed += OpenModDbPage;
		_modDownloadsButton.Pressed += OpenModDbPage;
		_modFollowsButton.Pressed += OpenModDbPage;
		_modCommentsButton.Pressed += OpenModDbPage;

		GetLogoTexture();
	}

	private void OpenModDbPage() {
		Tools.RichTextOpenUrl(ModSummary!.UrlAlias is null
			? $"https://mods.vintagestory.at/show/mod/{ModSummary!.AssetId}"
			: $"https://mods.vintagestory.at/{ModSummary!.UrlAlias}");
	}

	private async void GetLogoTexture() {
		if (ModSummary!.Logo is null) {
			return;
		}

		var path = Path.Join(Paths.CacheFolder, $"{new Guid(ModSummary.Logo.Sha256Buffer().Take(16).ToArray())}.png");
		ImageTexture? texture = null;
		await Task.Run(async () => {
			try {
				if (!File.Exists(path)) {
					GD.Print(ModSummary.Logo);
					var buffer = await ModSummary.Logo.GetBytesAsync();
					texture = Tools.CreateTextureFromBytes(buffer, Tools.GetImageFormatWithSwitch(buffer));
					texture.GetImage().SavePng(path);
				} else {
					texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path));
				}
			} catch (Exception e) {
				GD.PrintErr(e);
			}
		});
		if (IsInstanceValid(this) && texture is not null) {
			_modIconTextureRect!.Texture = texture;
		}
	}
}
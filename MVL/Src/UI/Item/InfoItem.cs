using System.Threading.Tasks;
using Godot;
using MVL.Utils.Help;

namespace MVL.UI.Item;

public partial class InfoItem : Control {
	[Export]
	private Label? _title;

	[Export]
	private RichTextLabel? _content;

	public string Title {
		get;
		set {
			field = value;
			if (_title != null) {
				_title.Text = value;
			}
		}
	} = "";

	public string Content {
		get;
		set {
			field = value;
			if (_content != null) {
				_content.Text = value;
			}
		}
	} = "";

	public override void _Ready() {
		_title.NotNull();
		_content.NotNull();
		_title.Text = Title;
		_content.Text = Content;
		_content.MetaClicked += Main.RichTextOpenUrl;
	}
}
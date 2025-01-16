using Godot;

namespace MVL.Utils;

[Tool]
[GlobalClass]
public partial class SvgTexture2D : CanvasTexture {
	private string _svgString = "";
	private Image _image = new();

	[Export(PropertyHint.File, "*.svg")]
	public string SvgPath {
		get;
		set {
			field = value;
			LoadSvgImage(value, Scale);
		}
	} = "";

	[Export]
	public float Scale {
		get;
		set {
			field = value;
			LoadSvgImage(SvgPath, value);
		}
	} = 1.0f;

	private void LoadSvgImage(string path, float scale) {
		if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path)) {
			return;
		}
		var fileContent = FileAccess.GetFileAsString(path);
		_image.LoadSvgFromString(fileContent, scale);
		DiffuseTexture = ImageTexture.CreateFromImage(_image);
	}
}
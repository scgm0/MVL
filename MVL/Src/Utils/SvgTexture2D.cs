using Godot;

namespace MVL.Utils;

[Tool]
[GlobalClass]
public partial class SvgTexture2D : ImageTexture {
	private string _svgString = "";
	private Image _image = Image.CreateEmpty(1, 1, true, Image.Format.Rgbaf);

	[Export(PropertyHint.File, "*.svg")]
	public string SvgPath {
		get;
		set {
			field = value;
			LoadSvgImage(value, Scale);
		}
	} = "";

	[Export]
	public double Scale {
		get;
		set {
			field = value;
			LoadSvgImage(SvgPath, value);
		}
	} = 1.0;

	private void LoadSvgImage(string path, double scale) {
		if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path)) {
			return;
		}

		var fileContent = FileAccess.GetFileAsBytes(path);
		_image.LoadSvgFromBuffer(fileContent, (float)scale);
		SetImage(_image);

		EmitChanged();
	}
}
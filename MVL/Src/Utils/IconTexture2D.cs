using System.Collections.Generic;
using System.Globalization;
using Godot;
using MVL.Utils.Attribute;

namespace MVL.Utils;

[Tool]
[GlobalClass]
public partial class IconTexture2D : Texture2D {
	[Export]
	public string IconName {
		get;
		set {
			field = value;
			_iconChar = Icons.TryGetValue(value, out var hexString) ? long.Parse(hexString, NumberStyles.HexNumber) : 0;
			EmitChanged();
		}
	} = "";

	[Export]
	public int FontSize {
		get;
		set {
			field = value;
			EmitChanged();
		}
	} = 24;

	private long _iconChar;

	public IconTexture2D() { FontFile.Changed += EmitChanged; }

	public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) {
		var newPos = new Vector2(pos.X, pos.Y + FontFile.GetAscent(FontSize));
		FontFile.DrawChar(toCanvasItem, newPos, _iconChar, FontSize, modulate);
	}

	public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose) {
		var pos = new Vector2(rect.Position.X, rect.Position.Y + FontFile.GetAscent(FontSize));
		FontFile.DrawChar(toCanvasItem, pos, _iconChar, FontSize, modulate);
	}

	public override int _GetHeight() { return (int)FontFile.GetCharSize(_iconChar, FontSize).Y; }

	public override int _GetWidth() { return (int)FontFile.GetCharSize(_iconChar, FontSize).X; }

	static private readonly FontFile FontFile = GD.Load<FontFile>("uid://dp7r8itfqvqca");

	[JsonDictionary("Assets/Icon/MD/icons.json")]
	static private partial Dictionary<string, string> Icons { get; }
}
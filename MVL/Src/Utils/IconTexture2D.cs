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
			if (field == value) return;
			field = value;
			_iconChar = Icons.TryGetValue(value, out var hexString) ? long.Parse(hexString, NumberStyles.HexNumber) : 0;
			EmitChanged();
		}
	} = "";

	[Export]
	public int FontSize {
		get;
		set {
			if (field == value) return;
			field = value;
			EmitChanged();
		}
	} = 24;

	[Export]
	public FontFile? FontFile {
		get;
		set {
			if (field == value) return;
			if (field != null) {
				field.Changed -= ((Resource)this).EmitChanged;
			}

			field = value;
			if (value != null) {
				value.Changed += ((Resource)this).EmitChanged;
			}

			EmitChanged();
		}
	} = GD.Load<FontFile>("uid://dp7r8itfqvqca");

	private long _iconChar;
	private Vector2I _iconSize;

	public IconTexture2D() { FontFile.Changed += EmitChanged; }

	public new void EmitChanged() {
		_iconSize = (Vector2I)(FontFile?.GetCharSize(_iconChar, FontSize) ?? Vector2I.Zero);
		base.EmitChanged();
	}

	public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) {
		if (FontFile == null) return;
		var newPos = new Vector2(pos.X, pos.Y + FontFile.GetAscent(FontSize));
		FontFile.DrawChar(toCanvasItem, newPos, _iconChar, FontSize, modulate);
	}

	public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose) {
		if (FontFile == null) return;
		var pos = new Vector2(rect.Position.X, rect.Position.Y + FontFile.GetAscent(FontSize));
		FontFile.DrawChar(toCanvasItem, pos, _iconChar, FontSize, modulate);
	}

	public override int _GetHeight() { return _iconSize.Y; }

	public override int _GetWidth() { return _iconSize.X; }

	[JsonDictionary("Assets/Icon/MD/icons.json")]
	static private partial Dictionary<string, string> Icons { get; }
}
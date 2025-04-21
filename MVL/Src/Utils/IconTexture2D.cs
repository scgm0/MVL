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

	private SvgTexture _svgTexture = new();

	public IconTexture2D() { EmitChanged(); }

	public new void EmitChanged() {
		_svgTexture.Source = Icons.TryGetValue(IconName, out var str) ? str : string.Empty;
		_svgTexture.SetSizeOverride(new(FontSize, FontSize));
		_svgTexture.EmitChanged();
		base.EmitChanged();
	}

	public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) {
		_svgTexture.Draw(toCanvasItem, pos, modulate, transpose);
	}

	public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose) {
		_svgTexture.DrawRect(toCanvasItem, rect, tile, modulate, transpose);
	}

	public override void _DrawRectRegion(
		Rid toCanvasItem,
		Rect2 rect,
		Rect2 srcRect,
		Color modulate,
		bool transpose,
		bool clipUv) {
		_svgTexture.DrawRectRegion(toCanvasItem, rect, srcRect, modulate, transpose, clipUv);
	}

	public override int _GetHeight() { return _svgTexture.GetHeight(); }

	public override int _GetWidth() { return _svgTexture.GetWidth(); }

	[JsonDictionary("Assets/Icon/MD/icons.json")]
	static private partial Dictionary<string, string> Icons { get; }
}
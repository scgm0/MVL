using System;
using Godot;

namespace MVL.Utils;

public static class Tools {
	public static float GetAutoDisplayScale() {
#if GODOT_LINUXBSD
		if (DisplayServer.GetName() == "Wayland") {
			var mainWindowScale = DisplayServer.ScreenGetScale();

			if (DisplayServer.GetScreenCount() == 1 || Fract(mainWindowScale) != 0) {
				return mainWindowScale;
			}

			return DisplayServer.ScreenGetMaxScale();
		}
#endif
		var screen = DisplayServer.WindowGetCurrentScreen();

		if (DisplayServer.ScreenGetSize(screen) == default) {
			return 1;
		}

		var size = DisplayServer.ScreenGetSize(screen);
		var smallestDimension = MathF.Min(size.X, size.Y);
		if (DisplayServer.ScreenGetDpi(screen) >= 192 && smallestDimension >= 1400) {
			return 2;
		}

		return smallestDimension switch { >= 1700 => 1.5f, <= 800 => 0.75f, _ => 1.0f };
	}

	public static double Fract(double value) {
		if (double.IsInfinity(value) || double.IsNaN(value)) {
			return 0;
		}

		return value - Math.Floor(value);
	}
}
using Godot;

namespace MVL.Utils.Extensions;

public static partial class WindowExtensions {

	extension(Window window) {
		public void Minimize() {
			window.Mode = Window.ModeEnum.Minimized;
		}

		public void Maximize() {
			window.Mode = Window.ModeEnum.Windowed;
		}
	}
}
using Godot;

namespace 复古物语启动器.Utils.Extensions;

public static partial class WindowExtensions {
	public static void StartMoveDrag(this Window window) { DisplayServer.WindowStartDrag(window.GetWindowId()); }
	public static partial void StartResizeDrag(this Window window, WindowEdge edge);
	public static partial void Minimize(this Window window);
}
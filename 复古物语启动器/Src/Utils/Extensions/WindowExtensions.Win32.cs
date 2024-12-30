#if GODOT_WINDOWS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace 复古物语启动器.Utils.Extensions;

public static partial class WindowExtensions {
	private const int ScSize = 0xF000;
	private const int ScMousemove = 0xf012;
	private const int WmSysCommand = 0x0112;
	private const int WmLButtonUp = 0x0202;
	private const int WmNcLButtonDown = 0x00a1;
	private const int SwMinimize = 0x6;

	[LibraryImport("user32.dll", EntryPoint = "ReleaseCapture")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static private partial bool ReleaseCapture();

	[LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
	static private partial IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


	[LibraryImport("user32", EntryPoint = "DefWindowProcW")]
	static private partial void DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	[LibraryImport("user32")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static private partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

	static private readonly Dictionary<WindowEdge, HitTestValues> SEdgeLookup =
		new() {
			{ WindowEdge.Right, HitTestValues.HtRight },
			{ WindowEdge.Top, HitTestValues.HtTop },
			{ WindowEdge.Top | WindowEdge.Right, HitTestValues.HtTopRight },
			{ WindowEdge.Top | WindowEdge.Left, HitTestValues.HtTopLeft },
			{ WindowEdge.Bottom, HitTestValues.HtBottom },
			{ WindowEdge.Bottom | WindowEdge.Right, HitTestValues.HtBottomRight },
			{ WindowEdge.Bottom | WindowEdge.Left, HitTestValues.HtBottomLeft },
			{ WindowEdge.Left, HitTestValues.HtLeft }
		};

	// public static partial void StartMoveDrag(this Godot.Window window) {
	// 	var handle = new IntPtr(
	// 		DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId()));
	// 	ReleaseCapture();
	// 	SendMessage(handle, WmSysCommand, ScMousemove, IntPtr.Zero);
	// 	SendMessage(handle, WmLButtonUp, IntPtr.Zero, IntPtr.Zero);
	// 	// Dispatcher.SynchronizationContext.Post(_ => {
	// 	// 		SendMessage(handle, WmSysCommand, ScMousemove, IntPtr.Zero);
	// 	// 		SendMessage(handle, WmLButtonUp, IntPtr.Zero, IntPtr.Zero);
	// 	// 	},
	// 	// 	handle);
	// }

	public static partial void StartResizeDrag(this Godot.Window window, WindowEdge edge) {
		var handle = new IntPtr(
			DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId()));
		ReleaseCapture();
		DefWindowProc(handle,
			WmNcLButtonDown,
			(int)SEdgeLookup[edge],
			IntPtr.Zero);
	}

	public static partial void Minimize(this Window window) {
		var handle = new IntPtr(
			DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId()));
		ReleaseCapture();
		ShowWindow(handle, SwMinimize);
	}
}

enum HitTestValues {
	HtError = -2,
	HtTransparent = -1,
	HtNowhere = 0,
	HtClient = 1,
	HtCaption = 2,
	HtSysMenu = 3,
	HtGrowBox = 4,
	HtMenu = 5,
	HtHScroll = 6,
	HtVScroll = 7,
	HtMinButton = 8,
	HtMaxButton = 9,
	HtLeft = 10,
	HtRight = 11,
	HtTop = 12,
	HtTopLeft = 13,
	HtTopRight = 14,
	HtBottom = 15,
	HtBottomLeft = 16,
	HtBottomRight = 17,
	HtBorder = 18,
	HtObject = 19,
	HtClose = 20,
	HtHelp = 21
}

#endif
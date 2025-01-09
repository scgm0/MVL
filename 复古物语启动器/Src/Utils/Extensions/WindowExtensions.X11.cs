#if GODOT_LINUXBSD
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace 复古物语启动器.Utils.Extensions;

public static partial class WindowExtensions {
	[LibraryImport("libX11")]
	static private partial void XSendEvent(
		IntPtr display,
		IntPtr window,
		[MarshalAs(UnmanagedType.Bool)] bool propagate,
		long eventMask,
		ref XEvent sendEvent);

	[LibraryImport("libX11")] static private partial IntPtr XRootWindow(IntPtr display, int screenNumber);

	[LibraryImport("libX11")] static private partial void XUngrabPointer(IntPtr display, IntPtr time);

	[LibraryImport("libX11")] static private partial void XFlush(IntPtr display);

	[LibraryImport("libX11", StringMarshalling = StringMarshalling.Utf8)]
	static private partial IntPtr XInternAtom(
		IntPtr display,
		string atomName,
		[MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

	[LibraryImport("libX11")]
	static private partial void XIconifyWindow(IntPtr display, IntPtr window, int screen);

	[LibraryImport("libX11")] static private partial void XMapWindow(IntPtr display, IntPtr window);

	[LibraryImport("libX11")] static private partial void XRaiseWindow(IntPtr display, IntPtr window);

	static private IntPtr DisplayHandle { get; } =
		new(DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.DisplayHandle));

	static private IntPtr RootWindowHandle { get; } = XRootWindow(DisplayHandle, 0);
	private const int SubstructureNotifyMask = 1 << 19;
	private const int SubstructureRedirectMask = 1 << 20;

	static private void SendNetWmMessage(
		IntPtr handle,
		IntPtr messageType,
		IntPtr l0,
		IntPtr? l1 = null,
		IntPtr? l2 = null,
		IntPtr? l3 = null,
		IntPtr? l4 = null) {
		var xev = new XEvent {
			type = 33,
			ClientMessageEvent = {
				type = 33,
				send_event = 1,
				window = handle,
				message_type = messageType,
				format = 32,
				ptr1 = l0,
				ptr2 = l1 ?? IntPtr.Zero,
				ptr3 = l2 ?? IntPtr.Zero,
				ptr4 = l3 ?? IntPtr.Zero
			}
		};
		xev.ClientMessageEvent.ptr4 = l4 ?? IntPtr.Zero;
		XSendEvent(DisplayHandle,
			RootWindowHandle,
			false,
			new IntPtr(SubstructureRedirectMask | SubstructureNotifyMask),
			ref xev);
		XFlush(DisplayHandle);
	}

	static private void StartMoveResize(IntPtr handle, NetWmMoveResize side) {
		var pos = DisplayServer.MouseGetPosition();
		XUngrabPointer(DisplayHandle, IntPtr.Zero);
		SendNetWmMessage(handle,
			Atoms.NetWmMoveResize,
			pos.X,
			pos.Y,
			(IntPtr)side,
			1,
			1);
	}

	public static partial void Minimize(this Window window) {
		var handle = new IntPtr(
			DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId()));
		XIconifyWindow(DisplayHandle, handle, window.CurrentScreen);
	}

	public static partial void Maximize(this Window window) {
		window.Mode = Window.ModeEnum.Maximized;
	}

	static private class Atoms {
		public static readonly IntPtr NetWmMoveResize = XInternAtom(DisplayHandle, "_NET_WM_MOVERESIZE", false);
		public static readonly IntPtr NetWmState = XInternAtom(DisplayHandle, "_NET_WM_STATE", false);

		public static readonly IntPtr NetWmStateMaximizedHorz =
			XInternAtom(DisplayHandle, "_NET_WM_STATE_MAXIMIZED_HORZ", false);

		public static readonly IntPtr NetWmStateMaximizedVert =
			XInternAtom(DisplayHandle, "_NET_WM_STATE_MAXIMIZED_VERT", false);
	}
}

[StructLayout(LayoutKind.Sequential)]
struct XClientMessageEvent {
	internal int type;
	internal IntPtr serial;
	internal int send_event;
	internal IntPtr display;
	internal IntPtr window;
	internal IntPtr message_type;
	internal int format;
	internal IntPtr ptr1;
	internal IntPtr ptr2;
	internal IntPtr ptr3;
	internal IntPtr ptr4;
	internal IntPtr ptr5;
}

[StructLayout(LayoutKind.Explicit)]
struct XEvent {
	[FieldOffset(0)] internal int type;
	[FieldOffset(0)] internal XClientMessageEvent ClientMessageEvent;
}

enum NetWmMoveResize {
	NetWmMoveResizeSizeTopLeft = 0,
	NetWmMoveResizeSizeTop = 1,
	NetWmMoveResizeSizeTopRight = 2,
	NetWmMoveResizeSizeRight = 3,
	NetWmMoveResizeSizeBottomRight = 4,
	NetWmMoveResizeSizeBottom = 5,
	NetWmMoveResizeSizeBottomLeft = 6,
	NetWmMoveResizeSizeLeft = 7,
	NetWmMoveResizeMove = 8,
	NetWmMoveResizeSizeKeyboard = 9,
	NetWmMoveResizeMoveKeyboard = 10,
	NetWmMoveResizeCancel = 11
}

#endif
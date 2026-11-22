using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// Whether a full-screen window (a video, a game) is in front on a monitor, so that the
// floating button there can hide under it. In front on that monitor, whichever monitor
// has the focus: Windows keeps its own taskbar under a full-screen program also while
// another monitor has the focus (measured), and a video on an external monitor while the
// user works on another one is the case that matters (owner's choice, To do 18).
// Measurements: docs/research/full-screen.md.
internal static class FullScreen
{
    // True when the front window on this monitor covers all of it. EnumWindows gives the
    // top windows from the front. Passed over: windows the user doesn't see there, and
    // always-on-top windows and tool windows that don't cover the monitor (the floating
    // buttons, the grid, small popups, the taskbar). The taskbar is a tool window: Windows
    // takes its always-on-top flag away while a full-screen window is on its monitor, and
    // it stays in front of one that never had the focus, such as the GUI test's (measured).
    // The first window left decides. A maximized window ends at the taskbar, so it never
    // covers the monitor. About 0.1 to 0.2 ms for about 440 windows (measured).
    // button: the floating button that would hide, passed over too: one that isn't always
    // on top, made while the full-screen window is in front, shows in front of it (code
    // review).
    public static bool InFront(Screen monitor, IntPtr button)
    {
        var bounds = monitor.Bounds;
        var desktop = GetShellWindow();
        var found = false;
        // Returns true to go on to the next window, false to stop.
        EnumWindows((window, _) =>
        {
            // Cloaked: on another virtual desktop, or a Store app's window that doesn't
            // show; such a window is "visible" to Windows all the same. No visible window
            // lacked DWM's bounds (measured); one that does is passed over.
            if (window == button || !IsWindowVisible(window) || IsCloaked(window) || !VisibleBounds(window, out var visible))
            {
                return true;
            }
            // No area on this monitor: on another one, minimized (at -32000), or 0 pixels
            // tall, as the toast window is while no toast shows.
            var overlap = Rectangle.Intersect(visible, bounds);
            if (overlap.Width == 0 || overlap.Height == 0)
            {
                return true;
            }
            if (visible.Contains(bounds))
            {
                // The desktop covers every monitor and is the last window: nothing was in
                // front of it.
                found = window != desktop;
                return false;
            }
            return (GetWindowLong(window, GwlExStyle) & (WsExTopmost | WsExToolWindow)) != 0;
        }, IntPtr.Zero);
        return found;
    }

    private static bool IsCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    // The window as it shows, in physical pixels. Not GetWindowRect: that includes Windows'
    // invisible resize border, with which a maximized window reaches 8 to 11 px into the
    // next monitor (measured), where it would count as a window in front.
    private static bool VisibleBounds(IntPtr window, out Rectangle bounds)
    {
        var ok = DwmGetWindowAttribute(window, DwmwaExtendedFrameBounds, out Rect r, Marshal.SizeOf<Rect>()) == 0;
        bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        return ok;
    }

    private const int GwlExStyle = -20;
    private const int WsExTopmost = 0x8;
    private const int WsExToolWindow = 0x80;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    // The desktop's window (Progman).
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
}

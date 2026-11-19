using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GuiTest;

// The Windows functions and messages the test works the app's windows with.
internal static class Win32
{
    public const int WmContextMenu = 0x007B;
    public const int WmNcActivate = 0x0086;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmSysCommand = 0x0112;
    public const int WmLButtonDown = 0x0201;
    public const int WmLButtonUp = 0x0202;
    public const int WmEnterSizeMove = 0x0231;
    public const int WmExitSizeMove = 0x0232;
    public const int WmHotkey = 0x0312;
    // With WM_LBUTTONDOWN: the left mouse button is down.
    public const int MkLButton = 0x1;
    public const int VkEscape = 0x1B;
    // With WM_SYSCOMMAND: what a click on a window's X sends.
    public const int ScClose = 0xF060;
    public const int GwlExStyle = -20;
    // The always-on-top flag among a window's extended styles.
    public const int WsExTopmost = 0x8;
    public const uint SwpNoSize = 0x1;
    public const uint SwpNoZOrder = 0x4;
    public const uint SwpNoActivate = 0x10;
    // The parent of message-only windows, such as the app's hotkey window.
    public static readonly IntPtr MessageOnlyParent = new(-3);
    // For DwmGetWindowAttribute: the window's frame as it shows, without the invisible border.
    public const int DwmwaExtendedFrameBounds = 9;
    // From SHQueryUserNotificationState: Windows shows notification popups now (no
    // full-screen program, no Do Not Disturb).
    public const int QunsAcceptsNotifications = 5;
    // The window of Windows' shell that shows every notification popup, as measured on
    // English Windows (docs/research/notifications.md).
    public const string ToastClass = "Windows.UI.Core.CoreWindow";
    public const string ToastTitle = "New notification";

    // A monitor's scaling: 1.0 at 100 %, 1.5 at 150 %. The same way as the app's Dpi.cs,
    // written again here so the test doesn't share a mistake with the app.
    public static float ScaleOf(Screen screen)
    {
        var monitor = MonitorFromPoint(screen.Bounds.Location, MonitorDefaultToNearest);
        return GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _) == 0 ? dpi / 96f : 1f;
    }

    // A window's rectangle, in physical pixels.
    public static Rectangle Bounds(IntPtr window)
    {
        GetWindowRect(window, out var r);
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    private const uint MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string title);

    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}

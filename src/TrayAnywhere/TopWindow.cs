using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// What the app's own windows share: the grid and the floating buttons stay on top, and
// they and the settings window stay inside the free area of their own monitor.
internal static class TopWindow
{
    // Sets a window's always-on-top flag again, only if it is gone: Windows can drop it
    // (docs/research/winforms-dpi.md). Only when gone, because setting it also lifts the
    // window above other always-on-top windows, such as notification popups in the same
    // corner. Not through Form.TopMost, which would also activate the window and take the
    // keyboard focus.
    public static void KeepOnTop(Form window)
    {
        if ((GetWindowLong(window.Handle, GwlExStyle) & WsExTopmost) == 0)
        {
            SetWindowPos(window.Handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
        }
    }

    // The point nearest to the given place where a window of this size is fully inside
    // the area (a monitor without its taskbar).
    public static Point KeepInside(Rectangle area, Point place, Size size) => new(
        Math.Max(area.Left, Math.Min(place.X, area.Right - size.Width)),
        Math.Max(area.Top, Math.Min(place.Y, area.Bottom - size.Height)));

    // For a window's WndProc: while the user drags the window by its title bar, Windows
    // asks with WM_MOVING where it may go; this keeps it inside the area (its monitor's
    // free area). A window laid out for one monitor's scaling stays there: moved to a
    // monitor with another scaling, WinForms resizes its text but not the window
    // (docs/research/winforms-dpi.md). Returns true when it handled the message.
    public static bool KeepOnMonitor(ref Message m, Rectangle area)
    {
        if (m.Msg != WmMoving)
        {
            return false;
        }
        var r = Marshal.PtrToStructure<Win32Rect>(m.LParam);
        var size = new Size(r.Right - r.Left, r.Bottom - r.Top);
        var place = KeepInside(area, new Point(r.Left, r.Top), size);
        r = new Win32Rect { Left = place.X, Top = place.Y, Right = place.X + size.Width, Bottom = place.Y + size.Height };
        Marshal.StructureToPtr(r, m.LParam, false);
        // Handled: Windows moves the window to the changed rectangle.
        m.Result = 1;
        return true;
    }

    // The always-on-top flag among a window's extended styles.
    public const int WsExTopmost = 0x8;
    private const int GwlExStyle = -20;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoActivate = 0x10;
    private const int WmMoving = 0x0216;

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

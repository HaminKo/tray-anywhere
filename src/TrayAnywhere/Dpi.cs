using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// Each monitor's scaling. The app is per-monitor DPI aware and works in physical pixels,
// so its windows size themselves for the monitor they open on (150 % and 100 % here).
internal static class Dpi
{
    // A monitor's scaling: 1.0 at 100 %, 1.5 at 150 %.
    public static float ScaleOf(Screen screen)
    {
        // Any point on the monitor finds it; its top-left corner is one.
        var monitor = MonitorFromPoint(screen.Bounds.Location, MonitorDefaultToNearest);
        return GetDpiForMonitor(monitor, EffectiveDpi, out var dpi, out _) == 0 ? dpi / 96f : 1f;
    }

    private const uint MonitorDefaultToNearest = 2;
    // The DPI that the monitor's scaling setting gives (96 at 100 %).
    private const int EffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}

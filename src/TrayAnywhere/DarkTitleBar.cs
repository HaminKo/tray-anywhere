using System;
using System.Runtime.InteropServices;

namespace TrayAnywhere;

// A dark title bar for the app's windows that have one (the grid, the settings window),
// to match their dark inside. Windows draws title bars light unless asked.
internal static class DarkTitleBar
{
    public static void Use(IntPtr window)
    {
        var dark = 1;
        DwmSetWindowAttribute(window, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

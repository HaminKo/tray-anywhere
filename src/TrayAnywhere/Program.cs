using System;
using System.Threading;
using System.Windows.Forms;

namespace TrayAnywhere;

// tray-anywhere: shows the main taskbar's tray icons in a grid on the monitor the mouse
// is on. Alt+` opens and closes the grid.
internal static class Program
{
    // WinForms windows need a single-threaded COM apartment on the UI thread.
    [STAThread]
    private static void Main()
    {
        // One copy at a time: a second one couldn't register the hotkey. "Local\" makes
        // the name count per signed-in user session.
        using var oneCopy = new Mutex(initiallyOwned: true, @"Local\tray-anywhere", out var isFirstCopy);
        if (!isFirstCopy)
        {
            // Say so: the app has no window of its own, so starting it again would
            // otherwise seem to do nothing.
            MessageBox.Show($"tray-anywhere is already running. {App.HotkeyName} opens its grid.", "tray-anywhere");
            return;
        }

        // The monitors have different scaling. Per-monitor aware, Windows gives this
        // program physical pixels (as UI Automation does) and lets each window draw at
        // its own monitor's scale instead of blurring it.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var app = new App();
        if (!app.HotkeyRegistered)
        {
            MessageBox.Show(
                $"{App.HotkeyName} is already used by another program, so tray-anywhere can't open its grid.",
                "tray-anywhere");
            return;
        }
        Application.Run(app);
    }
}

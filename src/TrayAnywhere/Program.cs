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
        // Windows' "monitors changed" event (see App) calls the app through the UI thread's
        // synchronization context as it is when the app subscribes, or else on a thread of
        // its own. WinForms makes that context with the first window or in Application.Run,
        // and with no secondary monitor the app has no window yet, so it is made here.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        var settingsPath = Settings.DefaultPath;
        var settings = Settings.Load(settingsPath, out var settingsProblem);
        // Said before the app starts: once it runs, its grid and buttons (always on top)
        // could open over this message while it waits for an answer.
        if (settingsProblem != null)
        {
            MessageBox.Show(
                $"tray-anywhere couldn't read its settings file:\n{settingsPath}\n\n{settingsProblem}\n\n" +
                "It starts with the buttons and the grid in their usual places, and won't change the file until it can read it.",
                "tray-anywhere");
        }
        using var app = new App(settings);
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

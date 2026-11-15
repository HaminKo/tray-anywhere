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
    private static void Main(string[] args)
    {
        // No arguments, or for the GUI test (src/GuiTest), so it never changes the user's
        // settings or autostart: "--settings PATH" for another settings file, and
        // "--run-key KEY" for another registry key than Windows' Run key (see Autostart).
        // "--gui-test" lets it run beside the user's copy: a lock of its own, and the
        // hotkey left to the user's copy (the test sends the hotkey's message itself).
        // Anything else is refused, so a mistyped option can't fall back to the user's.
        var settingsPath = Settings.DefaultPath;
        var runKey = Autostart.WindowsRunKey;
        var guiTest = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--gui-test")
            {
                guiTest = true;
            }
            else if (i + 1 < args.Length && args[i] == "--settings")
            {
                settingsPath = args[i + 1];
                i++;
            }
            else if (i + 1 < args.Length && args[i] == "--run-key")
            {
                runKey = args[i + 1];
                i++;
            }
            else
            {
                MessageBox.Show("tray-anywhere takes no arguments, or for its GUI test --gui-test, --settings and a file path, and --run-key and a registry key.", "tray-anywhere");
                return;
            }
        }

        // One copy at a time: a second one couldn't register the hotkey. "Local\" makes
        // the name count per signed-in user session. The GUI test's copy, which leaves the
        // hotkey alone, has a lock of its own: it runs beside the user's copy, but not
        // beside another test's, which would share its settings file and Run key.
        var lockName = guiTest ? @"Local\tray-anywhere-gui" : @"Local\tray-anywhere";
        using var oneCopy = new Mutex(initiallyOwned: true, lockName, out var isFirstCopy);
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
        using var app = new App(settings, new Autostart(runKey), useHotkey: !guiTest);
        if (app.HotkeyTaken)
        {
            MessageBox.Show(
                $"{App.HotkeyName} is already used by another program, so tray-anywhere can't open its grid.",
                "tray-anywhere");
            return;
        }
        Application.Run(app);
    }
}

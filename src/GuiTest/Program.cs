using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;
using static GuiTest.Checks;

namespace GuiTest;

// The settings file as the app writes it (src/TrayAnywhere/Settings.cs): places measured
// from each monitor's top-left corner, and each monitor's button look, by the monitor's
// name (DISPLAY5). The icon order and which monitors have a button, which the app also
// keeps there, are read on their own (SavedOrderLength, SavedButtonsOff, SavedMainButtonOn).
internal sealed record SettingsFile(Dictionary<string, Offset> ButtonPlaces, Dictionary<string, Offset> GridPlaces, Dictionary<string, Look> ButtonLooks);

internal sealed record Offset(int X, int Y);

// A floating button's look as the file has it: its color and size by name ("Accent",
// "Large"), and whether it stays on top.
internal sealed record Look(string Color, string Size, bool OnTop);

// Where the test dragged its icon's tile: to the first or the last place of this section
// ("Pinned" or "Overflow").
internal sealed record DraggedIcon(string Section, bool Last)
{
    public string PlaceName => Last ? "last" : "first";
}

// The GUI test: starts the real app with a settings file of its own, works it as a user
// would, and checks what it shows. With "screenshots" it takes pictures of the grid
// instead (Screenshots.cs). It works the app through messages to the app's own
// windows and UI Automation, never the real mouse or keyboard, so the user can keep
// working meanwhile. Exit code 0 when every check passes; at the first that fails it
// stops, with exit code 1. The sizes, the settings file's form and the monitor names are
// written here again, not shared with the app, so the test checks the app against what
// it should do rather than against its own code.
internal static class Program
{
    // Sizes at 100 % scaling, in pixels, as in the app: between the grid and its button,
    // and between the grid or a button and the edge of the screen.
    private const int ButtonGap = 8;
    private const int ScreenGap = 12;

    // No arguments: the test. "screenshots", optionally with a monitor's name (DISPLAY5):
    // the pictures, of each secondary monitor or of that one.
    [STAThread]
    private static int Main(string[] args)
    {
        // Physical pixels, as the app works in. Otherwise Windows would scale what this
        // program sees on a monitor with other scaling than the main one.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var screenshots = args.Length > 0 && args[0].Equals("screenshots", StringComparison.OrdinalIgnoreCase);
        if (args.Length > (screenshots ? 2 : 0))
        {
            Console.WriteLine("Use: dotnet run --project src/GuiTest [-- screenshots [DISPLAYn]]");
            return 1;
        }
        // One test copy of the app at a time. One already running is left alone: it may be
        // another test's, still at work.
        if (TestedApp.IsRunning(TestedApp.LockName))
        {
            Console.WriteLine(@"A test copy of tray-anywhere is running: another GUI test, or one stopped early. Wait for it, or end that copy (TrayAnywhere.exe under src\GuiTest\bin).");
            return 1;
        }
        // The test runs beside the user's copy and never touches it. Screenshots copy the
        // screen, where the user's buttons or grid could show, so they need it closed.
        var userCopy = TestedApp.IsRunning(TestedApp.UserLockName);
        if (userCopy && screenshots)
        {
            Console.WriteLine("tray-anywhere is running and could show in the pictures. Exit it first (right-click its button: Exit), then take them again.");
            return 1;
        }
        if (userCopy)
        {
            Console.WriteLine("note  tray-anywhere is running: the test runs beside it");
        }
        var monitors = Screen.AllScreens.Where(monitor => !monitor.Primary).ToList();
        if (monitors.Count == 0)
        {
            Console.WriteLine("The GUI test needs a secondary monitor: the floating buttons are only there.");
            return 1;
        }
        if (args.Length == 2)
        {
            var names = string.Join(", ", monitors.Select(Key));
            monitors = monitors.FindAll(monitor => Key(monitor).Equals(args[1], StringComparison.OrdinalIgnoreCase));
            if (monitors.Count == 0)
            {
                Console.WriteLine($"No floating button on {args[1]}: the secondary monitors are {names}.");
                return 1;
            }
        }
        // The test's folder in the temp folder. Besides the screenshots, the one file
        // written: the app's settings for the test, never the user's. In the registry, the
        // one key written is the test's own (TestedApp.TestKey); a run that failed may
        // have left it.
        Registry.CurrentUser.DeleteSubKeyTree(TestedApp.TestKey, throwOnMissingSubKey: false);
        var folder = Path.Combine(Path.GetTempPath(), "tray-anywhere-gui");
        Directory.CreateDirectory(folder);
        var settingsPath = Path.Combine(folder, "settings.json");

        var watch = Stopwatch.StartNew();
        try
        {
            if (screenshots)
            {
                Screenshots.Take(folder, settingsPath, monitors);
            }
            else
            {
                var first = monitors[0];
                // The first monitor's button in the top half of its monitor, as if the user
                // had dragged it there: its grid opens below it.
                var savedPlace = new Point(first.WorkingArea.X + first.WorkingArea.Width / 2, first.WorkingArea.Y + Px(first, 100));
                WriteSettings(settingsPath, new(new() { [Key(first)] = ToOffset(first, savedPlace) }, new(), new()));
                var (draggedPlace, draggedIcon) = FirstRun(settingsPath, monitors, savedPlace);
                IconOrderAfterRestart(settingsPath, monitors[0], draggedIcon);
                AfterRestart(settingsPath, monitors, draggedPlace);
                SettingsWindow(settingsPath, monitors);
                ButtonsInSettings(settingsPath, monitors);
                ButtonLookInSettings(settingsPath, monitors);
                HotkeyInSettings(settingsPath, monitors);
                if (userCopy)
                {
                    Check(TestedApp.IsRunning(TestedApp.UserLockName), "the user's tray-anywhere still runs", "it has stopped");
                }
            }
        }
        catch (CheckFailed e)
        {
            Console.WriteLine($"FAILED: {e.Message}");
            return 1;
        }
        catch (Exception e)
        {
            // A mistake in the test itself, caught so the app is still ended (TestedApp.Dispose).
            Console.WriteLine($"FAILED, an error in the test: {e}");
            return 1;
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestedApp.TestKey, throwOnMissingSubKey: false);
        }
        Console.WriteLine($"{(screenshots ? "Done" : "All checks passed")} in {watch.Elapsed.TotalSeconds:0.0} s.");
        return 0;
    }

    // The app as it starts with the saved place; returns where the first monitor's button
    // was dragged to, and where the test's icon was dragged to (null: skipped). The test
    // ends this copy of the app (TestedApp.Dispose): a menu opened by a message doesn't
    // show once the app has been in front (see AfterRestart).
    private static (Point DraggedPlace, DraggedIcon? DraggedIcon) FirstRun(string settingsPath, List<Screen> monitors, Point savedPlace)
    {
        using var app = new TestedApp(settingsPath);
        var buttons = CheckButtons(app, monitors, savedPlace);
        ToastToMouse();
        OpenAndClose(app, buttons, monitors);
        var (grid, draggedIcon) = TestIconInGrid(app, buttons[0], monitors[0]);
        var draggedPlace = DragGrid(grid, buttons[0], monitors[0], settingsPath);
        CloseGrid(app, grid, buttons[0], monitors[0]);
        GearOpensSettings(app, buttons[0], monitors[0]);
        return (draggedPlace, draggedIcon);
    }

    // The settings gear at the grid's top right, above the icons and named "Settings": a
    // click (UI Automation's Invoke) closes the grid and opens the settings window in the
    // middle of that monitor, with that monitor picked. Last in its copy of the app: once
    // the settings window has shown, the test's message no longer opens the app's menu
    // (see SettingsWindow).
    private static void GearOpensSettings(TestedApp app, AppWindow button, Screen monitor)
    {
        button.Click();
        var grid = WaitForGrid(app, monitor, "the button opens the grid again");
        var element = WaitFor("it has a settings gear", () => grid.ControlWithId("Gear"));
        var gear = new AppWindow(element);
        Check(element.Current.Name == "Settings", "named Settings", $"named \"{element.Current.Name}\"");
        var tiles = TestedApp.Tiles(grid);
        var bounds = gear.Bounds;
        var size = new Size(Px(monitor, 24), Px(monitor, 24));
        Check(bounds.Size == size && tiles.All(tile => bounds.Bottom <= tile.Bounds.Top && bounds.Right >= tile.Bounds.Right),
            $"{size.Width} px square, above the icons, at their right", $"it is at {bounds}");
        AppWindow.Invoke(element);
        WaitUntil("a click on it closes the grid", () => app.Grid() == null);
        var window = WaitFor($"and opens the settings window on {Key(monitor)}", () => app.SettingsWindow() is AppWindow open && IsOn(open, monitor) ? open : null);
        app.GiveFrontBack();
        CheckInMiddle(window, monitor);
        var name = MonitorName(monitor, monitor);
        WaitUntil($"with {name} picked", () => PickedName(window) == name, found: () => PickedName(window) ?? "nothing");
    }

    // Started again, the app shows the test's icon where it was dragged, not where Windows
    // puts it. The icon is there before the app starts, so the grid's read at opening has
    // it, or else the next refresh. This copy is ended by the test, as FirstRun's.
    private static void IconOrderAfterRestart(string settingsPath, Screen monitor, DraggedIcon? dragged)
    {
        if (dragged == null)
        {
            Skipped("the dragged icon order after a restart: the test's icon wasn't dragged");
            return;
        }
        using var icon = new TestIcon();
        using var app = new TestedApp(settingsPath);
        var button = WaitFor($"the app starts again, with its button on {Key(monitor)}", () => app.Buttons().Find(b => IsOn(b, monitor)));
        button.Click();
        var grid = WaitForGrid(app, monitor, "the button opens the grid");
        WaitUntil($"the test's icon is {dragged.PlaceName} in {dragged.Section}, where it was dragged before the restart",
            () => IsTestIconAt(grid, dragged), seconds: 15, found: () => string.Join(", ", SectionTiles(grid, dragged.Section).Select(tile => tile.Name)));
    }

    // A notification popup goes to the monitor the mouse is on. The test's own tray icon
    // shows a balloon, which Windows shows as a toast in one window of its shell, at the
    // bottom right of the main monitor. With the mouse on a secondary monitor, the app
    // moves that window to the bottom right of that monitor's free area, further left by
    // the scales' ratio (docs/research/notifications.md); with the mouse on the main
    // monitor, it stays. The user's copy, if it runs, may be the one that moves it: both
    // move it the same way. Skipped while Windows shows no popup: a full-screen program,
    // Do Not Disturb, or notifications turned off for the test's icon. Those are Windows'
    // doing, not the app's.
    private static void ToastToMouse()
    {
        const string what = "a notification goes to the monitor the mouse is on";
        if (Win32.SHQueryUserNotificationState(out var state) != 0 || state != Win32.QunsAcceptsNotifications)
        {
            Skipped($"{what}: Windows shows no notifications now (a full-screen program or Do Not Disturb; state {state})");
            return;
        }
        var toast = Win32.FindWindow(Win32.ToastClass, Win32.ToastTitle);
        Check(toast != IntPtr.Zero, $"Windows' notification window is there (class {Win32.ToastClass}, title \"{Win32.ToastTitle}\")", "not found");
        var mouse = Screen.FromPoint(Cursor.Position);
        using var icon = new TestIcon();
        icon.ShowBalloon("A notification for the GUI test");
        var bounds = Rectangle.Empty;
        try
        {
            WaitUntil("a balloon from the test's tray icon shows as a notification", () => (bounds = Win32.Bounds(toast)).Height > 0);
        }
        catch (CheckFailed)
        {
            Skipped($"{what}: Windows showed no popup for the balloon (Do Not Disturb, or notifications off for the test's icon)");
            return;
        }
        if (!Screen.FromPoint(Cursor.Position).Equals(mouse))
        {
            Skipped($"{what}: the mouse went to another monitor meanwhile");
            return;
        }
        var main = Screen.PrimaryScreen!;
        var area = mouse.WorkingArea;
        var width = (int)Math.Round(bounds.Width * Win32.ScaleOf(mouse) / Win32.ScaleOf(main));
        var expected = new Point(area.Right - width, area.Bottom - bounds.Height);
        WaitUntil($"{what} ({Key(mouse)})", () => Win32.Bounds(toast).Location == expected, seconds: 2, () => $"at {Win32.Bounds(toast)}, expected {expected}");
    }

    // Started again, the button is where it was dragged, Reset icon order in its
    // right-click menu works, and Exit there ends the app. Before any grid opens: once
    // the app has been in front and lost it, WinForms doesn't show a menu opened by a
    // message, though a real right-click works (docs/research/gui-testing.md).
    private static void AfterRestart(string settingsPath, List<Screen> monitors, Point draggedPlace)
    {
        using var app = new TestedApp(settingsPath);
        var buttons = CheckButtons(app, monitors, draggedPlace);
        ResetIconOrder(app, buttons[0], settingsPath);
        buttons[0].RightClick();
        var exit = WaitFor("the button's right-click menu opens, with Exit", () => app.MenuItem("Exit tray-anywhere"));
        AppWindow.Invoke(exit);
        WaitUntil("Exit ends the app", () => app.HasExited);
        Check(app.ExitCode == 0, "with exit code 0", $"exit code {app.ExitCode}");
    }

    // Start with Windows, clicked in the settings window three times, in the test's own
    // Run and Startup apps keys. The Run key starts out with another copy's exe (a build
    // folder, say), which counts as off; the first click points it at the tested app's
    // exe. Then a "turned off" as Task Manager's Startup apps writes it counts as off too;
    // the second click deletes it. Then a "turned on" from Startup apps counts as on; the
    // third click deletes it and the Run value. Windows' own keys stay as they were. Each
    // click in a copy of the app started again for it: once the settings window has shown,
    // the app no longer opens a menu from the test's message, so "Settings..." works only
    // once per copy (measured; the user's real right-click isn't affected, see AfterRestart).
    // The window opens on the first secondary monitor, then on the last.
    private static void SettingsWindow(string settingsPath, List<Screen> monitors)
    {
        var windowsValues = (TestedApp.RunValue(TestedApp.WindowsKey), TestedApp.StartupAppsValue(TestedApp.WindowsKey));
        using (var key = Registry.CurrentUser.CreateSubKey(TestedApp.TestKey + TestedApp.RunKey))
        {
            key.SetValue("tray-anywhere", @"""C:\another copy\TrayAnywhere.exe""");
        }
        // In quotes, as Windows needs a path that may hold a space.
        var command = $"\"{TestedApp.Exe}\"";
        var last = monitors[^1];
        using (var app = new TestedApp(settingsPath))
        {
            var window = ClickStartWithWindows(app, monitors[0], "Start with Windows is off while the Run key holds another copy's exe",
                ticked: false, "a click on it writes the app's exe to the Run key", expectedRun: command);
            Check(!window.IsOnTop, "the settings window isn't always on top", "it is");
            window.PressEscape();
            WaitUntil("Escape closes it", () => app.SettingsWindow() == null);
            // Windows may hand the front to one of the app's buttons.
            app.GiveFrontBack();
        }
        // 1 (turned off) and when, as a FILETIME: what Task Manager wrote on the owner's PC
        // (docs/research/startup-apps.md).
        SetStartupAppsValue([1, 0, 0, 0, .. BitConverter.GetBytes(DateTime.Now.ToFileTime())]);
        using (var app = new TestedApp(settingsPath))
        {
            ClickStartWithWindows(app, last, "Start with Windows is off while Startup apps says turned off",
                ticked: false, "a second click deletes that, and the Run value stays", expectedRun: command);
        }
        // 2 (turned on), then 8 zero bytes: what Task Manager writes when turned on again.
        SetStartupAppsValue([2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        using (var app = new TestedApp(settingsPath))
        {
            ClickStartWithWindows(app, last, "Start with Windows is on while Startup apps says turned on",
                ticked: true, "a third click deletes that and the Run value", expectedRun: null);
        }
        Check((TestedApp.RunValue(TestedApp.WindowsKey), TestedApp.StartupAppsValue(TestedApp.WindowsKey)) == windowsValues,
            "Windows' own Run and Startup apps values are unchanged", "they changed");
    }

    // "Reset icon order" in the button's right-click menu empties the saved order of both
    // sections in settings.json: the tray's order again.
    private static void ResetIconOrder(TestedApp app, AppWindow button, string settingsPath)
    {
        button.RightClick();
        var item = WaitFor("the button's right-click menu opens, with Reset icon order", () => app.MenuItem("Reset icon order"));
        AppWindow.Invoke(item);
        WaitUntil("a click on it empties the saved icon order", () => SavedOrderLength(settingsPath) == 0,
            found: () => $"{SavedOrderLength(settingsPath)} keys saved");
        WaitUntil("the menu closes", () => app.MenuItem("Reset icon order") == null);
    }

    // "Show the floating button" in the settings window, for the monitor picked in its
    // list, in three copies of the app (one window per copy, see SettingsWindow). M is the
    // secondary monitor the mouse is on (else the first), as the hotkey opens the grid
    // on the mouse's monitor. Copy 1, the window opened from M's button: it lists the
    // monitors by name with M picked and ticked; the main monitor, picked, is unticked
    // (off by default), and ticked it gets a button in its corner; M, picked again and
    // unticked, loses its button, and only M; settings.json keeps both. Copy 2, started
    // again: those buttons; the hotkey opens the grid in M's corner (with the mouse
    // there), and the grid opens next to the main monitor's button. Copy 3, the window
    // opened from the main monitor's button: the main monitor picked and ticked; unticked
    // its button closes; M ticked brings M's button back at its old place, and
    // settings.json has none off. All ended by the test.
    private static void ButtonsInSettings(string settingsPath, List<Screen> monitors)
    {
        var main = Screen.PrimaryScreen!;
        var monitor = MouseMonitor(monitors);
        var others = monitors.FindAll(m => m.DeviceName != monitor.DeviceName);
        Point oldPlace;
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, monitor);
            CheckMonitorList(window, monitor);
            var box = WaitFor("it has Show the floating button", () => window.Control("Show the floating button"));
            Check(AppWindow.IsTicked(box), $"ticked for {Key(monitor)}, which has a button", "it isn't");
            oldPlace = app.Buttons().Find(b => IsOn(b, monitor))!.Bounds.Location;

            PickMonitor(window, main, monitor);
            Check(!AppWindow.IsTicked(box), "unticked for the main monitor, off by default", "it is ticked");
            AppWindow.Toggle(box);
            var mainButton = WaitFor("ticked, it shows a button on the main monitor", () => app.Buttons().Find(b => IsOn(b, main)));
            var corner = Corner(main, mainButton.Bounds.Size);
            Check(mainButton.Bounds.Location == corner, "in its corner above the clock", $"it is at {mainButton.Bounds.Location}, the corner at {corner}");
            Check(SavedMainButtonOn(settingsPath), "settings.json keeps the main monitor's button on", "it doesn't");

            PickMonitor(window, monitor, monitor);
            Check(AppWindow.IsTicked(box), $"ticked again for {Key(monitor)}", "it isn't");
            AppWindow.Toggle(box);
            WaitUntil($"unticked, it closes the button on {Key(monitor)}, and only that one",
                () => ButtonsOn(app, [main, .. others]), found: () => ButtonMonitors(app));
            var off = SavedButtonsOff(settingsPath);
            Check(off.SequenceEqual([Key(monitor)]), $"settings.json lists {Key(monitor)} as off", $"it lists [{string.Join(", ", off)}]");
        }
        using (var app = new TestedApp(settingsPath))
        {
            WaitUntil($"started again, the app shows buttons on the main monitor and on each secondary one but {Key(monitor)}",
                () => ButtonsOn(app, [main, .. others]), found: () => ButtonMonitors(app));
            var mouse = Screen.FromPoint(Cursor.Position);
            app.PressHotkey();
            var grid = WaitForGrid(app, mouse, $"the hotkey opens the grid on the mouse's monitor, {Key(mouse)}");
            if (mouse.DeviceName == monitor.DeviceName)
            {
                CheckInCorner(grid, monitor);
            }
            else
            {
                Skipped($"the grid in the corner of {Key(monitor)}, which has no button: the mouse is on {Key(mouse)}");
            }
            // On the main monitor the hotkey's grid is at its button already; elsewhere a
            // click on that button moves the grid there (a second click would close it).
            var mainButton = app.Buttons().Find(b => IsOn(b, main))!;
            if (!mouse.Primary)
            {
                mainButton.Click();
                grid = WaitForGrid(app, main, "a click on the main monitor's button moves the grid there");
            }
            CheckNextToButton(grid, mainButton, main, below: false);
        }
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, main);
            CheckMonitorList(window, main);
            var box = WaitFor("it has Show the floating button", () => window.Control("Show the floating button"));
            Check(AppWindow.IsTicked(box), "ticked for the main monitor", "it isn't");
            AppWindow.Toggle(box);
            WaitUntil("unticked, it closes the main monitor's button", () => ButtonsOn(app, others), found: () => ButtonMonitors(app));

            PickMonitor(window, monitor, main);
            Check(!AppWindow.IsTicked(box), $"unticked for {Key(monitor)}", "it is ticked");
            AppWindow.Toggle(box);
            var button = WaitFor($"ticked, it shows the button on {Key(monitor)} again", () => app.Buttons().Find(b => IsOn(b, monitor)));
            Check(button.Bounds.Location == oldPlace, "at its old place", $"it is at {button.Bounds.Location}, before at {oldPlace}");
            WaitUntil("settings.json lists no monitor as off and the main monitor's button off",
                () => SavedButtonsOff(settingsPath).Count == 0 && !SavedMainButtonOn(settingsPath));
        }
    }

    // The secondary monitor the mouse is on, else the first.
    private static Screen MouseMonitor(List<Screen> monitors)
    {
        var mouse = Screen.FromPoint(Cursor.Position);
        return monitors.Find(m => m.DeviceName == mouse.DeviceName) ?? monitors[0];
    }

    // The color, size and always on top of M's button in the settings window (M as in
    // ButtonsInSettings, which leaves a button on each secondary monitor), in two copies
    // of the app. Copy 1: the window shows M's defaults (Dark, Small, on top) and the
    // button has them; Windows' accent color, Large and Always on top unticked each make
    // M's button again with the change, at its place; the other buttons stay as they
    // were; settings.json keeps the change. Copy 2, started again: the window and the
    // button still have them, and a click on the bigger button opens the grid next to it;
    // back to the defaults, the open grid opens again next to the small button, and
    // settings.json keeps the defaults. All ended by the test.
    private static void ButtonLookInSettings(string settingsPath, List<Screen> monitors)
    {
        var monitor = MouseMonitor(monitors);
        var others = monitors.FindAll(m => m.DeviceName != monitor.DeviceName);
        var dark = Color.FromArgb(32, 32, 32);
        var accent = AccentColor();
        var small = new Size(Px(monitor, 32), Px(monitor, 32));
        var large = new Size(Px(monitor, 48), Px(monitor, 48));
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, monitor);
            CheckLookShown(window, "Dark", "Small", onTop: true, $"for {Key(monitor)} it shows the defaults: Dark, Small, Always on top");
            WaitForButton(app, monitor, "and its button has them", dark, small, onTop: true);

            Pick(window, "Windows' accent color");
            WaitForButton(app, monitor, $"Windows' accent color ({accent.R}, {accent.G}, {accent.B}) makes it again in that color", accent, small, onTop: true);
            Pick(window, "Large");
            var button = WaitForButton(app, monitor, "Large makes it again, bigger", accent, large, onTop: true);
            CheckButtonPlace(button, monitor, settingsPath);
            AppWindow.Toggle(ControlNamed(window, "Always on top"));
            WaitForButton(app, monitor, "Always on top unticked makes it again, not on top", accent, large, onTop: false);
            if (others.Count > 0)
            {
                Check(others.All(m => app.Buttons().Exists(b => IsOn(b, m) && b.IsOnTop && b.Bounds.Size == new Size(Px(m, 32), Px(m, 32)))),
                    "the other monitors' buttons are still small and on top", ButtonMonitors(app));
            }
            CheckSavedLook(settingsPath, monitor, new Look("Accent", "Large", false));
        }
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, monitor);
            CheckLookShown(window, "Windows' accent color", "Large", onTop: false, "started again, the window shows them");
            var button = WaitForButton(app, monitor, "and the button has them", accent, large, onTop: false);
            button.Click();
            var grid = WaitForGrid(app, monitor, "a click on it opens the grid");
            // M's button is in the bottom half of its monitor: in its corner, or where
            // DragGrid dragged it.
            CheckNextToButton(grid, button, monitor, below: false);

            Pick(window, "Dark");
            WaitForButton(app, monitor, "Dark makes it again, dark", dark, large, onTop: false);
            Pick(window, "Small");
            WaitForButton(app, monitor, "Small makes it again, small", dark, small, onTop: false);
            AppWindow.Toggle(ControlNamed(window, "Always on top"));
            button = WaitForButton(app, monitor, "Always on top ticked makes it again, on top", dark, small, onTop: true);
            // Each change closes the grid and opens it again; the last opening is at this button.
            grid = WaitForGrid(app, monitor, "the grid opens again");
            CheckNextToButton(grid, button, monitor, below: false);
            CheckSavedLook(settingsPath, monitor, new Look("Dark", "Small", true));
        }
    }

    // The hotkey in the settings window, in two copies of the app. The test's copy never
    // takes the keys (TestedApp), so the window and settings.json are checked here, and the
    // real keys are the owner's to test. Copy 1: the box shows Alt+`, Default greyed; a
    // click on the box waits for keys; A alone isn't allowed; Escape keeps Alt+`; F9 alone
    // becomes the hotkey, and settings.json keeps it. Copy 2, after the test wrote
    // Ctrl+Alt+Space into settings.json (keys with Ctrl or Alt can't be sent, see
    // AppWindow.PressKey): the box shows them; Default brings back Alt+`, greyed, and
    // settings.json holds no hotkey. Both ended by the test.
    private static void HotkeyInSettings(string settingsPath, List<Screen> monitors)
    {
        var monitor = MouseMonitor(monitors);
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, monitor);
            var box = window.ControlWithId("Hotkey") ?? throw new CheckFailed("the settings window has no hotkey box");
            var defaultButton = ControlNamed(window, "Default");
            Check(box.Current.Name == "Alt+`" && !defaultButton.Current.IsEnabled && NoteOf(window) == IdleNote,
                "the hotkey box shows Alt+`, Default is greyed, and the note says how the box works",
                $"it shows {box.Current.Name}, Default {(defaultButton.Current.IsEnabled ? "not greyed" : "greyed")}, the note says \"{NoteOf(window)}\"");
            AppWindow.Invoke(box);
            WaitForNote(window, "Press the new keys, or Escape to keep these.", "a click on it waits for new keys");
            window.PressKey(Win32.VkA);
            WaitForNote(window, "Use Ctrl, Alt or Win with that key.", "A alone isn't allowed");
            window.PressEscape();
            WaitForNote(window, IdleNote, "Escape ends the wait");
            Check(app.SettingsWindow() != null && box.Current.Name == "Alt+`", "the window stays open, with Alt+`",
                $"open: {app.SettingsWindow() != null}, the box shows {box.Current.Name}");
            Check(SavedHotkey(settingsPath) == null, "settings.json holds no hotkey", $"it holds {SavedHotkey(settingsPath)}");

            AppWindow.Invoke(box);
            WaitForNote(window, "Press the new keys, or Escape to keep these.", "a second click waits again");
            window.PressKey(Win32.VkF9);
            WaitUntil("F9 alone becomes the hotkey", () => box.Current.Name == "F9" && NoteOf(window) == IdleNote,
                found: () => $"the box shows {box.Current.Name}, the note says \"{NoteOf(window)}\"");
            Check(defaultButton.Current.IsEnabled, "Default isn't greyed now", "it is");
            WaitUntil("settings.json keeps F9", () => SavedHotkey(settingsPath) == "None F9", found: () => SavedHotkey(settingsPath) ?? "no hotkey");
        }
        WriteHotkey(settingsPath, "Alt, Ctrl", "Space");
        using (var app = new TestedApp(settingsPath))
        {
            var window = OpenSettingsWindow(app, monitor);
            var box = window.ControlWithId("Hotkey") ?? throw new CheckFailed("the settings window has no hotkey box");
            var defaultButton = ControlNamed(window, "Default");
            Check(box.Current.Name == "Ctrl+Alt+Space", "started with Ctrl+Alt+Space in settings.json, the box shows them", $"it shows {box.Current.Name}");
            AppWindow.Invoke(defaultButton);
            WaitUntil("Default brings back Alt+`, and is greyed", () => box.Current.Name == "Alt+`" && !defaultButton.Current.IsEnabled,
                found: () => $"the box shows {box.Current.Name}");
            WaitUntil("settings.json holds no hotkey", () => SavedHotkey(settingsPath) == null, found: () => SavedHotkey(settingsPath) ?? "");
        }
    }

    // The line under the hotkey box while it doesn't wait for keys.
    private const string IdleNote = "Click the keys to change them.";

    // The line under the hotkey box, or null if the window has none.
    private static string? NoteOf(AppWindow window) => window.ControlWithId("HotkeyNote")?.Current.Name;

    private static void WaitForNote(AppWindow window, string note, string what) =>
        WaitUntil(what, () => NoteOf(window) == note, found: () => $"the note says \"{NoteOf(window)}\"");

    // The settings window shows this color, size and always on top for the picked monitor.
    private static void CheckLookShown(AppWindow window, string color, string size, bool onTop, string what)
    {
        var colorPicked = AppWindow.IsSelected(ControlNamed(window, color));
        var sizePicked = AppWindow.IsSelected(ControlNamed(window, size));
        var onTopTicked = AppWindow.IsTicked(ControlNamed(window, "Always on top"));
        Check(colorPicked && sizePicked && onTopTicked == onTop, what,
            $"{color} picked: {colorPicked}, {size} picked: {sizePicked}, Always on top ticked: {onTopTicked}");
    }

    // Picks the color or size with this name in the settings window.
    private static void Pick(AppWindow window, string name) => AppWindow.Select(ControlNamed(window, name));

    private static AutomationElement ControlNamed(AppWindow window, string name) =>
        window.Control(name) ?? throw new CheckFailed($"the settings window has no {name}");

    // Waits for the button on this monitor to have this color, size and always on top: the
    // app makes the button again at each change, so it is looked for again each time. A
    // failure lists every button there, to tell an old button left open from a new one
    // drawn wrong.
    private static AppWindow WaitForButton(TestedApp app, Screen monitor, string what, Color color, Size size, bool onTop)
    {
        AppWindow? found = null;
        WaitUntil(what, () => (found = app.Buttons().Find(b => IsOn(b, monitor) && b.Bounds.Size == size && b.IsOnTop == onTop && HasColor(b, color))) != null,
            found: () => "buttons there: [" + string.Join("; ", app.Buttons().Where(b => IsOn(b, monitor)).Select(b =>
                $"{b.Bounds.Width} × {b.Bounds.Height}, {(b.IsOnTop ? "" : "not ")}on top, color {b.ColorAt(ColorPoint(b, monitor))?.ToString() ?? "unknown"}")) + "]");
        return found!;
    }

    // The button's color, read in the middle of its top edge, away from the arrow and the
    // round corners, within 2 per channel. Under the mouse it is 15 % of the way to white.
    private static bool HasColor(AppWindow button, Color color)
    {
        if (button.ColorAt(ColorPoint(button, Screen.FromRectangle(button.Bounds))) is not Color found)
        {
            return false;
        }
        static bool Near(Color a, Color b) => Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2;
        static int Lighter(int value) => value + (int)Math.Round((255 - value) * 0.15);
        return Near(found, color) || Near(found, Color.FromArgb(Lighter(color.R), Lighter(color.G), Lighter(color.B)));
    }

    private static Point ColorPoint(AppWindow button, Screen monitor) => new(button.Bounds.Width / 2, Px(monitor, 4));

    // Windows' accent color as Settings > Personalization > Colors keeps it: AccentColor in
    // DWM's key, red in the lowest byte (measured, To do 17 step D). Without it the app
    // uses Blue, Windows' default accent.
    private static Color AccentColor() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr
            ? Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF)
            : Color.FromArgb(0, 120, 212);

    // A button made again keeps its place: where it was dragged (settings.json), or else
    // its corner for its new size.
    private static void CheckButtonPlace(AppWindow button, Screen monitor, string settingsPath)
    {
        var saved = ReadSettings(settingsPath).ButtonPlaces.GetValueOrDefault(Key(monitor));
        var bounds = button.Bounds;
        var expected = saved == null ? Corner(monitor, bounds.Size) : new Point(monitor.Bounds.X + saved.X, monitor.Bounds.Y + saved.Y);
        Check(bounds.Location == expected, saved == null ? "in its corner above the clock" : "at its dragged place",
            $"it is at {bounds.Location}, expected {expected}");
    }

    private static void CheckSavedLook(string settingsPath, Screen monitor, Look expected)
    {
        var saved = ReadSettings(settingsPath).ButtonLooks.GetValueOrDefault(Key(monitor));
        Check(saved == expected, $"settings.json keeps {expected.Color}, {expected.Size}, {(expected.OnTop ? "" : "not ")}on top for {Key(monitor)}",
            $"it has {saved?.ToString() ?? "none"}");
    }

    // True when the app shows a button on each of these monitors and on no other.
    private static bool ButtonsOn(TestedApp app, List<Screen> monitors)
    {
        var buttons = app.Buttons();
        return buttons.Count == monitors.Count && monitors.All(m => buttons.Exists(b => IsOn(b, m)));
    }

    // The settings window draws every monitor as a box, a radio button named "Main
    // monitor", or for the others by their place from the left ("External monitor"; "Left
    // monitor" and "Right monitor"; or "Monitor 1 from the left" and on), each with its size
    // in pixels, and "(this one)" for its own monitor, which is picked; the picked name also
    // shows under the boxes.
    private static void CheckMonitorList(AppWindow window, Screen here)
    {
        var named = MonitorNames(here);
        var expected = named.Select(n => n.Name).Order().ToList();
        var boxes = window.RadioButtons("Monitors");
        var names = boxes.Select(box => box.Current.Name).Order().ToList();
        Check(names.SequenceEqual(expected), $"it has a box for each monitor: {string.Join("; ", expected)}", $"it has {string.Join("; ", names)}");
        CheckOutline(named.ConvertAll(n => (n.Monitor, new AppWindow(boxes.Find(box => box.Current.Name == n.Name)!).Bounds)), here);
        var picked = boxes.Where(AppWindow.IsSelected).Select(box => box.Current.Name).ToList();
        var name = MonitorName(here, here);
        Check(picked.SequenceEqual([name]), $"{name} is picked", $"picked: [{string.Join("; ", picked)}]");
        Check(PickedName(window) == name, "its name shows under the boxes", $"it shows {PickedName(window) ?? "nothing"}");
    }

    // The boxes are drawn like the monitors in Windows' Display settings: the desktop
    // shrunk to one scale, each box at its monitor's place and size, within a few pixels
    // (the gap between boxes, rounding). The scale is read from the drawing's width.
    private static void CheckOutline(List<(Screen Monitor, Rectangle Box)> boxes, Screen here)
    {
        var desktop = boxes.Select(b => b.Monitor.Bounds).Aggregate(Rectangle.Union);
        var drawn = boxes.Select(b => b.Box).Aggregate(Rectangle.Union);
        var scale = (double)drawn.Width / desktop.Width;
        var tolerance = Px(here, 6);
        var off = new List<string>();
        foreach (var (monitor, box) in boxes)
        {
            var m = monitor.Bounds;
            var expected = new[] { (m.Left - desktop.Left) * scale, (m.Top - desktop.Top) * scale, m.Width * scale, m.Height * scale };
            var found = new[] { box.Left - drawn.Left, box.Top - drawn.Top, box.Width, box.Height };
            if (expected.Zip(found).Any(pair => Math.Abs(pair.First - pair.Second) > tolerance))
            {
                off.Add($"{Key(monitor)} at {m.Location}, {m.Width} × {m.Height}: its box at {box.Left - drawn.Left},{box.Top - drawn.Top}, {box.Width} × {box.Height}");
            }
        }
        Check(off.Count == 0, $"each box is at its monitor's place and size at one scale ({scale:0.0000}), within {tolerance} px", string.Join("; ", off));
    }

    // The monitor's name under the boxes in the settings window, or null.
    private static string? PickedName(AppWindow window) => window.ControlWithId("PickedMonitor")?.Current.Name;

    // Picks this monitor in the settings window opened on `here`.
    private static void PickMonitor(AppWindow window, Screen monitor, Screen here)
    {
        var name = MonitorName(monitor, here);
        var box = window.RadioButtons("Monitors").Find(r => r.Current.Name == name);
        Check(box != null, $"the window has a box for {name}", "it hasn't");
        AppWindow.Select(box);
        WaitUntil($"{name} picked, its name under the boxes", () => AppWindow.IsSelected(box) && PickedName(window) == name,
            found: () => $"picked: {AppWindow.IsSelected(box)}, the name shows {PickedName(window) ?? "nothing"}");
    }

    // The monitors as the settings window on `here` names them, in its order.
    private static List<(Screen Monitor, string Name)> MonitorNames(Screen here)
    {
        var all = Screen.AllScreens;
        var others = all.Where(m => !m.Primary).OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
        var places = all.Where(m => m.Primary).Select(m => (Monitor: m, Place: "Main monitor")).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var place = others.Count switch
            {
                1 => "External monitor",
                2 => i == 0 ? "Left monitor" : "Right monitor",
                _ => $"Monitor {i + 1} from the left",
            };
            places.Add((others[i], place));
        }
        var named = new List<(Screen Monitor, string Name)>();
        foreach (var (monitor, place) in places)
        {
            var thisOne = monitor.DeviceName == here.DeviceName ? " (this one)" : "";
            named.Add((monitor, $"{place}, {monitor.Bounds.Width} × {monitor.Bounds.Height}{thisOne}"));
        }
        return named;
    }

    private static string MonitorName(Screen monitor, Screen here) =>
        MonitorNames(here).Find(named => named.Monitor.DeviceName == monitor.DeviceName).Name;

    // The monitors the app shows a button on, for a failed check's message.
    private static string ButtonMonitors(TestedApp app) =>
        $"buttons on [{string.Join(", ", app.Buttons().Select(b => Key(Screen.FromRectangle(b.Bounds))))}]";

    // Writes the app's value in the test's Startup apps key.
    private static void SetStartupAppsValue(byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(TestedApp.TestKey + TestedApp.StartupAppsKey);
        key.SetValue("tray-anywhere", value, RegistryValueKind.Binary);
    }

    // In a freshly started app: opens the settings window from the right-click menu of
    // the button on this monitor and checks it is in the middle of the monitor. Then
    // checks the tick of Start with Windows, clicks it, and waits for the Run value to
    // become expectedRun (null: none) and for no Startup apps value.
    private static AppWindow ClickStartWithWindows(TestedApp app, Screen monitor, string tickCheck, bool ticked, string clickCheck, string? expectedRun)
    {
        var window = OpenSettingsWindow(app, monitor);
        var box = WaitFor("it has Start with Windows", () => window.Control("Start with Windows"));
        Check(AppWindow.IsTicked(box) == ticked, tickCheck, ticked ? "it isn't ticked" : "it is ticked");
        AppWindow.Toggle(box);
        WaitUntil(clickCheck,
            () => TestedApp.RunValue(TestedApp.TestKey) == expectedRun && TestedApp.StartupAppsValue(TestedApp.TestKey) == null,
            found: () => $"the Run value is {TestedApp.RunValue(TestedApp.TestKey) ?? "missing"}, the Startup apps value {TestedApp.StartupAppsValue(TestedApp.TestKey) ?? "missing"}");
        return window;
    }

    // In a freshly started app (see SettingsWindow): "Settings..." in the right-click menu
    // of the button on this monitor opens the settings window, in the middle of the monitor.
    private static AppWindow OpenSettingsWindow(TestedApp app, Screen monitor)
    {
        var button = WaitFor($"started again, the app shows its button on {Key(monitor)}", () => app.Buttons().Find(b => IsOn(b, monitor)));
        button.RightClick();
        var item = WaitFor("the button's right-click menu opens, with Settings...", () => app.MenuItem("Settings..."));
        AppWindow.Invoke(item);
        // The menu may close only after the click has run (as WinForms' code is
        // remembered; not measured): a menu found before that would be the old one.
        WaitUntil("the menu closes", () => app.MenuItem("Settings...") == null);
        var window = WaitFor($"the settings window shows on {Key(monitor)}", () => app.SettingsWindow() is AppWindow open && IsOn(open, monitor) ? open : null);
        app.GiveFrontBack();
        CheckInMiddle(window, monitor);
        return window;
    }

    // The settings window in the middle of its monitor's free area.
    private static void CheckInMiddle(AppWindow window, Screen monitor)
    {
        var bounds = window.Bounds;
        var area = monitor.WorkingArea;
        var middle = new Point(area.X + (area.Width - bounds.Width) / 2, area.Y + (area.Height - bounds.Height) / 2);
        Check(bounds.Location == middle, "it is in the middle of its monitor", $"it is at {bounds.Location}, the middle at {middle}");
    }

    // One floating button per secondary monitor, always on top: on the first monitor at
    // firstPlace, on the others in the corner above the clock. Returns them in the order
    // of monitors.
    private static List<AppWindow> CheckButtons(TestedApp app, List<Screen> monitors, Point firstPlace)
    {
        WaitUntil($"the app shows a floating button on each of the {monitors.Count} secondary monitors", () => app.Buttons().Count == monitors.Count);
        var all = app.Buttons();
        var buttons = new List<AppWindow>();
        foreach (var monitor in monitors)
        {
            var button = all.Find(b => IsOn(b, monitor));
            Check(button != null, $"one of them is on {Key(monitor)}", "none is");
            buttons.Add(button);
        }
        Check(buttons.All(b => b.IsOnTop), "they are always on top", "one isn't");
        var place = buttons[0].Bounds.Location;
        Check(place == firstPlace, $"the button on {Key(monitors[0])} is at its saved place", $"it is at {place}, saved {firstPlace}");
        for (var i = 1; i < monitors.Count; i++)
        {
            var bounds = buttons[i].Bounds;
            var corner = Corner(monitors[i], bounds.Size);
            Check(bounds.Location == corner, $"the button on {Key(monitors[i])} is in its corner", $"it is at {bounds.Location}, the corner at {corner}");
        }
        return buttons;
    }

    // A click on a button opens the grid next to it; a click on another monitor's button
    // moves the grid there; a second click closes it. The hotkey opens it on the monitor
    // the mouse is on, and closes it again.
    private static void OpenAndClose(TestedApp app, List<AppWindow> buttons, List<Screen> monitors)
    {
        buttons[0].Click();
        var grid = WaitForGrid(app, monitors[0], $"a click on the button opens the grid on {Key(monitors[0])}");
        CheckNextToButton(grid, buttons[0], monitors[0], below: true);
        Check(grid.IsOnTop, "the grid is always on top", "it isn't");
        CheckTiles(grid);

        var last = buttons[0];
        if (monitors.Count > 1)
        {
            buttons[1].Click();
            grid = WaitForGrid(app, monitors[1], $"a click on the button on {Key(monitors[1])} moves the grid there");
            CheckNextToButton(grid, buttons[1], monitors[1], below: false);
            last = buttons[1];
        }
        else
        {
            Skipped("the grid moving to another monitor's button: there is only one secondary monitor");
        }
        last.Click();
        WaitUntil("a second click on its button closes the grid", () => app.Grid() == null);

        var mouse = Screen.FromPoint(Cursor.Position);
        app.PressHotkey();
        grid = WaitForGrid(app, mouse, $"the hotkey opens the grid on the mouse's monitor, {Key(mouse)}");
        var index = monitors.FindIndex(monitor => monitor.DeviceName == mouse.DeviceName);
        if (index < 0)
        {
            // The main monitor: no button there.
            CheckInCorner(grid, mouse);
        }
        else
        {
            CheckNextToButton(grid, buttons[index], mouse, below: index == 0);
        }
        app.PressHotkey();
        WaitUntil("the hotkey again closes it", () => app.Grid() == null);
    }

    // The grid in the bottom-right corner of its monitor's free area: where it opens on a
    // monitor without a button, until it is dragged there.
    private static void CheckInCorner(AppWindow grid, Screen monitor)
    {
        var bounds = grid.Bounds;
        var corner = Corner(monitor, bounds.Size);
        Check(bounds.Location == corner, "there in the bottom-right corner", $"it is at {bounds.Location}, the corner at {corner}");
    }

    // Each tile has a name (its tooltip text), and the Pinned ones come before the
    // Overflow ones.
    private static void CheckTiles(AppWindow grid)
    {
        var tiles = WaitFor("it shows icons", () =>
        {
            var found = TestedApp.Tiles(grid);
            return found.Count > 0 ? found : null;
        });
        var pinned = tiles.TakeWhile(tile => tile.Id == "Pinned").Count();
        var overflow = tiles.Skip(pinned).Count(tile => tile.Id == "Overflow");
        Check(pinned + overflow == tiles.Count, "they are in Pinned, then Overflow", string.Join(", ", tiles.Select(tile => tile.Id)));
        Check(tiles.All(tile => tile.Name.Trim() != ""), $"each of the {pinned} pinned and {overflow} overflow icons has a name", "one has none");
    }

    // The test's own tray icon shows in the grid at a refresh, a left and then a right
    // click on its tile reach it as such, the window like Steam's menu that the right
    // click opens far away is moved to the tile, the grid stays open, a drag moves the tile
    // (DragTestIcon), and the tile goes when the icon does. Returns the open grid and where
    // the tile was dragged to (null: skipped). With the mouse elsewhere, the
    // grid reads the tray only every 10 s, so the icon shows about 10 s after the grid
    // opened rather than within 2 s: checked, unless the test saw the mouse over the grid.
    private static (AppWindow, DraggedIcon?) TestIconInGrid(TestedApp app, AppWindow button, Screen monitor)
    {
        button.Click();
        var grid = WaitForGrid(app, monitor, "the button opens the grid again");
        var watch = Stopwatch.StartNew();
        var mouseOver = false;
        DraggedIcon? dragged;
        using (var icon = new TestIcon())
        {
            var tile = WaitFor("the test's own tray icon shows in the grid at a refresh", () =>
            {
                // What the app counts as the grid being used.
                mouseOver |= grid.Bounds.Contains(Cursor.Position);
                return TestIconTile(grid);
            }, seconds: 15);
            var waited = watch.Elapsed.TotalSeconds;
            Console.WriteLine($"      (in {tile.Id}, after {waited:0.0} s)");
            if (mouseOver)
            {
                Skipped("an unused grid reads the tray only every 10 s: the mouse was over the grid");
            }
            else
            {
                // 5 s: halfway between the two rates, well clear of both.
                Check(waited > 5, "an unused grid reads the tray only every 10 s, not every 2 s", $"the icon showed after {waited:0.0} s");
            }
            tile.Invoke();
            WaitUntil("a left click on its tile reaches it as a left click", () => icon.Clicks.SequenceEqual([MouseButtons.Left]));
            // Where Steam opens its menu on the owner's 150 % monitor: 1280 px right of and
            // 433 px above the click (docs/research/uia-tray.md "Where Steam's menu opens"),
            // at its size at 100 % scaling.
            var t = tile.Bounds;
            var tileCentre = new Point(t.X + t.Width / 2, t.Y + t.Height / 2);
            icon.MenuPlace = new Rectangle(tileCentre.X + 1280, tileCentre.Y - 433, Px(monitor, 248), Px(monitor, 456));
            // The app puts a menu at the mouse instead, if it is over the grid when the app
            // takes the click: looked at before and after.
            var mouseOnGrid = grid.Bounds.Contains(Cursor.Position);
            tile.RightClick();
            WaitUntil("a right click on its tile reaches it as a right click", () => icon.Clicks.SequenceEqual([MouseButtons.Left, MouseButtons.Right]));
            mouseOnGrid |= grid.Bounds.Contains(Cursor.Position);
            CheckSteamMenuMoved(icon, monitor, mouseOnGrid ? null : tileCentre);
            Check(grid.IsOpen, "the grid stays open after clicks on its icons", "it closed");
            (grid, dragged) = DragTestIcon(app, grid, button, monitor, icon);
        }
        watch.Restart();
        WaitUntil("the tile goes when the icon does", () => TestIconTile(grid) == null, seconds: 15);
        Console.WriteLine($"      (after {watch.Elapsed.TotalSeconds:0.0} s)");
        return (grid, dragged);
    }

    // A drag of the test's icon onto the tile at the other end of its section puts it
    // there, at that tile's place, without clicking the icon: onto the last tile if it is
    // first (Windows put a new icon first in the overflow, measured in To do 14), else onto
    // the first. It is still there in the grid opened again (a new read, sorted by the
    // saved order). Returns that grid and where the icon went; null if it is the only icon
    // in its section.
    private static (AppWindow, DraggedIcon?) DragTestIcon(TestedApp app, AppWindow grid, AppWindow button, Screen monitor, TestIcon icon)
    {
        // Closed first: while the test's window like Steam's menu was open, UI Automation
        // timed out reading the grid's tiles (docs/research/gui-testing.md).
        icon.CloseMenu();
        // Found again: a refresh may have laid the grid out anew since.
        var tile = WaitFor("the test's icon is in the grid", () => TestIconTile(grid));
        var tiles = SectionTiles(grid, tile.Id);
        if (tiles.Count < 2)
        {
            Skipped($"a drag of the test's icon in {tile.Id}: it is the only icon there");
            return (grid, null);
        }
        var dragged = new DraggedIcon(tile.Id, Last: tiles[0].Name.Contains(TestIcon.Name));
        var target = (dragged.Last ? tiles[^1] : tiles[0]).Bounds;
        var clicks = icon.Clicks.Count;
        tile.MouseDrag(new Point(target.X + target.Width / 2, target.Y + target.Height / 2));
        // By name and place, not by window: a refresh may lay the grid out anew.
        WaitUntil($"a drag onto the {dragged.PlaceName} tile in {dragged.Section} puts the test's icon there, at that tile's place",
            () => IsTestIconAt(grid, dragged) && TestIconTile(grid)?.Bounds == target,
            found: () => $"the test's icon at {TestIconTile(grid)?.Bounds}, the {dragged.PlaceName} place {target}: " +
                string.Join(", ", SectionTiles(grid, dragged.Section).Select(t => t.Name)));
        button.Click();
        WaitUntil("a click on its button closes the grid", () => app.Grid() == null);
        button.Click();
        grid = WaitForGrid(app, monitor, "a second one opens it again, with a new read");
        WaitUntil($"the test's icon is still {dragged.PlaceName} in {dragged.Section}", () => IsTestIconAt(grid, dragged),
            found: () => string.Join(", ", SectionTiles(grid, dragged.Section).Select(t => t.Name)));
        Check(icon.Clicks.Count == clicks, "the drag didn't click the test's icon", $"it got {icon.Clicks.Count - clicks} more clicks");
        return (grid, dragged);
    }

    // The tiles of one section, "Pinned" or "Overflow", in their order.
    private static List<AppWindow> SectionTiles(AppWindow grid, string section) =>
        TestedApp.Tiles(grid).FindAll(tile => tile.Id == section);

    // True if the test's icon is at the first or last place of its section, as dragged.
    private static bool IsTestIconAt(AppWindow grid, DraggedIcon dragged)
    {
        var tiles = SectionTiles(grid, dragged.Section);
        var tile = dragged.Last ? tiles.LastOrDefault() : tiles.FirstOrDefault();
        return tile?.Name.Contains(TestIcon.Name) == true;
    }

    // The app moves the test's window like Steam's menu from far away to the click: a
    // corner at the tile's centre (null: the mouse was over the grid, so the app took the
    // mouse instead; left to the user's test), the window inside the monitor's free area.
    private static void CheckSteamMenuMoved(TestIcon icon, Screen monitor, Point? tileCentre)
    {
        var menu = icon.Menu;
        Check(menu != null, "the right click opens the test's window like Steam's menu, far away", "it didn't");
        var area = monitor.WorkingArea;
        if (tileCentre is Point centre)
        {
            WaitUntil("the app moves it to the tile: a corner at the tile's centre, inside the monitor's free area",
                () => Corners(menu.Bounds).Contains(centre) && area.Contains(menu.Bounds),
                found: () => $"it is at {menu.Bounds}, the tile's centre at {centre}, the free area {area}");
        }
        else
        {
            Skipped("the window like Steam's menu at the tile: the mouse was over the grid, so the app puts it at the mouse");
            WaitUntil("the app moves it inside the monitor's free area",
                () => menu.Bounds.Location != icon.MenuPlace.Location && area.Contains(menu.Bounds),
                found: () => $"it is at {menu.Bounds}, the free area {area}");
        }
    }

    private static Point[] Corners(Rectangle r) =>
        [new(r.Left, r.Top), new(r.Right, r.Top), new(r.Left, r.Bottom), new(r.Right, r.Bottom)];

    private static AppWindow? TestIconTile(AppWindow grid) =>
        TestedApp.Tiles(grid).Find(tile => tile.Name.Contains(TestIcon.Name));

    // A drag of the grid by its title bar into the bottom half of its monitor: its button
    // comes along just below it, right edges lined up, and the button's new place is
    // saved. Returns that place.
    private static Point DragGrid(AppWindow grid, AppWindow button, Screen monitor, string settingsPath)
    {
        var place = new Point(grid.Bounds.X, monitor.WorkingArea.Bottom - Px(monitor, 100) - grid.Bounds.Height);
        var gap = Px(monitor, ButtonGap);
        grid.Drag(place);
        // Both measured after the drag: when an icon comes or goes, a refresh lays the
        // grid out again at another size.
        WaitUntil("a dragged grid takes its button along, below it", () =>
            {
                var g = grid.Bounds;
                var b = button.Bounds;
                return b.Right == g.Right && b.Top == g.Bottom + gap;
            },
            found: () => $"the button at {button.Bounds}, the grid at {grid.Bounds}, dragged to {place}");
        Check(grid.Bounds.Location == place, "the grid stays where it was dragged", $"it is at {grid.Bounds.Location}, dragged to {place}");
        var buttonPlace = button.Bounds.Location;
        var saved = ReadSettings(settingsPath).ButtonPlaces.GetValueOrDefault(Key(monitor));
        var offset = ToOffset(monitor, buttonPlace);
        Check(saved == offset, "the button's new place is saved in settings.json", $"saved {saved}, the button at {offset}");
        return buttonPlace;
    }

    // Escape closes the grid; opened again, it is above the dragged button now, and its X
    // closes it.
    private static void CloseGrid(TestedApp app, AppWindow grid, AppWindow button, Screen monitor)
    {
        grid.PressEscape();
        WaitUntil("Escape closes the grid", () => app.Grid() == null);
        button.Click();
        grid = WaitForGrid(app, monitor, "the button opens the grid again");
        CheckNextToButton(grid, button, monitor, below: false);
        grid.PressClose();
        WaitUntil("its X closes it", () => app.Grid() == null);
    }

    // The grid next to its button, where the button's arrow points: right edges lined up,
    // just below the button or just above it.
    private static void CheckNextToButton(AppWindow grid, AppWindow button, Screen monitor, bool below)
    {
        var g = grid.Bounds;
        var b = button.Bounds;
        var gap = Px(monitor, ButtonGap);
        var expected = new Point(b.Right - g.Width, below ? b.Bottom + gap : b.Top - gap - g.Height);
        Check(g.Location == expected, $"it is {(below ? "below" : "above")} the button, right edges lined up", $"the grid at {g}, the button at {b}");
    }

    // Waits for the grid to open on this monitor, then gives the front back to the user's
    // window if the grid took it.
    internal static AppWindow WaitForGrid(TestedApp app, Screen monitor, string what)
    {
        var grid = WaitFor(what, () => app.Grid() is AppWindow open && IsOn(open, monitor) ? open : null);
        app.GiveFrontBack();
        return grid;
    }

    internal static bool IsOn(AppWindow window, Screen monitor) =>
        Screen.FromRectangle(window.Bounds).DeviceName == monitor.DeviceName;

    // Where a window of this size starts on a monitor: the bottom-right corner of its free
    // area, just above the taskbar.
    private static Point Corner(Screen monitor, Size size)
    {
        var area = monitor.WorkingArea;
        var gap = Px(monitor, ScreenGap);
        return new Point(area.Right - gap - size.Width, area.Bottom - gap - size.Height);
    }

    // A size at 100 % scaling in this monitor's pixels, rounded as the app rounds it.
    private static int Px(Screen monitor, int size) => (int)Math.Round(size * Win32.ScaleOf(monitor));

    // The monitor's name as the settings file has it: "\\.\DISPLAY5" becomes "DISPLAY5".
    internal static string Key(Screen monitor) => monitor.DeviceName.TrimStart('\\', '.');

    private static Offset ToOffset(Screen monitor, Point place) => new(place.X - monitor.Bounds.X, place.Y - monitor.Bounds.Y);

    internal static void WriteSettings(string path, SettingsFile settings) =>
        File.WriteAllText(path, JsonSerializer.Serialize(settings));

    private static SettingsFile ReadSettings(string path) =>
        JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path)) ?? throw new CheckFailed("settings.json holds null");

    // The monitors settings.json lists with their button off. None listed: none off.
    private static List<string> SavedButtonsOff(string path)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        return file.RootElement.TryGetProperty("ButtonsOff", out var names)
            ? names.EnumerateArray().Select(name => name.GetString() ?? "null").ToList()
            : [];
    }

    // Whether settings.json has the main monitor's button on. Not in the file: off.
    private static bool SavedMainButtonOn(string path)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        return file.RootElement.TryGetProperty("MainButtonOn", out var on) && on.GetBoolean();
    }

    // The hotkey settings.json keeps, as "modifiers key" ("Alt, Ctrl Space"), or null when
    // it has none (or null): the default.
    private static string? SavedHotkey(string path)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        return file.RootElement.TryGetProperty("Hotkey", out var keys) && keys.ValueKind != JsonValueKind.Null
            ? $"{keys.GetProperty("Modifiers").GetString()} {keys.GetProperty("Key").GetString()}"
            : null;
    }

    // Writes these keys into settings.json as the hotkey, by name as the app writes them,
    // and keeps the rest of the file.
    private static void WriteHotkey(string path, string modifiers, string key)
    {
        var file = JsonNode.Parse(File.ReadAllText(path)) ?? throw new CheckFailed("settings.json holds null");
        file["Hotkey"] = new JsonObject { ["Modifiers"] = modifiers, ["Key"] = key };
        File.WriteAllText(path, file.ToJsonString());
    }

    // The number of keys in the icon order settings.json keeps for both sections. A list
    // the file doesn't have counts as empty, as the app reads it.
    private static int SavedOrderLength(string path)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var length = 0;
        foreach (var name in new[] { "PinnedOrder", "OverflowOrder" })
        {
            if (file.RootElement.TryGetProperty(name, out var keys))
            {
                length += keys.GetArrayLength();
            }
        }
        return length;
    }
}

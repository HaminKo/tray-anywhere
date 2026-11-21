using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;
using static GuiTest.Checks;

namespace GuiTest;

// The settings file as the app writes it (src/TrayAnywhere/Settings.cs): places measured
// from each monitor's top-left corner, by the monitor's name (DISPLAY5). The icon order
// and the monitors whose button is off, which the app also keeps there, are read on their
// own (SavedOrderLength, SavedButtonsOff).
internal sealed record SettingsFile(Dictionary<string, Offset> ButtonPlaces, Dictionary<string, Offset> GridPlaces);

internal sealed record Offset(int X, int Y);

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
                WriteSettings(settingsPath, new(new() { [Key(first)] = ToOffset(first, savedPlace) }, new()));
                var (draggedPlace, draggedIcon) = FirstRun(settingsPath, monitors, savedPlace);
                IconOrderAfterRestart(settingsPath, monitors[0], draggedIcon);
                AfterRestart(settingsPath, monitors, draggedPlace);
                ButtonOff(settingsPath, monitors);
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
        return (draggedPlace, draggedIcon);
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

    // Started again, the button is where it was dragged, Start with Windows in its
    // right-click menu turns on and off, and Exit there ends the app. Before any grid
    // opens: once the app has been in front and lost it, WinForms doesn't show a menu
    // opened by a message, though a real right-click works (docs/research/gui-testing.md).
    private static void AfterRestart(string settingsPath, List<Screen> monitors, Point draggedPlace)
    {
        using var app = new TestedApp(settingsPath);
        var buttons = CheckButtons(app, monitors, draggedPlace);
        StartWithWindows(app, buttons[0]);
        ResetIconOrder(app, buttons[0], settingsPath);
        buttons[0].RightClick();
        var exit = WaitFor("the button's right-click menu opens, with Exit", () => app.MenuItem("Exit tray-anywhere"));
        AppWindow.Invoke(exit);
        WaitUntil("Exit ends the app", () => app.HasExited);
        Check(app.ExitCode == 0, "with exit code 0", $"exit code {app.ExitCode}");
    }

    // Start with Windows, clicked three times in the button's right-click menu, in the
    // test's own Run and Startup apps keys. The Run key starts out with another copy's exe
    // (a build folder, say), which counts as off; the first click points it at the tested
    // app's exe. Then a "turned off" as Task Manager's Startup apps writes it counts as off
    // too; the second click deletes it. Then a "turned on" from Startup apps counts as on;
    // the third click deletes it and the Run value. Windows' own keys stay as they were.
    private static void StartWithWindows(TestedApp app, AppWindow button)
    {
        var windowsValues = (TestedApp.RunValue(TestedApp.WindowsKey), TestedApp.StartupAppsValue(TestedApp.WindowsKey));
        using (var key = Registry.CurrentUser.CreateSubKey(TestedApp.TestKey + TestedApp.RunKey))
        {
            key.SetValue("tray-anywhere", @"""C:\another copy\TrayAnywhere.exe""");
        }
        // In quotes, as Windows needs a path that may hold a space.
        var command = $"\"{TestedApp.Exe}\"";
        ClickStartWithWindows(app, button, "Start with Windows is off while the Run key holds another copy's exe",
            ticked: false, "a click on it writes the app's exe to the Run key", expectedRun: command);
        // 1 (turned off) and when, as a FILETIME: what Task Manager wrote on the owner's PC
        // (docs/research/startup-apps.md).
        SetStartupAppsValue([1, 0, 0, 0, .. BitConverter.GetBytes(DateTime.Now.ToFileTime())]);
        ClickStartWithWindows(app, button, "Start with Windows is off while Startup apps says turned off",
            ticked: false, "a second click deletes that, and the Run value stays", expectedRun: command);
        // 2 (turned on), then 8 zero bytes: what Task Manager writes when turned on again.
        SetStartupAppsValue([2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        ClickStartWithWindows(app, button, "Start with Windows is on while Startup apps says turned on",
            ticked: true, "a third click deletes that and the Run value", expectedRun: null);
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

    // "Floating button on this monitor", unticked in a button's right-click menu, closes
    // that button and keeps its monitor in settings.json; the others stay. Started again,
    // the app shows no button there, and the hotkey opens the grid there in the corner, as
    // on the main monitor. The hotkey opens it on the mouse's monitor, so the button
    // turned off is the one there, if the mouse is on a secondary monitor. Ticking it
    // again is in the grid's menu, which the test can't open (see AfterRestart). Both
    // copies are ended by the test.
    private static void ButtonOff(string settingsPath, List<Screen> monitors)
    {
        var mouseAtStart = Screen.FromPoint(Cursor.Position);
        var monitor = monitors.Find(m => m.DeviceName == mouseAtStart.DeviceName) ?? monitors[0];
        using (var app = new TestedApp(settingsPath))
        {
            WaitUntil($"started again, the app shows its {monitors.Count} buttons", () => app.Buttons().Count == monitors.Count);
            var button = app.Buttons().Find(b => IsOn(b, monitor));
            Check(button != null, $"one of them is on {Key(monitor)}", "none is");
            button.RightClick();
            var item = WaitFor("its right-click menu opens, with Floating button on this monitor", () => app.MenuItem("Floating button on this monitor"));
            Check(AppWindow.IsTicked(item), "it is ticked", "it isn't");
            AppWindow.Invoke(item);
            WaitUntil($"a click on it closes the button on {Key(monitor)}, and only that one",
                () => AllButtonsBut(app, monitors, monitor), found: () => ButtonMonitors(app));
            var off = SavedButtonsOff(settingsPath);
            Check(off.SequenceEqual([Key(monitor)]), $"settings.json lists {Key(monitor)} as off", $"it lists [{string.Join(", ", off)}]");
        }
        using (var app = new TestedApp(settingsPath))
        {
            // With one secondary monitor there is no button left to wait for: the grid
            // opened by the hotkey shows the app has started, buttons made.
            WaitUntil("started again", () => app.HotkeyWindow() != IntPtr.Zero);
            var mouse = Screen.FromPoint(Cursor.Position);
            app.PressHotkey();
            var grid = WaitForGrid(app, mouse, $"the hotkey opens the grid on the mouse's monitor, {Key(mouse)}");
            WaitUntil($"the app shows no button on {Key(monitor)}, and one on each other secondary monitor",
                () => AllButtonsBut(app, monitors, monitor), found: () => ButtonMonitors(app));
            if (mouse.DeviceName == monitor.DeviceName)
            {
                CheckInCorner(grid, monitor);
            }
            else
            {
                Skipped($"the grid in the corner of {Key(monitor)}, which has no button: the mouse is on {Key(mouse)}");
            }
        }
    }

    // True when the app shows a button on each secondary monitor but this one.
    private static bool AllButtonsBut(TestedApp app, List<Screen> monitors, Screen monitor)
    {
        var buttons = app.Buttons();
        return buttons.Count == monitors.Count - 1 && !buttons.Exists(b => IsOn(b, monitor));
    }

    // The monitors the app shows a button on, for a failed check's message.
    private static string ButtonMonitors(TestedApp app) =>
        $"buttons on [{string.Join(", ", app.Buttons().Select(b => Key(Screen.FromRectangle(b.Bounds))))}]";

    // Writes the app's value in the test's Startup apps key.
    private static void SetStartupAppsValue(byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(TestedApp.TestKey + TestedApp.StartupAppsKey);
        key.SetValue("tray-anywhere", value, RegistryValueKind.Binary);
    }

    // Opens the button's menu, checks the tick, clicks Start with Windows, and waits for
    // the Run value to become expectedRun (null: none) and for no Startup apps value. The
    // menu closes at each click and is opened again, so its tick is seen as it is when it
    // opens.
    private static void ClickStartWithWindows(TestedApp app, AppWindow button, string tickCheck, bool ticked, string clickCheck, string? expectedRun)
    {
        button.RightClick();
        var item = WaitFor("the button's right-click menu opens, with Start with Windows", () => app.MenuItem("Start with Windows"));
        Check(AppWindow.IsTicked(item) == ticked, tickCheck, ticked ? "it isn't ticked" : "it is ticked");
        AppWindow.Invoke(item);
        WaitUntil(clickCheck,
            () => TestedApp.RunValue(TestedApp.TestKey) == expectedRun && TestedApp.StartupAppsValue(TestedApp.TestKey) == null,
            found: () => $"the Run value is {TestedApp.RunValue(TestedApp.TestKey) ?? "missing"}, the Startup apps value {TestedApp.StartupAppsValue(TestedApp.TestKey) ?? "missing"}");
        // The menu may close only after the click has run (as WinForms' code is
        // remembered; not measured): a menu found before that would be the old one.
        WaitUntil("the menu closes", () => app.MenuItem("Start with Windows") == null);
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

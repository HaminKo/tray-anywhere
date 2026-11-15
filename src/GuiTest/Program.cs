using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using static GuiTest.Checks;

namespace GuiTest;

// The settings file as the app writes it (src/TrayAnywhere/Settings.cs): places measured
// from each monitor's top-left corner, by the monitor's name (DISPLAY5).
internal sealed record SettingsFile(Dictionary<string, Offset> ButtonPlaces, Dictionary<string, Offset> GridPlaces);

internal sealed record Offset(int X, int Y);

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
        // The app runs one copy at a time (its mutex, in src/TrayAnywhere/Program.cs).
        if (Mutex.TryOpenExisting(@"Local\tray-anywhere", out var running))
        {
            running.Dispose();
            Console.WriteLine("tray-anywhere is running. Exit it first (right-click its button: Exit), then run the test again.");
            return 1;
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
                var draggedPlace = FirstRun(settingsPath, monitors, savedPlace);
                AfterRestart(settingsPath, monitors, draggedPlace);
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
    // was dragged to. The test ends this copy of the app (TestedApp.Dispose): a menu
    // opened by a message doesn't show once the app has been in front (see AfterRestart).
    private static Point FirstRun(string settingsPath, List<Screen> monitors, Point savedPlace)
    {
        using var app = new TestedApp(settingsPath);
        var buttons = CheckButtons(app, monitors, savedPlace);
        OpenAndClose(app, buttons, monitors);
        var grid = TestIconInGrid(app, buttons[0], monitors[0]);
        var draggedPlace = DragGrid(grid, buttons[0], monitors[0], settingsPath);
        CloseGrid(app, grid, buttons[0], monitors[0]);
        return draggedPlace;
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
        buttons[0].RightClick();
        var exit = WaitFor("the button's right-click menu opens, with Exit", () => app.MenuItem("Exit tray-anywhere"));
        AppWindow.Invoke(exit);
        WaitUntil("Exit ends the app", () => app.HasExited);
        Check(app.ExitCode == 0, "with exit code 0", $"exit code {app.ExitCode}");
    }

    // Start with Windows, clicked twice in the button's right-click menu. The test's Run
    // key starts out with another copy's exe (a build folder, say), which counts as off;
    // the first click points it at the tested app's exe, the second deletes it. The menu
    // closes at each click and is opened again, so its tick is seen as it is when it
    // opens. Windows' own Run key stays as it was.
    private static void StartWithWindows(TestedApp app, AppWindow button)
    {
        var windowsValue = TestedApp.RunValue(TestedApp.WindowsRunKey);
        using (var key = Registry.CurrentUser.CreateSubKey(TestedApp.TestRunKey))
        {
            key.SetValue("tray-anywhere", @"""C:\another copy\TrayAnywhere.exe""");
        }
        // In quotes, as Windows needs a path that may hold a space.
        var command = $"\"{TestedApp.Exe}\"";
        foreach (var turnOn in new[] { true, false })
        {
            button.RightClick();
            var item = WaitFor("the button's right-click menu opens, with Start with Windows", () => app.MenuItem("Start with Windows"));
            Check(AppWindow.IsTicked(item) != turnOn,
                turnOn ? "Start with Windows is off while the Run key holds another copy's exe" : "Start with Windows is on",
                turnOn ? "it is ticked" : "it isn't ticked");
            AppWindow.Invoke(item);
            var expected = turnOn ? command : null;
            WaitUntil(turnOn ? "a click on it writes the app's exe to the Run key" : "a second click deletes it again",
                () => TestedApp.RunValue(TestedApp.TestRunKey) == expected,
                found: () => $"the value is {TestedApp.RunValue(TestedApp.TestRunKey) ?? "missing"}");
            // The menu may close only after the click has run (as WinForms' code is
            // remembered; not measured): a menu found before that would be the old one.
            WaitUntil("the menu closes", () => app.MenuItem("Start with Windows") == null);
        }
        Check(TestedApp.RunValue(TestedApp.WindowsRunKey) == windowsValue, "Windows' own Run key is unchanged", "it changed");
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
            var bounds = grid.Bounds;
            var corner = Corner(mouse, bounds.Size);
            Check(bounds.Location == corner, "there in the bottom-right corner", $"it is at {bounds.Location}, the corner at {corner}");
        }
        else
        {
            CheckNextToButton(grid, buttons[index], mouse, below: index == 0);
        }
        app.PressHotkey();
        WaitUntil("the hotkey again closes it", () => app.Grid() == null);
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
    // click on its tile reach it as such, the grid stays open, and the tile goes when the
    // icon does. Returns the open grid.
    private static AppWindow TestIconInGrid(TestedApp app, AppWindow button, Screen monitor)
    {
        button.Click();
        var grid = WaitForGrid(app, monitor, "the button opens the grid again");
        using (var icon = new TestIcon())
        {
            var tile = WaitFor("the test's own tray icon shows in the grid at a refresh", () => TestIconTile(grid), seconds: 8);
            Console.WriteLine($"      (in {tile.Id})");
            tile.Invoke();
            WaitUntil("a left click on its tile reaches it as a left click", () => icon.Clicks.SequenceEqual([MouseButtons.Left]));
            tile.RightClick();
            WaitUntil("a right click on its tile reaches it as a right click", () => icon.Clicks.SequenceEqual([MouseButtons.Left, MouseButtons.Right]));
            Check(grid.IsOpen, "the grid stays open after clicks on its icons", "it closed");
        }
        WaitUntil("the tile goes when the icon does", () => TestIconTile(grid) == null, seconds: 8);
        return grid;
    }

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
}

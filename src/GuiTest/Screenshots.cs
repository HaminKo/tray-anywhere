using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static GuiTest.Checks;
using static GuiTest.Win32;

namespace GuiTest;

// The screenshots (dotnet run --project src/GuiTest -- screenshots): on each secondary
// monitor the settings window, and the grid with its floating button as a click on the
// button shows it, one PNG each per monitor in the test's temp folder, for Claude to look
// at after a change to the look. Like the test, it works the app through messages, not
// the real mouse.
internal static class Screenshots
{
    // A plain grey for what is around the two windows (TakeOne).
    private static readonly Color Outside = Color.FromArgb(128, 128, 128);

    public static void Take(string folder, string settingsPath, List<Screen> monitors)
    {
        // No saved places: each button in its corner above the clock, the grid above it,
        // as on the first start. So every run takes the same pictures. With two or more
        // monitors, the last one's button in Windows' accent color and large, so the
        // pictures show a button in its default look and one in another.
        var looks = new Dictionary<string, Look>();
        if (monitors.Count > 1)
        {
            looks[Program.Key(monitors[^1])] = new Look("Accent", "Large", OnTop: true);
        }
        Program.WriteSettings(settingsPath, new(new(), new(), looks));
        foreach (var monitor in monitors)
        {
            TakeSettings(folder, settingsPath, monitor);
        }
        using var app = new TestedApp(settingsPath);
        foreach (var monitor in monitors)
        {
            var name = Program.Key(monitor);
            var button = WaitFor($"the floating button on {name}", () => app.Buttons().Find(b => Program.IsOn(b, monitor)));
            // A click on another monitor's button moves an open grid there.
            button.Click();
            var grid = Program.WaitForGrid(app, monitor, "a click on it opens the grid");
            WaitUntil("it shows icons", () => TestedApp.Tiles(grid).Count > 0);
            // Windows fades a window in as it opens, and the app paints the tiles a moment
            // after they exist.
            Thread.Sleep(500);
            // The title bar drawn inactive, so every run takes the same picture. The test
            // keeps the front with the user's window, and without this the grid's title bar
            // showed active in some runs and inactive in others (measured). After a real
            // click the user sees it active: a white title and a red X.
            SendMessage(grid.Handle, WmNcActivate, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(200);
            TakeOne(Path.Combine(folder, $"grid-{name}.png"), grid, button);
        }
    }

    // The settings window as "Settings..." in the menu of this monitor's button opens it,
    // in a copy of the app of its own: once the window has shown, the app opens no more
    // menus from the test's messages (see Program.SettingsWindow). It isn't always on top,
    // so the picture is taken while it is in front, as it comes after the menu.
    private static void TakeSettings(string folder, string settingsPath, Screen monitor)
    {
        using var app = new TestedApp(settingsPath);
        var name = Program.Key(monitor);
        var button = WaitFor($"the floating button on {name}", () => app.Buttons().Find(b => Program.IsOn(b, monitor)));
        button.RightClick();
        var item = WaitFor("its right-click menu opens, with Settings...", () => app.MenuItem("Settings..."));
        AppWindow.Invoke(item);
        var window = WaitFor("a click on it opens the settings window there", () => app.SettingsWindow() is AppWindow open && Program.IsOn(open, monitor) ? open : null);
        // As for the grid: the fade-in, and the title bar drawn inactive.
        Thread.Sleep(500);
        SendMessage(window.Handle, WmNcActivate, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(200);
        TakeOne(Path.Combine(folder, $"settings-{name}.png"), window);
        app.GiveFrontBack();
    }

    // A picture of what the screen shows inside the box around these windows' visible
    // frames, saved at this path.
    private static void TakeOne(string path, params AppWindow[] windows)
    {
        var frames = windows.Select(window => window.VisibleBounds).ToList();
        var box = frames.Aggregate(Rectangle.Union);
        using var picture = new Bitmap(box.Width, box.Height);
        using (var graphics = Graphics.FromImage(picture))
        {
            graphics.CopyFromScreen(box.Location, Point.Empty, box.Size);
            // Grey over the rest of the box (for the grid: the gap to its button, the strip
            // beside the narrower button), so a window behind them doesn't land in the picture.
            using var rest = new Region(new Rectangle(Point.Empty, box.Size));
            foreach (var frame in frames)
            {
                rest.Exclude(Within(box, frame));
            }
            using var grey = new SolidBrush(Outside);
            graphics.FillRegion(grey, rest);
        }
        picture.Save(path, ImageFormat.Png);
        Console.WriteLine($"saved {path}");
    }

    // A rectangle on the screen as a place in the picture of the box.
    private static Rectangle Within(Rectangle box, Rectangle screen) =>
        new(screen.X - box.X, screen.Y - box.Y, screen.Width, screen.Height);
}

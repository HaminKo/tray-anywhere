using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using static GuiTest.Checks;
using static GuiTest.Win32;

namespace GuiTest;

// The screenshots (dotnet run --project src/GuiTest -- screenshots): the grid with its
// floating button on each secondary monitor, as a click on the button shows it, one PNG
// per monitor in the test's temp folder, for Claude to look at after a change to the
// look. Like the test, it works the app through messages, not the real mouse.
internal static class Screenshots
{
    // A plain grey for what is around the two windows (TakeOne).
    private static readonly Color Outside = Color.FromArgb(128, 128, 128);

    public static void Take(string folder, string settingsPath, List<Screen> monitors)
    {
        // No saved places: each button in its corner above the clock, the grid above it,
        // as on the first start. So every run takes the same pictures.
        Program.WriteSettings(settingsPath, new(new(), new()));
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
            var path = Path.Combine(folder, $"grid-{name}.png");
            TakeOne(grid, button, path);
            Console.WriteLine($"saved {path}");
        }
    }

    // A picture of what the screen shows inside the box around the grid's visible frame
    // and its button: both are always on top, so nothing covers them.
    private static void TakeOne(AppWindow grid, AppWindow button, string path)
    {
        var gridFrame = grid.VisibleBounds;
        var buttonFrame = button.VisibleBounds;
        var box = Rectangle.Union(gridFrame, buttonFrame);
        using var picture = new Bitmap(box.Width, box.Height);
        using (var graphics = Graphics.FromImage(picture))
        {
            graphics.CopyFromScreen(box.Location, Point.Empty, box.Size);
            // Grey over the rest of the box (the gap between them, the strip beside the
            // narrower button), so a window behind them doesn't land in the picture.
            using var rest = new Region(new Rectangle(Point.Empty, box.Size));
            rest.Exclude(Within(box, gridFrame));
            rest.Exclude(Within(box, buttonFrame));
            using var grey = new SolidBrush(Outside);
            graphics.FillRegion(grey, rest);
        }
        picture.Save(path, ImageFormat.Png);
    }

    // A rectangle on the screen as a place in the picture of the box.
    private static Rectangle Within(Rectangle box, Rectangle screen) =>
        new(screen.X - box.X, screen.Y - box.Y, screen.Width, screen.Height);
}

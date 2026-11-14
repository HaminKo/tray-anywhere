using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TrayAnywhere;

// The running app. It has no main window: it waits for the hotkey and opens or closes
// the grid. The app ends with Exit in the grid's right-click menu.
internal sealed class App : ApplicationContext
{
    // Alt and the key left of 1: one hand, two neighbouring keys (owner's choice).
    public const string HotkeyName = "Alt+`";

    private readonly Hotkey hotkey = new();
    private readonly TrayReader reader = new();
    // The open grid, or null.
    private GridForm? grid;
    // Where the user last dragged the grid on each monitor, by the monitor's device name
    // (e.g. \\.\DISPLAY5). Kept while the app runs.
    private readonly Dictionary<string, Point> gridPlaces = new();
    // True while the grid waits for its tray read, so a second press doesn't open two.
    private bool opening;

    public App()
    {
        // Oemtilde: the ` and ~ key on a US keyboard.
        HotkeyRegistered = hotkey.Register(Hotkey.Alt, Keys.Oemtilde);
        hotkey.Pressed += OpenOrCloseGrid;

        // A first read at start: it warms up UI Automation (the first read is the
        // slowest), and if Explorer hasn't created the overflow window yet, the one
        // flash of the flyout happens now instead of when the grid opens. Not when the
        // app is about to quit because the hotkey is taken.
        if (HotkeyRegistered)
        {
            _ = reader.ReadAsync(openIfMissing: true);
        }
    }

    // False if another program already uses the hotkey.
    public bool HotkeyRegistered { get; }

    // async void: an event handler, so nothing waits for it.
    private async void OpenOrCloseGrid()
    {
        if (grid != null)
        {
            grid.Close();
            return;
        }
        if (opening)
        {
            return;
        }
        opening = true;
        try
        {
            // The monitor the mouse is on now, not when the read is done.
            var monitor = Screen.FromPoint(Cursor.Position);
            // Read first, then show, so the grid never shows stale icons (owner's choice).
            // openIfMissing: Explorer may have restarted since the last read.
            var state = await reader.ReadAsync(openIfMissing: true);
            grid = new GridForm(state, monitor, gridPlaces.TryGetValue(monitor.DeviceName, out var place) ? place : null);
            grid.FormClosed += (_, _) => ForgetGrid();
            grid.Show();
            grid.Activate();
        }
        finally
        {
            opening = false;
        }
    }

    // After the grid closed: keep where the user dragged it, for the next time it opens on
    // that monitor.
    private void ForgetGrid()
    {
        if (grid?.UserPlace is Point place)
        {
            gridPlaces[grid.Monitor.DeviceName] = place;
        }
        grid = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hotkey.Dispose();
        }
        base.Dispose(disposing);
    }
}

using System;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace TrayAnywhere;

// The right-click menu of the floating buttons and of the grid's background, made in
// one place so they all offer the same. Each window gets a menu of its own: a window
// disposes its menu when it closes.
internal static class AppMenu
{
    // resetIconOrder: what "Reset icon order" does.
    public static ContextMenuStrip Create(Autostart autostart, Action resetIconOrder)
    {
        var menu = new ContextMenuStrip();
        // CheckOnClick: a click turns the tick over before Click runs, and UI Automation
        // shows the tick as an on/off state (the GUI test reads it).
        var startWithWindows = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        startWithWindows.Click += (_, _) =>
        {
            try
            {
                autostart.Set(startWithWindows.Checked);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException)
            {
                // Shown once the click is done and the menu has closed, not over the open menu.
                menu.BeginInvoke(() => MessageBox.Show($"tray-anywhere couldn't change Start with Windows:\n{e.Message}", "tray-anywhere"));
            }
        };
        // Read from the registry each time the menu opens, not kept: another window's
        // menu may have changed it since, or the change above may have failed.
        menu.Opening += (_, _) => startWithWindows.Checked = autostart.IsOn;
        menu.Items.Add(startWithWindows);
        // Back to the tray's order, after icons were dragged in the grid.
        menu.Items.Add("Reset icon order", null, (_, _) => resetIconOrder());
        menu.Items.Add("Exit tray-anywhere", null, (_, _) => Application.Exit());
        return menu;
    }
}

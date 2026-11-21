using System;
using System.Threading;
using System.Windows.Forms;

namespace TrayAnywhere;

// The right-click menu of the floating buttons and of the grid's background, made in
// one place so they all offer the same. Each window gets a menu of its own: a window
// disposes its menu when it closes. The settings are in the settings window (SettingsForm).
internal static class AppMenu
{
    // openSettings: what "Settings..." does. resetIconOrder: what "Reset icon order" does.
    public static ContextMenuStrip Create(Action openSettings, Action resetIconOrder)
    {
        var menu = new ContextMenuStrip();
        // Once the menu has closed, so the settings window doesn't open while the menu
        // still holds the keyboard.
        menu.Items.Add("Settings...", null, (_, _) => Later(openSettings));
        // Back to the tray's order, after icons were dragged in the grid.
        menu.Items.Add("Reset icon order", null, (_, _) => resetIconOrder());
        menu.Items.Add("Exit tray-anywhere", null, (_, _) => Application.Exit());
        return menu;
    }

    // Runs the action after the current click, once the UI thread is free again.
    private static void Later(Action action) => SynchronizationContext.Current?.Post(_ => action(), null);
}

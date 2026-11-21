using System;
using System.Threading;
using System.Windows.Forms;

namespace TrayAnywhere;

// The right-click menu of the floating buttons and of the grid's background, made in
// one place so they all offer the same. Each window gets a menu of its own: a window
// disposes its menu when it closes. The settings of the whole app are in the settings
// window (SettingsForm); only the one for the menu's own monitor is here.
internal static class AppMenu
{
    // openSettings: what "Settings..." does. resetIconOrder: what "Reset icon order" does.
    // buttonOn and setButtonOn: whether the floating button on the menu's monitor is on,
    // and what switching it does; both null on the main monitor, which never has a button.
    public static ContextMenuStrip Create(Action openSettings, Action resetIconOrder, Func<bool>? buttonOn, Action<bool>? setButtonOn)
    {
        var menu = new ContextMenuStrip();
        if (buttonOn != null && setButtonOn != null)
        {
            // CheckOnClick: a click turns the tick over before Click runs, and UI
            // Automation shows the tick as an on/off state (the GUI test reads it).
            var button = new ToolStripMenuItem("Floating button on this monitor") { CheckOnClick = true };
            // Done once the click is over and the menu has closed: switching closes the
            // button or the grid this menu belongs to, which would dispose the menu during
            // its own click.
            button.Click += (_, _) => Later(() => setButtonOn(button.Checked));
            menu.Opening += (_, _) => button.Checked = buttonOn();
            menu.Items.Add(button);
        }
        // Also once the menu has closed, so the settings window doesn't open while the
        // menu still holds the keyboard.
        menu.Items.Add("Settings...", null, (_, _) => Later(openSettings));
        // Back to the tray's order, after icons were dragged in the grid.
        menu.Items.Add("Reset icon order", null, (_, _) => resetIconOrder());
        menu.Items.Add("Exit tray-anywhere", null, (_, _) => Application.Exit());
        return menu;
    }

    // Runs the action after the current click, once the UI thread is free again.
    private static void Later(Action action) => SynchronizationContext.Current?.Post(_ => action(), null);
}

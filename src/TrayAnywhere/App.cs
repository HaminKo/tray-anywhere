using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Tray;

namespace TrayAnywhere;

// The running app. It has no main window: it shows a floating button on each secondary
// monitor (unless turned off there; on the main one only if turned on; hidden while a
// full-screen window is in front there), opens or closes
// the grid when the hotkey is pressed or a button is clicked, reads the tray again while
// the grid is open (every 2 s while it is used, every 10 s while not), and passes clicks
// on the grid to the real tray icons. Each read shows in the
// order the user dragged the icons into (IconOrder). The app ends with Exit in the
// right-click menu of a button or of the grid.
internal sealed class App : ApplicationContext
{
    // The keys set in the settings window (Alt+` by default).
    private readonly Hotkey hotkey;
    private readonly TrayReader reader = new();
    // How old the grid's icons may get before the tray is read again, in ms: while the
    // grid is used (GridForm.InUse), and while it isn't (owner's choice, To do 8: an open
    // grid nobody looks at costs less, and catches up as soon as the mouse comes).
    private const long InUseRefresh = 2000;
    private const long UnusedRefresh = 10000;
    // Runs only while the grid is open. It only checks whether a read is due
    // (RefreshIfDue), often enough that the mouse coming to the grid brings one at once.
    private readonly Timer refreshTimer = new() { Interval = 500 };
    // When the read the grid shows started, in ms (Environment.TickCount64).
    private long lastRead;
    // Where the user dragged the buttons, the grid and its icons; saved at each drag.
    private readonly Settings settings;
    // "Start with Windows" in the settings window.
    private readonly Autostart autostart;
    // The open settings window, or null.
    private SettingsForm? settingsWindow;
    // The floating buttons, one per monitor that has one; made again when the monitors change.
    private readonly List<FloatingButton> buttons = [];
    // The open grid, or null.
    private GridForm? grid;
    // True while the grid waits for its tray read, so a second press doesn't open two.
    private bool opening;
    // Moves Steam's tray menu to the click when Steam opens it far away.
    private readonly SteamMenu steamMenu;
    // Moves Windows' notification popups to the monitor the mouse is on.
    private readonly ToastMover toastMover = new();
    // Hides each button while a full-screen window is in front on its monitor. Checked as
    // often as the grid's refresh: a button shows over a video that has just gone full
    // screen for at most 0.5 s.
    private readonly Timer fullScreenTimer = new() { Interval = 500 };

    // hotkey: made by Program, which says so first if another program has the keys. The
    // app disposes hotkey and steamMenu.
    public App(Settings settings, Autostart autostart, Hotkey hotkey, SteamMenu steamMenu)
    {
        this.settings = settings;
        this.autostart = autostart;
        this.hotkey = hotkey;
        this.steamMenu = steamMenu;
        // The monitor the mouse is on at the press, not when the read is done.
        hotkey.Pressed += () => OpenOrCloseGrid(Screen.FromPoint(Cursor.Position));
        refreshTimer.Tick += (_, _) => RefreshIfDue();

        // A first read at start: it warms up UI Automation (the first read is the
        // slowest), and if Explorer hasn't created the overflow window yet, the one
        // flash of the flyout happens now instead of when the grid opens.
        _ = reader.ReadAsync(openIfMissing: true);
        MakeButtons();
        SystemEvents.DisplaySettingsChanged += OnMonitorsChanged;
        fullScreenTimer.Tick += (_, _) => HideUnderFullScreen();
        fullScreenTimer.Start();
    }

    // The hotkey as set, e.g. "Alt+`", for the buttons' tooltips.
    private string HotkeyName => hotkey.Current.Name();

    // Opens the grid on this monitor, or closes it if it is open there. Open on another
    // monitor, it moves here: closed there, opened here (owner's choice).
    // async void: an event handler, so nothing waits for it.
    private async void OpenOrCloseGrid(Screen monitor)
    {
        if (grid != null)
        {
            var sameMonitor = grid.Monitor.DeviceName == monitor.DeviceName;
            grid.Close();
            if (sameMonitor)
            {
                return;
            }
        }
        if (opening)
        {
            return;
        }
        opening = true;
        try
        {
            // Read first, then show, so the grid never shows stale icons (owner's choice).
            // openIfMissing: Explorer may have restarted since the last read.
            lastRead = Environment.TickCount64;
            var state = await reader.ReadAsync(openIfMissing: true);
            // The monitor's button, if it has one: the grid and the button stay together, so
            // the button alone says where the grid is on its monitor (owner's choice). Only
            // on a monitor without a button (by default the main one) does the grid keep a
            // place of its own, where it was last dragged.
            var button = ButtonOf(monitor);
            grid = new GridForm(Sorted(state), monitor, button == null ? settings.GridPlace(monitor) : null, button, NewMenu(monitor));
            grid.FormClosed += (_, _) => ForgetGrid();
            grid.IconClicked += ClickIcon;
            // The gear: the settings window, and the grid closes, as with its X: it is always
            // on top and the settings window isn't, so it could cover the window. Settings
            // first: closing the grid while it is the active window would hand the front to
            // another program's window before the settings window could take it (code review).
            grid.SettingsClicked += () =>
            {
                OpenSettings(monitor);
                grid?.Close();
            };
            // Saved with the keys of icons that aren't there now, at their places.
            grid.Reordered += (section, items) => settings.SetOrder(section, IconOrder.Merge(items, settings.OrderOf(section)));
            grid.Dragged += place =>
            {
                if (button == null)
                {
                    settings.SetGridPlace(monitor, place);
                }
                else
                {
                    // The button moved along with the grid.
                    settings.SetButtonPlace(monitor, button.Location);
                }
            };
            grid.Show();
            grid.Activate();
            refreshTimer.Start();
        }
        finally
        {
            opening = false;
        }
    }

    private void ForgetGrid()
    {
        refreshTimer.Stop();
        grid = null;
    }

    // A floating button on each monitor the settings give one: by default each but the
    // main one, whose taskbar has the real tray.
    private void MakeButtons()
    {
        foreach (var monitor in Screen.AllScreens)
        {
            if (settings.ButtonOn(monitor))
            {
                MakeButton(monitor);
            }
        }
    }

    // The floating button of this monitor, where the user last dragged it there, as its
    // settings give it.
    private void MakeButton(Screen monitor)
    {
        var button = new FloatingButton(monitor, settings.ButtonPlace(monitor), settings.LookOf(monitor), HotkeyName, NewMenu(monitor));
        button.Clicked += () => OpenOrCloseGrid(monitor);
        button.Dragged += place => settings.SetButtonPlace(monitor, place);
        // An open grid on this monitor goes along with the button while it is dragged.
        button.Moving += () =>
        {
            if (grid != null && grid.Monitor.DeviceName == monitor.DeviceName)
            {
                grid.FollowButton();
            }
        };
        buttons.Add(button);
        // Shown first, so that it takes its place (FloatingButton.OnLoad), where a grid
        // opened with the hotkey goes next to it; then hidden at once if a full-screen
        // window is in front there. A game that changes its monitor's resolution, say, has
        // the buttons made again (OnMonitorsChanged).
        button.Show();
        ShowUnlessFullScreen(button);
    }

    // The floating button on this monitor, or null if it has none.
    private FloatingButton? ButtonOf(Screen monitor) => buttons.Find(b => b.Monitor.DeviceName == monitor.DeviceName);

    // Every 0.5 s: each button hides while a full-screen window (a video, a game) is in
    // front on its monitor, and shows again once none is (owner's choice, To do 18). A
    // hidden button keeps its place, so the hotkey's grid still opens next to it.
    private void HideUnderFullScreen()
    {
        foreach (var button in buttons)
        {
            ShowUnlessFullScreen(button);
        }
    }

    // Changed only when it differs, so a button that shows isn't shown again at each check.
    private static void ShowUnlessFullScreen(FloatingButton button)
    {
        var show = !FullScreen.InFront(button.Monitor, button.Handle);
        if (button.Visible != show)
        {
            button.Visible = show;
        }
    }

    // "Show the floating button" in the settings window.
    private void SetButtonOn(Screen monitor, bool on)
    {
        settings.SetButtonOn(monitor, on);
        MakeButtonAgain(monitor);
    }

    // The color, size and always on top of a monitor's button in the settings window.
    // Always on top is set only when Windows creates a window, so the button is made again
    // for each change.
    private void SetLook(Screen monitor, ButtonLook look)
    {
        settings.SetLook(monitor, look);
        MakeButtonAgain(monitor);
    }

    // New keys in the settings window: taken if no other program has them (else the old
    // ones stay, and it returns false), saved, and named in the buttons' tooltips.
    private bool SetHotkey(HotkeyKeys keys)
    {
        if (!hotkey.TryChange(keys))
        {
            return false;
        }
        settings.SetHotkey(keys);
        foreach (var button in buttons)
        {
            button.ShowHotkey(HotkeyName);
        }
        return true;
    }

    // After a change to a monitor's button settings: its button closes and, if it is on,
    // is made again as the settings now give it. A grid open there was placed for the
    // button as it was, so it closes and opens again at its new place: next to the
    // button, or else where the grid was last dragged there or in the corner. The
    // button's own place stays saved, for when it is turned on again.
    private void MakeButtonAgain(Screen monitor)
    {
        // A grid open on another monitor stays as it is.
        var gridHere = false;
        if (grid != null && grid.Monitor.DeviceName == monitor.DeviceName)
        {
            grid.Close();
            gridHere = true;
        }
        var button = ButtonOf(monitor);
        if (button != null)
        {
            button.Close();
            buttons.Remove(button);
        }
        if (settings.ButtonOn(monitor))
        {
            MakeButton(monitor);
        }
        if (gridHere)
        {
            OpenOrCloseGrid(monitor);
        }
    }

    // Windows raises this when a monitor is plugged in or out, or its resolution or place
    // changes. The buttons are made again for the monitors there are now, and an open
    // grid or settings window closes: each was laid out for its monitor as it was.
    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        grid?.Close();
        settingsWindow?.Close();
        foreach (var button in buttons)
        {
            button.Close();
        }
        buttons.Clear();
        MakeButtons();
    }

    // Reads the tray again if the grid's icons are older than the grid's use allows. So
    // the mouse coming onto a grid left unused brings a read within 0.5 s, unless the last
    // one is under 2 s old anyway.
    private void RefreshIfDue()
    {
        if (grid == null)
        {
            return;
        }
        var maxAge = grid.InUse ? InUseRefresh : UnusedRefresh;
        if (Environment.TickCount64 - lastRead >= maxAge)
        {
            RefreshGrid();
        }
    }

    // Reads the tray again and shows it in the open grid. Never opens the flyout, so a
    // failing read can't make it flash at every refresh; a part that fails keeps its last
    // icons. A call during a slow read gets that same read, so reads don't pile up.
    private async void RefreshGrid()
    {
        lastRead = Environment.TickCount64;
        var state = await reader.ReadAsync(openIfMissing: false);
        // The grid may have closed during the read. Sorted now, not before the read: the
        // user may have dragged an icon meanwhile.
        grid?.ShowState(Sorted(state));
    }

    // A read in the order the user dragged the icons into, in each section.
    private TrayState Sorted(TrayState state) => state with
    {
        Pinned = IconOrder.Sort(state.Pinned, settings.PinnedOrder),
        Overflow = IconOrder.Sort(state.Overflow, settings.OverflowOrder),
    };

    // The right-click menu of a button or of the grid on this monitor.
    private ContextMenuStrip NewMenu(Screen monitor) => AppMenu.Create(() => OpenSettings(monitor), ResetIconOrder);

    // "Settings...": the settings window in the middle of this monitor. One at a time: open
    // here, it comes to the front, showing the settings as they are now; open on another
    // monitor, it moves here (closed there, opened here, like the grid), so it is always
    // laid out for the monitor it is on.
    private void OpenSettings(Screen monitor)
    {
        if (settingsWindow != null && settingsWindow.Monitor.DeviceName != monitor.DeviceName)
        {
            // Closed at once: FormClosed below has run when Close returns.
            settingsWindow.Close();
        }
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsForm(monitor, autostart, settings.ButtonOn, SetButtonOn, settings.LookOf, SetLook, hotkey, SetHotkey,
                settings.HasOrder, ResetIconOrder);
            settingsWindow.FormClosed += (_, _) => settingsWindow = null;
            settingsWindow.Show();
        }
        else
        {
            settingsWindow.ShowSettings();
        }
        settingsWindow.Activate();
    }

    // "Reset icon order": the tray's own order again, in an open grid too. From the menu
    // too, an open settings window greys its button at once: the menu doesn't take the
    // front from it, so it isn't activated again.
    private void ResetIconOrder()
    {
        settings.ResetOrder();
        settingsWindow?.ShowSettings();
        if (grid != null)
        {
            RefreshGrid();
        }
    }

    // A click on a tile goes to its real tray icon, on a background thread: the click
    // calls into Explorer (17 to 82 ms, measured in To do 2), too long for the UI thread.
    // menuPoint: where a menu the click opens belongs.
    private async void ClickIcon(TrayItem item, MouseButtons button, Point menuPoint)
    {
        // Before the click goes out: Steam's menu can show 51 ms after a click (measured,
        // To do 7 step A), before the click call returns, and one already in its wrong
        // place when the watch starts would bring no later event to move it.
        steamMenu.Watch(menuPoint);
        try
        {
            await Task.Run(() =>
            {
                if (button == MouseButtons.Right)
                {
                    TrayClicks.RightClick(item);
                }
                else
                {
                    TrayClicks.LeftClick(item);
                }
            });
        }
        catch (InvalidOperationException)
        {
            // The only exception TrayClicks throws: the icon is gone, or it was kept from
            // an older read after a failed one. Show nothing; read the tray again now
            // instead of at the next tick, so the grid catches up.
            RefreshGrid();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // A static event: it would keep the app alive and call it after it is gone.
            SystemEvents.DisplaySettingsChanged -= OnMonitorsChanged;
            foreach (var button in buttons)
            {
                button.Dispose();
            }
            settingsWindow?.Dispose();
            refreshTimer.Dispose();
            fullScreenTimer.Dispose();
            hotkey.Dispose();
            steamMenu.Dispose();
            toastMover.Dispose();
        }
        base.Dispose(disposing);
    }
}

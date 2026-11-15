using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Tray;

namespace TrayAnywhere;

// The running app. It has no main window: it shows a floating button on each secondary
// monitor, opens or closes the grid when the hotkey is pressed or a button is clicked,
// reads the tray again while the grid is open (every 2 s while it is used, every 10 s
// while not), and passes clicks on the grid to the real tray icons. The app ends with
// Exit in the right-click menu of a button or of the grid.
internal sealed class App : ApplicationContext
{
    // Alt and the key left of 1: one hand, two neighbouring keys (owner's choice).
    public const string HotkeyName = "Alt+`";

    private readonly Hotkey hotkey = new();
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
    // Where the user dragged the buttons and the grid; saved at each drag.
    private readonly Settings settings;
    // "Start with Windows" in every window's right-click menu.
    private readonly Autostart autostart;
    // One floating button per secondary monitor; made again when the monitors change.
    private readonly List<FloatingButton> buttons = [];
    // The open grid, or null.
    private GridForm? grid;
    // True while the grid waits for its tray read, so a second press doesn't open two.
    private bool opening;

    // useHotkey: false for the GUI test's copy, which leaves the keys to the user's copy;
    // its hidden hotkey window is still made, and the test sends it the hotkey's message.
    public App(Settings settings, Autostart autostart, bool useHotkey)
    {
        this.settings = settings;
        this.autostart = autostart;
        // Oemtilde: the ` and ~ key on a US keyboard.
        HotkeyTaken = useHotkey && !hotkey.Register(Hotkey.Alt, Keys.Oemtilde);
        // The monitor the mouse is on at the press, not when the read is done.
        hotkey.Pressed += () => OpenOrCloseGrid(Screen.FromPoint(Cursor.Position));
        refreshTimer.Tick += (_, _) => RefreshIfDue();

        // A first read at start: it warms up UI Automation (the first read is the
        // slowest), and if Explorer hasn't created the overflow window yet, the one
        // flash of the flyout happens now instead of when the grid opens. No read and no
        // buttons when the app is about to quit because the hotkey is taken.
        if (!HotkeyTaken)
        {
            _ = reader.ReadAsync(openIfMissing: true);
            MakeButtons();
            SystemEvents.DisplaySettingsChanged += OnMonitorsChanged;
        }
    }

    // True if the app wanted the hotkey and another program already uses it.
    public bool HotkeyTaken { get; }

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
            // on a monitor without a button (the main one) does the grid keep a place of its
            // own, where it was last dragged.
            var button = buttons.Find(b => b.Monitor.DeviceName == monitor.DeviceName);
            grid = new GridForm(state, monitor, button == null ? settings.GridPlace(monitor) : null, button, AppMenu.Create(autostart));
            grid.FormClosed += (_, _) => ForgetGrid();
            grid.IconClicked += ClickIcon;
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

    // One floating button on each monitor but the main one (the main taskbar has the real
    // tray), where the user last dragged it there.
    private void MakeButtons()
    {
        foreach (var monitor in Screen.AllScreens)
        {
            if (monitor.Primary)
            {
                continue;
            }
            var button = new FloatingButton(monitor, settings.ButtonPlace(monitor), AppMenu.Create(autostart));
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
            button.Show();
        }
    }

    // Windows raises this when a monitor is plugged in or out, or its resolution or place
    // changes. The buttons are made again for the monitors there are now, and an open
    // grid closes: each was laid out for its monitor as it was.
    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        grid?.Close();
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
        // The grid may have closed during the read.
        grid?.ShowState(state);
    }

    // A click on a tile goes to its real tray icon, on a background thread: the click
    // calls into Explorer (17 to 82 ms, measured in To do 2), too long for the UI thread.
    private async void ClickIcon(TrayItem item, MouseButtons button)
    {
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
            refreshTimer.Dispose();
            hotkey.Dispose();
        }
        base.Dispose(disposing);
    }
}

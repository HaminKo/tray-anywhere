using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TrayAnywhere;

// Moves Steam's tray menu to the click. Steam opens it at the mouse plus the monitor's
// top-left corner times (scale - 1): at the mouse on the main monitor and on 100 %
// monitors, but far off, even off-screen, on a scaled monitor that isn't at 0,0 (Steam's
// own bug; docs/research/uia-tray.md "To do 7 step A"). After each forwarded click, two
// WinEvent hooks tell the app when any window shows, hides or moves; when Steam's menu
// opens soon after the click and far from it, the app moves it there. Steam moves its
// menu itself while it opens and again just before it hides it; each of those moves is
// corrected the same way.
// Only Steam's menu: other programs' popups (notifications, say) look the same by their
// window styles, and would be moved too (docs/decisions.md, 2026-11-16).
internal sealed class SteamMenu : IDisposable
{
    // Steam's menu, as measured: a window of class SDL_app titled "Menu", of the program
    // steamwebhelper (not steam.exe).
    private const string MenuClass = "SDL_app";
    private const string MenuTitle = "Menu";
    private const string SteamHelper = "steamwebhelper";
    // The GUI test's own window like Steam's menu (src/GuiTest/SteamLikeMenu.cs): the test
    // can't make Steam open its menu.
    private const string GuiTest = "GuiTest";
    // A menu this close to the click, in pixels, is where it belongs: Steam put it right,
    // or the app just moved it there (its own move raises a "moved" event too, which must
    // not move it again).
    private const int Near = 50;
    // How long after a click, in ms, a menu that opens counts as the click's; also how
    // long the hooks stay after the last click while no such menu is open. Steam's menu
    // showed 157 to 281 ms after a click (measured).
    private const long WatchTime = 1000;

    private readonly string[] programs;
    // In a field: Windows calls it through a pointer the garbage collector doesn't know
    // about, so without the field it could be collected while a hook still uses it.
    private readonly WinEventProc callback;
    // Removes the hooks once they aren't needed (see OnTick).
    private readonly Timer timer = new() { Interval = 1000 };
    private IntPtr showHideHook;
    private IntPtr moveHook;
    // Where the last click's menu belongs, and when that click was (Environment.TickCount64).
    private Point clickPoint;
    private long clickTime;
    // Steam's menu window, once seen: Steam reuses one window for every menu.
    private IntPtr menu;
    // True while that menu is open and opened within WatchTime of a click in the grid: the
    // app moves it. Not one opened later from Steam's real icon on the main taskbar, where
    // Steam puts it right.
    private bool moving;

    // alsoGuiTest: for the GUI test's copy of the app, which also moves the test's own
    // window like Steam's menu.
    public SteamMenu(bool alsoGuiTest)
    {
        programs = alsoGuiTest ? [SteamHelper, GuiTest] : [SteamHelper];
        callback = OnWinEvent;
        timer.Tick += OnTick;
    }

    // Called just before a click goes to a tray icon: a menu it opens belongs at
    // clickPoint. Sets the hooks if they aren't set. Out of context: nothing runs inside
    // Steam; Windows queues each event to this (the UI) thread, between its other messages.
    // The hooks skip this program's own windows (the grid, the buttons).
    public void Watch(Point clickPoint)
    {
        this.clickPoint = clickPoint;
        clickTime = Environment.TickCount64;
        if (!timer.Enabled)
        {
            const uint flags = WinEventOutOfContext | WinEventSkipOwnProcess;
            showHideHook = SetWinEventHook(EventObjectShow, EventObjectHide, IntPtr.Zero, callback, 0, 0, flags);
            moveHook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, IntPtr.Zero, callback, 0, 0, flags);
            timer.Start();
        }
    }

    // The hooks go 1 to 2 s after the last click, once no menu the app moves is open. Not
    // when the menu hides: a second click on Steam's tile closes the open menu, and that
    // "hidden" event can come in after the second click set its watch (owner's choice,
    // To do 7 step B).
    private void OnTick(object? sender, EventArgs e)
    {
        if (Environment.TickCount64 - clickTime >= WatchTime && !moving)
        {
            Stop();
        }
    }

    private void Stop()
    {
        timer.Stop();
        if (showHideHook != IntPtr.Zero)
        {
            UnhookWinEvent(showHideHook);
            showHideHook = IntPtr.Zero;
        }
        if (moveHook != IntPtr.Zero)
        {
            UnhookWinEvent(moveHook);
            moveHook = IntPtr.Zero;
        }
    }

    // A window showed, hid or moved. The hooks report this for every window of every
    // program, and also for parts of windows and the mouse pointer, so the cheap tests
    // come first. Only a window that shows is checked by name; hiding and moving count
    // only for the menu that showed.
    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (objectId != ObjIdWindow || childId != ChildIdSelf)
        {
            return;
        }
        if (eventType == EventObjectShow && IsSteamMenu(window))
        {
            menu = window;
            moving = Environment.TickCount64 - clickTime < WatchTime;
        }
        else if (eventType == EventObjectHide && window == menu)
        {
            moving = false;
        }
        else if (eventType != EventObjectLocationChange || window != menu)
        {
            return;
        }
        if (moving)
        {
            MoveToClick(window);
        }
    }

    // Steam's menu by class and title, then by program: that lookup (a list of all
    // programs) runs only for a window named like it.
    private bool IsSteamMenu(IntPtr window) =>
        ClassOf(window) == MenuClass && TitleOf(window) == MenuTitle && Array.IndexOf(programs, ProgramOf(window)) >= 0;

    // Puts a corner of the menu at the click point, the menu up and to the right of it,
    // flipped left or down where there is no room; inside the free area of the click's
    // monitor. Left alone if it is near the click already.
    private void MoveToClick(IntPtr window)
    {
        if (!GetWindowRect(window, out var r))
        {
            return;
        }
        var bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        var near = bounds;
        near.Inflate(Near, Near);
        if (near.Contains(clickPoint))
        {
            return;
        }
        var area = Screen.FromPoint(clickPoint).WorkingArea;
        var size = bounds.Size;
        var x = clickPoint.X + size.Width <= area.Right ? clickPoint.X : clickPoint.X - size.Width;
        var y = clickPoint.Y - size.Height >= area.Top ? clickPoint.Y - size.Height : clickPoint.Y;
        var place = TopWindow.KeepInside(area, new Point(x, y), size);
        // Already there (kept inside the area, it can end up over 50 px from the click):
        // a move to the same place could bring another "moved" event.
        if (place != bounds.Location)
        {
            SetWindowPos(window, IntPtr.Zero, place.X, place.Y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }
    }

    // A window's class name and title. One longer than 63 characters comes back cut to
    // 63, still longer than the menu's, so it can't match.
    private static string ClassOf(IntPtr window)
    {
        var name = new StringBuilder(64);
        GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    private static string TitleOf(IntPtr window)
    {
        var title = new StringBuilder(64);
        GetWindowText(window, title, title.Capacity);
        return title.ToString();
    }

    // The name of the window's program without ".exe", or null if it has ended. An
    // exception must not leave this callback: Windows called it, and the app would crash.
    private static string? ProgramOf(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        Stop();
        timer.Dispose();
    }

    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WinEventOutOfContext = 0x0;
    private const uint WinEventSkipOwnProcess = 0x2;
    // The event is about the window itself, not a part of it or the mouse pointer.
    private const int ObjIdWindow = 0;
    private const int ChildIdSelf = 0;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder title, int size);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

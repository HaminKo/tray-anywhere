using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// Moves Windows' notification popups (toasts, and tray apps' balloons, which Windows
// shows as toasts) to the monitor the mouse is on. Windows shows them at the bottom right
// of the main monitor only. One window of the shell (the process ShellExperienceHost)
// shows them all; the shell sizes and places it for each toast, and a WinEvent hook on
// that process tells the app each time, so it moves the window again.
// Measurements: docs/research/notifications.md. Only this window: no mover for other
// programs' windows (docs/decisions.md, 2026-11-18).
// While a full-screen program covers the main monitor, Windows shows no toasts at all
// (Do Not Disturb), so there is nothing to move.
internal sealed class ToastMover : IDisposable
{
    // The toast window, as measured on English Windows: its title is in Windows' language.
    private const string ToastClass = "Windows.UI.Core.CoreWindow";
    private const string ToastTitle = "New notification";

    // In a field: Windows calls it through a pointer the garbage collector doesn't know
    // about, so without the field it could be collected while the hook still uses it.
    private readonly WinEventProc callback;
    // Looks for the toast window again every 5 s: Windows starts the shell again after it
    // crashes or Explorer restarts, with a new window in a new process, and the hook is
    // for one process.
    private readonly Timer timer = new() { Interval = 5000 };
    private IntPtr hook;
    // The toast window the hook is for; zero if none was found.
    private IntPtr toast;

    public ToastMover()
    {
        callback = OnWinEvent;
        timer.Tick += (_, _) => FindToastWindow();
        FindToastWindow();
        timer.Start();
    }

    // Sets the hook for the shell process that has the toast window, if that window is
    // new. Only that process's events: a hook for every program's "moved" events would
    // also get one for each move of the mouse pointer. If it fails (the shell still
    // starting at sign-in, say), toast stays zero and the next tick tries again.
    private void FindToastWindow()
    {
        var window = FindWindow(ToastClass, ToastTitle);
        if (window == toast)
        {
            return;
        }
        Unhook();
        toast = IntPtr.Zero;
        if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out var processId) == 0)
        {
            return;
        }
        // Out of context: nothing runs inside the shell; Windows queues each event to this
        // (the UI) thread, between its other messages.
        hook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, IntPtr.Zero, callback, processId, 0, WinEventOutOfContext);
        if (hook != IntPtr.Zero)
        {
            toast = window;
            // A toast that already shows.
            MoveToMouse();
        }
    }

    private void Unhook()
    {
        if (hook != IntPtr.Zero)
        {
            UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }
    }

    // A window of the shell moved or changed size. The toast window never hides: with no
    // toast it is 0 pixels tall, and the shell sizes it for each toast.
    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (window == toast && objectId == ObjIdWindow && childId == ChildIdSelf)
        {
            MoveToMouse();
        }
    }

    // Keeps the toast window, while it shows a toast, at the bottom right of the free area
    // of the mouse's monitor. Every place it takes is checked, not only the shell's first
    // one: after the app has moved it, the shell sometimes moves it again, even below the
    // screen (owner's test, To do 13). A window already at its place is left alone: the
    // app's own move raises a "moved" event too, and so does a move by another copy of
    // the app (the GUI test's), which puts it at the same place.
    private void MoveToMouse()
    {
        if (!GetWindowRect(toast, out var r))
        {
            return;
        }
        var bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        if (bounds.Height == 0)
        {
            return;
        }
        var monitor = Screen.FromPoint(Cursor.Position);
        // The shell lays toasts out for the main monitor's scale (it sizes the window
        // there). On a monitor with another scale, the window keeps its size in pixels,
        // but its toasts draw at that monitor's scale, at the window's bottom left; the
        // rest of it is see-through. Resizing it would cut them off: the shell doesn't lay
        // them out again (measured). So the window goes as far left as its toasts' new
        // width needs. On the main monitor this is the shell's own place.
        var scale = Dpi.ScaleOf(monitor) / Dpi.ScaleOf(Screen.PrimaryScreen!);
        var area = monitor.WorkingArea;
        var place = new Point(area.Right - (int)Math.Round(bounds.Width * scale), area.Bottom - bounds.Height);
        if (place != bounds.Location)
        {
            SetWindowPos(toast, IntPtr.Zero, place.X, place.Y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }
    }

    public void Dispose()
    {
        Unhook();
        timer.Dispose();
    }

    private const uint EventObjectLocationChange = 0x800B;
    private const uint WinEventOutOfContext = 0x0;
    // The event is about the window itself, not a part of it.
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string title);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

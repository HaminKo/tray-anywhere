using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tray;

// Asks Explorer whether an entry of Windows' list of tray icons (IconSettings) has an icon
// in the tray right now, with the documented Shell_NotifyIconGetRect. Explorer knows an
// icon by its GUID, or by the program's window and the icon's number; so this lists every
// window once and asks with each window of the entry's program.
// A "yes" is sure. A "no" can be wrong: a program may number its icon differently from the
// number Windows stored (Task Manager does, and so does a script that makes a new icon for
// each message; measured: docs/research/uia-tray.md "To do 9").
internal sealed class ShownIcons
{
    // Each process's windows: top-level ones, hidden ones included, and message-only ones.
    // Every tray icon seen was on one of those two kinds (measured).
    private readonly Dictionary<int, List<IntPtr>> windowsByProcess = [];

    public ShownIcons()
    {
        EnumWindows((window, _) =>
        {
            Add(window);
            return true;
        }, IntPtr.Zero);
        // EnumWindows skips message-only windows; they are the children of HWND_MESSAGE, and
        // only FindWindowEx lists them (EnumChildWindows finds none, measured). If the window
        // the loop is at closes meanwhile, the list ends early: this read may miss an icon,
        // the next one won't.
        var messageOnly = FindWindowEx(HwndMessage, IntPtr.Zero, null, null);
        while (messageOnly != IntPtr.Zero)
        {
            Add(messageOnly);
            messageOnly = FindWindowEx(HwndMessage, messageOnly, null, null);
        }
    }

    // Whether the entry's icon is in the tray now. processIds: the processes that run the
    // entry's program.
    public bool IsShown(IconSetting setting, List<int> processIds)
    {
        if (setting.IconGuid is Guid guid)
        {
            // The GUID alone names the icon (measured).
            return IsKnown(IntPtr.Zero, 0, guid);
        }
        foreach (var processId in processIds)
        {
            if (!windowsByProcess.TryGetValue(processId, out var windows))
            {
                continue;
            }
            foreach (var window in windows)
            {
                if (IsKnown(window, setting.Uid ?? 0, Guid.Empty))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void Add(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var processId);
        if (!windowsByProcess.TryGetValue(processId, out var windows))
        {
            windows = [];
            windowsByProcess[processId] = windows;
        }
        windows.Add(window);
    }

    // Explorer answers S_OK (0) for an icon it shows, on the taskbar or in the overflow,
    // and E_FAIL for one it doesn't know. The place it gives isn't used: for an overflow
    // icon it is the overflow arrow's while the flyout is closed (measured).
    private static bool IsKnown(IntPtr window, uint uid, Guid guid)
    {
        var icon = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            Window = window,
            Uid = uid,
            Guid = guid,
        };
        return Shell_NotifyIconGetRect(ref icon, out _) == 0;
    }

    // NOTIFYICONIDENTIFIER: an icon by its GUID, or (Guid.Empty) by its window and number.
    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Uid;
        public Guid Guid;
    }

    private struct Win32Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier icon, out Win32Rect place);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

    // HWND_MESSAGE.
    private static readonly IntPtr HwndMessage = new(-3);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
}

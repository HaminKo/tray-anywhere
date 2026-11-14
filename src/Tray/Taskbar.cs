using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace Tray;

// Reads the Windows taskbar's tray through UI Automation. Everything that depends on
// how the taskbar is built (window classes, AutomationIds) is in this file, so a
// Windows update that changes it is fixed here. The tree is described in
// docs/research/uia-tray.md.
public static class Taskbar
{
    // The XAML pane inside the primary taskbar's window. The tray buttons are its
    // direct children; the task list is one level deeper (inside TaskbarFrame), so
    // searching only the direct children skips it.
    private const string TrayPaneClass = "Windows.UI.Input.InputSite.WindowClass";

    // App icons and Windows' own tray buttons have these AutomationIds.
    private const string AppIconId = "NotifyItemIcon";
    private const string SystemIconId = "SystemTrayIcon";

    // Reads the tray buttons on the primary taskbar, in the taskbar's order (left to right).
    // Throws InvalidOperationException that names the part it could not find.
    public static List<TrayItem> ReadVisibleItems()
    {
        var taskbarWindow = FindWindow("Shell_TrayWnd", null);
        if (taskbarWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("Found no window with class Shell_TrayWnd (the primary taskbar).");
        }
        var taskbar = AutomationElement.FromHandle(taskbarWindow);

        var isTrayPane = new PropertyCondition(AutomationElement.ClassNameProperty, TrayPaneClass);
        var trayPane = taskbar.FindFirst(TreeScope.Children, isTrayPane)
            ?? throw new InvalidOperationException($"UI Automation found no {TrayPaneClass} under Shell_TrayWnd.");

        var isAppIcon = new PropertyCondition(AutomationElement.AutomationIdProperty, AppIconId);
        var isSystemIcon = new PropertyCondition(AutomationElement.AutomationIdProperty, SystemIconId);
        var buttons = trayPane.FindAll(TreeScope.Children, new OrCondition(isAppIcon, isSystemIcon));

        // The clock and Show Desktop are always there, so none at all means the tree changed.
        if (buttons.Count == 0)
        {
            throw new InvalidOperationException(
                $"UI Automation found no tray buttons ({AppIconId} or {SystemIconId}) under {TrayPaneClass}.");
        }

        var items = new List<TrayItem>();
        foreach (AutomationElement button in buttons)
        {
            var info = button.Current;
            var kind = info.AutomationId == AppIconId ? TrayItemKind.Pinned : TrayItemKind.System;
            items.Add(new TrayItem(info.Name, kind, ToRectangle(info.BoundingRectangle)));
        }
        return items;
    }

    // Win32: the top-level window with this class. Asking UI Automation to search the
    // desktop's windows instead took 100-370 ms per read; this takes 1-5 ms (measured).
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    // UI Automation gives pixels as doubles; WinForms uses whole numbers.
    // An element that isn't drawn reports an empty rectangle with infinite values.
    private static Rectangle ToRectangle(Rect r)
    {
        if (r.IsEmpty)
        {
            return Rectangle.Empty;
        }
        return new Rectangle(
            (int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace Tray;

// Reads the Windows taskbar's tray through UI Automation. Everything that depends on
// how the taskbar is built (window classes, AutomationIds) is in this file, so a
// Windows update that changes it is fixed here. The tree is described in
// docs/research/uia-tray.md.
public static class Taskbar
{
    // A XAML pane: inside the primary taskbar's window, and inside the overflow window.
    // The tray buttons are its direct children. On the taskbar the task list is one
    // level deeper (inside TaskbarFrame), so searching only the direct children skips it.
    private const string TrayPaneClass = "Windows.UI.Input.InputSite.WindowClass";

    // The overflow flyout (behind the "Show Hidden Icons" arrow) is its own top-level
    // window. Explorer creates it the first time the flyout opens; from then on it exists
    // while the flyout is closed, hidden, and keeps its buttons up to date (measured on
    // build 26300). Its pane is a child of the bridge window, which is a child of the
    // overflow window.
    private const string OverflowWindowClass = "TopLevelWindowForOverflowXamlIsland";
    private const string OverflowBridgeClass = "Windows.UI.Composition.DesktopWindowContentBridge";

    // The overflow arrow's name. Nothing else tells it apart from the input indicators
    // (measured), so this only works while Windows' display language is English.
    private const string OverflowArrowName = "Show Hidden Icons";

    // App icons and Windows' own tray buttons have these AutomationIds.
    private const string AppIconId = "NotifyItemIcon";
    private const string SystemIconId = "SystemTrayIcon";

    // Reads the tray buttons on the primary taskbar, in the taskbar's order (left to right).
    // Throws InvalidOperationException that names the part it could not find.
    public static List<TrayItem> ReadVisibleItems()
    {
        var items = ReadButtons(FindTrayPane(), TrayItemKind.Pinned);

        // The clock and Show Desktop are always there, so none at all means the tree changed.
        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                $"UI Automation found no tray buttons ({AppIconId} or {SystemIconId}) under {TrayPaneClass}.");
        }
        return items;
    }

    // Reads the app icons in the overflow, in the flyout's order, from its hidden window.
    // While the flyout is closed they have no position: their Bounds are empty.
    // After Explorer starts, the overflow window doesn't exist until the flyout has been
    // opened once. With openIfMissing, this then opens and closes the flyout (a flash of
    // about 0.2 s; the keyboard focus moves to the taskbar) and sets openedFlyout. Pass
    // false when reading every few seconds, so a failure can't flash it again and again.
    // Throws InvalidOperationException that names the part it could not find.
    public static List<TrayItem> ReadOverflowItems(bool openIfMissing, out bool openedFlyout)
    {
        openedFlyout = false;
        if (openIfMissing && FindWindowEx(IntPtr.Zero, IntPtr.Zero, OverflowWindowClass, null) == IntPtr.Zero)
        {
            OpenAndCloseOverflow();
            openedFlyout = true;
        }

        // UI Automation doesn't walk down into a hidden window, so find the pane's own
        // window with Win32 and start UI Automation there.
        var overflowWindow = FindWindowByClass(
            IntPtr.Zero, OverflowWindowClass, "the overflow flyout; Explorer creates it the first time the flyout opens");
        var bridge = FindWindowByClass(overflowWindow, OverflowBridgeClass, $"inside {OverflowWindowClass}");
        var paneWindow = FindWindowByClass(bridge, TrayPaneClass, $"inside {OverflowBridgeClass}");

        // No buttons is fine: every icon may be shown on the taskbar.
        return ReadButtons(AutomationElement.FromHandle(paneWindow), TrayItemKind.Overflow);
    }

    // The XAML pane in the primary taskbar that holds the tray buttons.
    private static AutomationElement FindTrayPane()
    {
        var taskbar = AutomationElement.FromHandle(FindWindowByClass(IntPtr.Zero, "Shell_TrayWnd", "the primary taskbar"));
        var isTrayPane = new PropertyCondition(AutomationElement.ClassNameProperty, TrayPaneClass);
        return taskbar.FindFirst(TreeScope.Children, isTrayPane)
            ?? throw new InvalidOperationException($"UI Automation found no {TrayPaneClass} under Shell_TrayWnd.");
    }

    // Opens the flyout by invoking the overflow arrow, waits until the overflow window
    // shows (124 ms on a first opening, measured), then invokes the arrow again, which
    // closes it. The window then stays, hidden. Waits at most 3 s, far more than measured,
    // so that a slow opening isn't left open on screen; then throws.
    private static void OpenAndCloseOverflow()
    {
        var isArrow = new PropertyCondition(AutomationElement.NameProperty, OverflowArrowName);
        var arrow = FindTrayPane().FindFirst(TreeScope.Children, isArrow)
            ?? throw new InvalidOperationException(
                $"Found no tray button named \"{OverflowArrowName}\" (the overflow arrow; its name follows Windows' display language).");
        var invoke = (InvokePattern)arrow.GetCurrentPattern(InvokePattern.Pattern);
        invoke.Invoke();

        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 3000)
        {
            var window = FindWindowEx(IntPtr.Zero, IntPtr.Zero, OverflowWindowClass, null);
            if (window != IntPtr.Zero && IsWindowVisible(window))
            {
                // Invoking the arrow again closes the flyout. Only now: had it not opened
                // yet, a second invoke could leave it open.
                invoke.Invoke();
                return;
            }
            Thread.Sleep(10);
        }
        throw new InvalidOperationException(
            $"Invoked \"{OverflowArrowName}\", but no {OverflowWindowClass} showed within 3 s.");
    }

    // Reads the tray buttons that are the pane's direct children, in order. App icons
    // get appIconKind; Windows' own buttons get System.
    private static List<TrayItem> ReadButtons(AutomationElement pane, TrayItemKind appIconKind)
    {
        var isAppIcon = new PropertyCondition(AutomationElement.AutomationIdProperty, AppIconId);
        var isSystemIcon = new PropertyCondition(AutomationElement.AutomationIdProperty, SystemIconId);
        var buttons = pane.FindAll(TreeScope.Children, new OrCondition(isAppIcon, isSystemIcon));

        var items = new List<TrayItem>();
        foreach (AutomationElement button in buttons)
        {
            var info = button.Current;
            var kind = info.AutomationId == AppIconId ? appIconKind : TrayItemKind.System;
            items.Add(new TrayItem(info.Name, kind, ToRectangle(info.BoundingRectangle)));
        }
        return items;
    }

    // Win32: the first window with this class among the parent's direct children;
    // IntPtr.Zero as the parent searches the top-level windows. Throws if there is none.
    // Asking UI Automation to search the desktop's windows instead took 100-370 ms per
    // read; this takes 1-5 ms (measured).
    private static IntPtr FindWindowByClass(IntPtr parent, string className, string where)
    {
        var window = FindWindowEx(parent, IntPtr.Zero, className, null);
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Found no window with class {className} ({where}).");
        }
        return window;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

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

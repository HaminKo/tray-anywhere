using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Tray;

// Passes left and right clicks to the real tray icons. Both act on the tray button
// itself, not on a place on the screen: the mouse doesn't move, and overflow icons are
// clicked while the flyout stays closed (measured on build 26300; see
// docs/research/uia-tray.md "Spike step D"). Where the app's menu or window then appears
// is up to the app: at the mouse for Steam and Discord, above its icon for OneDrive.
public static class TrayClicks
{
    // A left click: the button's Invoke pattern, the one action Explorer's tray buttons
    // offer. Throws InvalidOperationException if it fails, e.g. because the icon is gone.
    public static void LeftClick(TrayItem item)
    {
        var button = ButtonOf(item);
        LetAppComeToFront();
        try
        {
            var invoke = (InvokePattern)button.Element.GetCurrentPattern(InvokePattern.Pattern);
            invoke.Invoke();
        }
        catch (Exception e) when (e is not InvalidOperationException)
        {
            throw ClickFailed("Left", item, e);
        }
    }

    // A right click: IUIAutomationElement3.ShowContextMenu. .NET's UI Automation library
    // doesn't offer it, so this uses COM UI Automation, a second view of the same tree:
    // there it starts at the button's pane (by its window) and takes the child with the
    // button's runtime ID (UI Automation's ID for an element while it exists). A new COM
    // object per click costs little for something done by hand, and it is used only on
    // the calling thread. Throws InvalidOperationException if it fails, e.g. because the
    // icon is gone.
    public static void RightClick(TrayItem item)
    {
        var button = ButtonOf(item);
        LetAppComeToFront();
        try
        {
            var runtimeId = button.Element.GetRuntimeId();
            var automation = (IUIAutomation)new CUIAutomation();
            var walker = automation.ControlViewWalker;
            var child = walker.GetFirstChildElement(automation.ElementFromHandle(button.PaneWindow));
            while (child != null && !child.GetRuntimeId().SequenceEqual(runtimeId))
            {
                child = walker.GetNextSiblingElement(child);
            }
            if (child == null)
            {
                throw new InvalidOperationException($"Right click on \"{item.Name}\" failed: the icon is no longer in the tray.");
            }
            ShowContextMenu(child);
        }
        catch (Exception e) when (e is not InvalidOperationException)
        {
            throw ClickFailed("Right", item, e);
        }
    }

    private static TrayButton ButtonOf(TrayItem item) =>
        item.Button ?? throw new InvalidOperationException($"\"{item.Name}\" wasn't read from the tray: no button to click.");

    // UI Automation and COM report a failure with many exception types (ElementNotAvailable,
    // COMException, ArgumentException, TimeoutException and more), e.g. when the icon went
    // away after it was read or Explorer restarted. Callers catch this one type instead.
    private static InvalidOperationException ClickFailed(string which, TrayItem item, Exception e) =>
        new($"{which} click on \"{item.Name}\" failed (the icon may be gone): {e.Message}", e);

    // Lets the tray app put its menu or window in front, as a real click does. Windows lets
    // a program come to the front only if the program in front now allows it. On a real
    // click that is Explorer (the taskbar was just clicked), which passes the right on; on
    // a forwarded click it is the caller (the grid, just clicked), so the caller passes it
    // on here. Steam shows its tray menu only with that right (owner's test, To do 3 step
    // B; docs/research/uia-tray.md). Any process, not only the icon's: the window can
    // belong to a helper process (Steam's menu does), and the icon's program is only a
    // guess. If the caller isn't in front, Windows refuses and nothing changes.
    private static void LetAppComeToFront() => AllowSetForegroundWindow(AnyProcess);

    // ASFW_ANY in winuser.h.
    private const int AnyProcess = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    // ShowContextMenu is entry 91 of IUIAutomationElement3's table of methods: 3 of
    // IUnknown, 82 of IUIAutomationElement, 6 of IUIAutomationElement2 (uiautomationclient.h;
    // confirmed by a probe that opened Discord's menu). Declaring the 90 methods before it
    // only to call this one would take a page of placeholders, so this calls entry 91.
    private const int ShowContextMenuEntry = 91;
    private static readonly Guid IUIAutomationElement3Id = new("8471DF34-AEE0-4A01-A7DE-7DB9AF12C296");

    // A COM method without parameters; returns an HRESULT (0 = success).
    private delegate int ComMethod(IntPtr self);

    private static void ShowContextMenu(IUIAutomationElement element)
    {
        var unknown = Marshal.GetIUnknownForObject(element);
        var result = Marshal.QueryInterface(unknown, IUIAutomationElement3Id, out var element3);
        Marshal.Release(unknown);
        Marshal.ThrowExceptionForHR(result);
        try
        {
            var methods = Marshal.ReadIntPtr(element3);
            var entry = Marshal.ReadIntPtr(methods, ShowContextMenuEntry * IntPtr.Size);
            var showContextMenu = Marshal.GetDelegateForFunctionPointer<ComMethod>(entry);
            Marshal.ThrowExceptionForHR(showContextMenu(element3));
        }
        finally
        {
            Marshal.Release(element3);
        }
    }

    // COM UI Automation, declared by hand: .NET has no wrapper for it. A COM interface is a
    // table of methods in a fixed order, so each declaration lists the methods in
    // uiautomationclient.h's order up to the last one used here. The unused ones are
    // placeholders, never called, that keep the used ones at their place in the table.

    [ComImport, Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E")]
    private class CUIAutomation
    {
    }

    [ComImport, Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        void CompareElements();
        void CompareRuntimeIds();
        void GetRootElement();
        IUIAutomationElement ElementFromHandle(IntPtr window);
        void ElementFromPoint();
        void GetFocusedElement();
        void GetRootElementBuildCache();
        void ElementFromHandleBuildCache();
        void ElementFromPointBuildCache();
        void GetFocusedElementBuildCache();
        void CreateTreeWalker();
        IUIAutomationTreeWalker ControlViewWalker { get; }
    }

    [ComImport, Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        void SetFocus();
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)]
        int[] GetRuntimeId();
    }

    [ComImport, Guid("4042C624-389C-4AFC-A630-9DF854A541FC"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationTreeWalker
    {
        void GetParentElement();
        IUIAutomationElement? GetFirstChildElement(IUIAutomationElement element);
        void GetLastChildElement();
        IUIAutomationElement? GetNextSiblingElement(IUIAutomationElement element);
    }
}

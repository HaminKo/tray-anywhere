using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using static GuiTest.Win32;

namespace GuiTest;

// One of the app's windows as UI Automation finds it: the grid, a floating button, the
// settings window or a tile (in WinForms each control is a window of its own). The test
// works it with messages to this window only, the ones Windows would send it for the
// real mouse and keyboard, and reads its picture with PrintWindow.
internal sealed class AppWindow
{
    public AppWindow(AutomationElement element)
    {
        Element = element;
        Handle = new IntPtr(element.Current.NativeWindowHandle);
        Id = element.Current.AutomationId;
        Name = element.Current.Name;
    }

    public AutomationElement Element { get; }
    public IntPtr Handle { get; }
    // The name the app gives it for the test: "Grid", "FloatingButton", or a tile's section.
    public string Id { get; }
    // A tile's tooltip text.
    public string Name { get; }

    // The window's rectangle as the app places it (a form's Bounds), in physical pixels.
    // For the grid that includes Windows' invisible border around its frame.
    public Rectangle Bounds => Win32.Bounds(Handle);

    // The window as it shows on screen: for the grid a few pixels inside Bounds, without
    // the invisible border Windows puts around a frame for resizing. The same as Bounds
    // for a floating button, which has no frame.
    public Rectangle VisibleBounds
    {
        get
        {
            if (DwmGetWindowAttribute(Handle, DwmwaExtendedFrameBounds, out var r, Marshal.SizeOf<Rect>()) != 0)
            {
                throw new CheckFailed("Windows didn't give the window's visible frame");
            }
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    public bool IsOnTop => (GetWindowLong(Handle, GwlExStyle) & WsExTopmost) != 0;

    // Still shown: false once the window is closed (its handle is gone then too).
    public bool IsOpen => IsWindowVisible(Handle);

    // A left click: button down and up at (1, 1) inside the window. The app's own mouse
    // handling runs; the real mouse doesn't move.
    public void Click()
    {
        // x in the low 16 bits, y in the high ones.
        var place = new IntPtr(1 | (1 << 16));
        PostMessage(Handle, WmLButtonDown, new IntPtr(MkLButton), place);
        PostMessage(Handle, WmLButtonUp, IntPtr.Zero, place);
    }

    // What Windows sends after a right click. -1 as the place stands for the menu key, so
    // WinForms opens the window's menu in its middle wherever the mouse is.
    public void RightClick() => PostMessage(Handle, WmContextMenu, Handle, new IntPtr(-1));

    public void PressEscape()
    {
        PostMessage(Handle, WmKeyDown, new IntPtr(VkEscape), IntPtr.Zero);
        PostMessage(Handle, WmKeyUp, new IntPtr(VkEscape), IntPtr.Zero);
    }

    // What a click on the window's X sends.
    public void PressClose() => PostMessage(Handle, WmSysCommand, new IntPtr(ScClose), IntPtr.Zero);

    // A drag by the title bar as the app sees one: Windows' "move begins", the move, and
    // "move ends" (tried in To do 3 step C). The real drag's WM_MOVING, with which the
    // grid keeps itself on its monitor, doesn't come this way.
    public void Drag(Point place)
    {
        SendMessage(Handle, WmEnterSizeMove, IntPtr.Zero, IntPtr.Zero);
        SetWindowPos(Handle, IntPtr.Zero, place.X, place.Y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        SendMessage(Handle, WmExitSizeMove, IntPtr.Zero, IntPtr.Zero);
    }

    // A drag with the left mouse button, as Windows sends it to a window holding the
    // mouse: pressed at the window's centre, moved to `to` (a point on screen) and let go
    // there. Sent, not posted: a tile moves with the mouse, so each message's place in the
    // window is worked out once the one before it has run.
    public void MouseDrag(Point to)
    {
        var start = Bounds;
        SendMessage(Handle, WmLButtonDown, new IntPtr(MkLButton), PlaceInWindow(new Point(start.X + start.Width / 2, start.Y + start.Height / 2)));
        SendMessage(Handle, WmMouseMove, new IntPtr(MkLButton), PlaceInWindow(to));
        SendMessage(Handle, WmLButtonUp, IntPtr.Zero, PlaceInWindow(to));
    }

    // A point on screen as a mouse message gives it: from the window's top-left corner
    // (a tile has no frame), x in the low 16 bits and y in the high ones, either below 0
    // outside the window.
    private IntPtr PlaceInWindow(Point screen)
    {
        var bounds = Bounds;
        var x = screen.X - bounds.X;
        var y = screen.Y - bounds.Y;
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }

    // UI Automation's Invoke: WinForms runs the control's click, as for Enter or a click.
    public void Invoke() => Invoke(Element);

    public static void Invoke(AutomationElement element) =>
        ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    // True when a menu item with a tick (WinForms' CheckOnClick) or a checkbox is ticked.
    public static bool IsTicked(AutomationElement element) =>
        ((TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On;

    // UI Automation's Toggle on a checkbox: WinForms runs its click, as for Space or a click.
    public static void Toggle(AutomationElement element) =>
        ((TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern)).Toggle();

    // UI Automation's Select on a radio button: it gets the dot, as for a click.
    public static void Select(AutomationElement element) =>
        ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

    // True when a radio button has the dot.
    public static bool IsSelected(AutomationElement element) =>
        ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected;

    // A control in this window with this name (a checkbox's text), or null.
    public AutomationElement? Control(string name) =>
        Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));

    // A control in this window with this AutomationId (its WinForms Name), or null.
    public AutomationElement? ControlWithId(string id) =>
        Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    // The radio buttons in the panel with this AutomationId (the settings window's monitor
    // boxes in "Monitors"), in UI Automation's order: the checks find each by its name.
    public List<AutomationElement> RadioButtons(string panelId) =>
        (ControlWithId(panelId) ?? throw new CheckFailed($"the window has no {panelId}"))
            .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton))
            .Cast<AutomationElement>().ToList();

    // The color the window draws at this point, from its top-left corner, in the picture
    // Windows gives with PrintWindow: also where another window covers it. Null when
    // Windows gives none (the window has just gone).
    public Color? ColorAt(Point point)
    {
        var bounds = Bounds;
        if (point.X >= bounds.Width || point.Y >= bounds.Height)
        {
            return null;
        }
        using var picture = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(picture))
        {
            var dc = graphics.GetHdc();
            var given = PrintWindow(Handle, dc, PwRenderFullContent);
            graphics.ReleaseHdc(dc);
            if (!given)
            {
                return null;
            }
        }
        return picture.GetPixel(point.X, point.Y);
    }
}

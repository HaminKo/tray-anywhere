using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// The floating button: a small square with Windows' "show hidden icons" arrow, one on
// each monitor where the settings give one: by default each secondary monitor (owner's
// choice). Its color, size and always on top are set per monitor in the settings window
// (ButtonLook); a change there makes the button again (App). A click opens or closes the
// grid next to it, on the side its arrow points to; dragging moves it within its monitor;
// a right click gives the app's menu (AppMenu). It takes the keyboard focus only when
// clicked or dragged, not when it appears.
internal sealed class FloatingButton : Form
{
    // Sizes at 100 % scaling, in pixels. The arrow on the small button: a ^ (or a v) this
    // wide, drawn with a line this thick; on a bigger button as much bigger.
    private const int ArrowWidth = 10;
    private const float ArrowLine = 1.5f;
    // Between the button and the screen's edge, as for the grid.
    private const int ScreenGap = 12;

    private static readonly Color ArrowColor = Color.FromArgb(220, 220, 220);
    // Windows' message to every window when its colors change, the accent color too.
    private const int WmDwmColorizationColorChanged = 0x0320;

    private readonly ButtonLook look;
    // The look's always on top, in a field of its own: WinForms reads CreateParams in
    // Form's constructor already, before this constructor has set `look` (measured: a
    // crash there). False at that point, which is harmless: no window exists yet.
    private readonly bool onTop;
    // The background in the button's color, and lighter under the mouse.
    private Color background;
    private Color hoverBackground;
    // The scaling of the button's monitor: 1.5 at 150 %.
    private readonly float scale;
    // The button's monitor without its taskbar, in physical pixels; the button stays inside it.
    private readonly Rectangle area;
    // Where the user last dragged the button on this monitor, or null.
    private readonly Point? savedPlace;
    private readonly ToolTip toolTip = new();

    // While the left mouse button is down on the button: where the mouse and the button
    // were when it went down, and whether it has moved far enough to be a drag.
    private bool pressed;
    private bool dragging;
    private Point pressMouse;
    private Point pressPlace;

    // monitor: the monitor the button is on. place: where the user last dragged
    // it there, or null for just above the right end of the taskbar. look: its color,
    // size and always on top. menu: its right-click menu (AppMenu), disposed with the button.
    public FloatingButton(Screen monitor, Point? place, ButtonLook look, ContextMenuStrip menu)
    {
        Monitor = monitor;
        this.look = look;
        onTop = look.OnTop;
        scale = Dpi.ScaleOf(monitor);
        area = monitor.WorkingArea;
        savedPlace = place;

        Text = "tray-anywhere";
        // Its AutomationId in UI Automation, by which the GUI test finds it.
        Name = "FloatingButton";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        // Sizes are in this monitor's physical pixels already (see GridForm).
        AutoScaleMode = AutoScaleMode.None;
        SetColors();
        BackColor = background;
        toolTip.SetToolTip(this, $"Tray icons ({App.HotkeyName})");

        ContextMenuStrip = menu;

        ClientSize = new Size(Side, Side);
        // Made in the middle of its monitor; OnLoad moves it to its place (see GridForm).
        StartPosition = FormStartPosition.Manual;
        Location = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
    }

    // The monitor the button is on.
    public Screen Monitor { get; }

    // True when the button is in the top half of its monitor: the grid then opens below
    // it and the arrow points down. One rule for both, so they always agree.
    public bool OpensDown => OpensDownAt(Bounds, area);

    // The same rule for a button at these bounds on a monitor with this free area. The
    // grid uses it to place the button while the user drags the grid.
    public static bool OpensDownAt(Rectangle bounds, Rectangle area) =>
        bounds.Top + bounds.Height / 2 < area.Top + area.Height / 2;

    // A click: the left mouse button went down and up without a drag.
    public event Action? Clicked;

    // The user dragged the button to this place (its top-left corner).
    public event Action<Point>? Dragged;

    // The user is dragging the button: it has just moved. Only for the user's own drag,
    // not when the grid moves the button along, so the two never push each other around.
    public event Action? Moving;

    // Shown without taking the focus from the window the user is in.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            // Always on top, unless turned off for this monitor. Set here instead of with
            // TopMost: WinForms activates a TopMost window when it shows, which
            // ShowWithoutActivation is there to prevent.
            if (onTop)
            {
                createParams.ExStyle |= TopWindow.WsExTopmost;
            }
            return createParams;
        }
    }

    // As in GridForm: on the 100 % monitor the first move changes the window's scaling,
    // so move, then set the size, then place it again with that size.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Location = Place();
        ClientSize = new Size(Side, Side);
        Location = Place();
    }

    // The button's side in this monitor's pixels.
    private int Side => Px((int)look.Size);

    // The background from the button's color. Windows' accent color is read again each time.
    private void SetColors()
    {
        background = ButtonColors.Background(look.Color);
        hoverBackground = ButtonColors.Hover(background);
    }

    // Where the user last dragged the button, or else just above the right end of the
    // taskbar, where the clock is. Inside the monitor's free area either way, in case
    // the monitor has become smaller since.
    private Point Place()
    {
        var corner = new Point(area.Right - Width - Px(ScreenGap), area.Bottom - Height - Px(ScreenGap));
        return TopWindow.KeepInside(area, savedPlace ?? corner, Size);
    }

    // Windows may create it without its always-on-top flag, as it did the grid
    // (docs/research/winforms-dpi.md).
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (onTop)
        {
            TopWindow.KeepOnTop(this);
        }
    }

    // Windows' accent color changed (Settings > Personalization > Colors): a button in that
    // color takes the new one.
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmDwmColorizationColorChanged && look.Color == ButtonColor.Accent)
        {
            SetColors();
            BackColor = background;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            dragging = false;
            pressMouse = Cursor.Position;
            pressPlace = Location;
        }
    }

    // WinForms holds on to the mouse from press to release, so moves keep coming here
    // even when the mouse leaves the button (at the monitor's edge, where it stops).
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pressed)
        {
            return;
        }
        var mouse = Cursor.Position;
        var moved = new Size(mouse.X - pressMouse.X, mouse.Y - pressMouse.Y);
        // A small shake while clicking stays a click: Windows' own distance for a drag.
        var dragSize = SystemInformation.DragSize;
        if (!dragging && Math.Abs(moved.Width) <= dragSize.Width / 2 && Math.Abs(moved.Height) <= dragSize.Height / 2)
        {
            return;
        }
        dragging = true;
        Location = TopWindow.KeepInside(area, pressPlace + moved, Size);
        Moving?.Invoke();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            EndPress(mayClick: true);
        }
    }

    // The mouse was taken away during a press (another window took it): a drag still
    // keeps its new place, but it isn't a click. After a normal release WinForms lets go
    // of the mouse too, but OnMouseUp has ended the press by then.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndPress(mayClick: false);
    }

    // A press that moved the button saves its place; one that didn't is a click.
    private void EndPress(bool mayClick)
    {
        if (!pressed)
        {
            return;
        }
        pressed = false;
        if (dragging)
        {
            dragging = false;
            Dragged?.Invoke(Location);
        }
        else if (mayClick)
        {
            Clicked?.Invoke();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        BackColor = hoverBackground;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        BackColor = background;
    }

    // Drawn again at each move: a drag across the middle of the monitor turns the arrow.
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        Invalidate();
    }

    // The arrow in the middle, pointing to where the grid opens: a ^, or a v when the
    // grid opens below. Half as tall as it is wide, with round line ends; as much bigger
    // as the button is bigger than the small one.
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var grow = scale * (float)look.Size / (float)ButtonSize.Small;
        using var pen = new Pen(ArrowColor, ArrowLine * grow)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        var half = ArrowWidth * grow / 2;
        var x = ClientSize.Width / 2f;
        var y = ClientSize.Height / 2f;
        // How far the tip sits below the middle: above it for a ^, below it for a v.
        var tip = OpensDown ? half / 2 : -half / 2;
        e.Graphics.DrawLines(pen, [
            new PointF(x - half, y - tip),
            new PointF(x, y + tip),
            new PointF(x + half, y - tip),
        ]);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Rounded corners like Windows 11's own windows. Windows rounds windows with a
        // border by itself, but a borderless one only when asked.
        var round = DwmCornerRound;
        DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref round, sizeof(int));
    }

    // A size at 100 % scaling in this monitor's pixels.
    private int Px(int size) => (int)Math.Round(size * scale);

    protected override void Dispose(bool disposing)
    {
        var menu = ContextMenuStrip;
        base.Dispose(disposing);
        if (disposing)
        {
            menu?.Dispose();
            toolTip.Dispose();
        }
    }

    private const int DwmWindowCornerPreference = 33;
    private const int DwmCornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

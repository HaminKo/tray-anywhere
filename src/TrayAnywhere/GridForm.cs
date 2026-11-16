using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// The grid: the app icons in "Pinned" (shown on the taskbar) and "Overflow" (behind the
// arrow), with each icon's tooltip text on hover. It stays on top until its X, Escape,
// the hotkey or its floating button closes it, and can be dragged by its title bar within
// its monitor (owner's choice). A new grid is made each time it opens, laid out at the
// scale of the monitor it opens on.
internal sealed class GridForm : Form
{
    // Sizes at 100 % scaling, in pixels. The icon is the tray's own size; the tile is the
    // size of a button in Windows' overflow flyout (60 px at 150 %, measured).
    private const int IconSize = 16;
    private const int TileSize = 40;
    // Columns: as many as the longer section needs, within these limits.
    private const int MinColumns = 4;
    private const int MaxColumns = 6;
    private const int TitleHeight = 24;
    // Text height in pixels (9 pt at 100 %).
    private const int TextHeight = 12;
    // Inside the grid's edge, and between the grid and the screen's edge.
    private const int EdgeSpace = 8;
    private const int ScreenGap = 12;
    // Between the grid and the floating button it opens next to.
    private const int ButtonGap = 8;

    // Dark, like the taskbar here: its icon pictures carry its dark background (owner's choice).
    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TitleColor = Color.FromArgb(220, 220, 220);
    private static readonly Color ProblemColor = Color.FromArgb(160, 160, 160);
    private static readonly Color HoverBorder = Color.FromArgb(110, 110, 110);

    // The scaling of the grid's monitor: 1.5 at 150 %.
    private readonly float scale;
    private readonly Font titleFont;
    private readonly Font textFont;
    // ShowAlways: also while another window has the focus; the grid stays open then.
    private readonly ToolTip toolTip = new() { ShowAlways = true };
    // The grid's monitor without its taskbar, in physical pixels; the grid stays inside it.
    private readonly Rectangle area;
    // The floating button on the grid's monitor, or null if it has none (the main one).
    private readonly FloatingButton? button;
    // Where the user last dragged the grid, or null. Only on a monitor without a button:
    // elsewhere the grid is always next to its button (owner's choice).
    private Point? userPlace;
    // True while the user drags the grid by its title bar.
    private bool beingDragged;
    // The size of the grid without its title bar.
    private Size contentSize;
    // The section titles and the problem line. The grid draws them itself instead of
    // using Labels: WinForms resizes a Label's font when the window's DPI changes, and
    // that happens as the grid opens on the 100 % monitor (logged; see OnLoad).
    private readonly List<Caption> captions = [];
    // The tiles: the Pinned icons, then the Overflow icons, in the order of `shown`.
    private readonly List<IconTile> tiles = [];
    // The read the grid shows now.
    private TrayState shown = TrayState.Empty;

    // state: what to show. monitor: the monitor to open on. place: where the user last
    // dragged the grid on that monitor, or null. button: the floating button there, or
    // null. Without a place the grid opens next to the button, or else in the corner.
    // menu: the right-click menu of its background (AppMenu), disposed with the grid.
    public GridForm(TrayState state, Screen monitor, Point? place, FloatingButton? button, ContextMenuStrip menu)
    {
        Monitor = monitor;
        scale = Dpi.ScaleOf(monitor);
        area = monitor.WorkingArea;
        userPlace = place;
        this.button = button;
        // Sizes in pixels, so the text is the same size on every monitor at its scale.
        titleFont = new Font("Segoe UI Semibold", Px(TextHeight), GraphicsUnit.Pixel);
        textFont = new Font("Segoe UI", Px(TextHeight), GraphicsUnit.Pixel);

        Text = "tray-anywhere";
        // Its AutomationId in UI Automation, by which the GUI test finds it.
        Name = "Grid";
        // A small title bar with only an X; dragging it moves the grid.
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        // Every size here is already in physical pixels for this monitor: WinForms must
        // not scale them again.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Background;

        // A right click on the grid's background: Start with Windows and Exit. Right
        // clicks on tiles don't open it (see IconTile).
        ContextMenuStrip = menu;

        LayOut(state);
        ClientSize = contentSize;

        // Made in the middle of its monitor; OnLoad moves it to its place there. Not at a
        // corner: a grid dragged into a corner stops exactly there, and OnLoad needs a move.
        StartPosition = FormStartPosition.Manual;
        Location = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
    }

    // The monitor the grid was opened and laid out for.
    public Screen Monitor { get; }

    // True while the mouse is over the grid, its title bar too. Only the mouse counts
    // (owner's choice, To do 8): a grid left in front, after the hotkey, while the owner
    // is away, would otherwise keep reading every 2 s.
    public bool InUse => Bounds.Contains(Cursor.Position);

    // A click on a tile: its tray icon, the mouse button (Left or Right), and where a menu
    // the click opens belongs (see MenuPoint).
    public event Action<TrayItem, MouseButtons, Point>? IconClicked;

    // The user dragged the grid to this place (its top-left corner). On a monitor with a
    // button, the button has moved along.
    public event Action<Point>? Dragged;

    // Where the grid was when the user pressed on its title bar.
    private Point dragStart;

    // Runs once the window exists, just before it shows. WinForms first treats a new
    // window as being at the main monitor's scaling (150 %); on the 100 % monitor, the
    // first move then changes it to 100 % and Windows resizes the title bar (logged).
    // So: move, then set the size, then place it again with that size.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Location = Place();
        ClientSize = contentSize;
        Location = Place();
    }

    // Shows a newer read of the tray. With the same number of icons in each section and
    // the same problem line, the tiles stay and change only where an icon's picture or
    // tooltip did, so a tooltip being read or the hover border doesn't go away at every
    // refresh. Otherwise the grid is laid out again, on the same monitor: in its corner or
    // above its button it grows up and to the left, below its button down and to the left;
    // once dragged it keeps its top-left corner.
    public void ShowState(TrayState state)
    {
        TopWindow.KeepOnTop(this);
        if (state.Pinned.Count != shown.Pinned.Count || state.Overflow.Count != shown.Overflow.Count || state.Problem != shown.Problem)
        {
            LayOut(state);
            ClientSize = contentSize;
            Location = Place();
            // The section titles and the problem line are drawn in OnPaint.
            Invalidate();
            return;
        }
        var items = state.Pinned.Concat(state.Overflow).ToList();
        for (var i = 0; i < tiles.Count; i++)
        {
            UpdateTile(tiles[i], items[i]);
        }
        shown = state;
    }

    // Points a tile to the same icon in a newer read, and redraws it only if its picture
    // or tooltip changed. The tile keeps the newest item, so a click goes to the newest
    // button.
    private void UpdateTile(IconTile tile, TrayItem item)
    {
        // The pictures are PNG bytes: the same bytes, the same picture. No bytes and
        // bytes that aren't a picture both draw the generic icon.
        if (!(tile.Item.Picture ?? []).AsSpan().SequenceEqual(item.Picture ?? []))
        {
            tile.SetPicture(Picture(item, Px(IconSize)));
        }
        var text = TooltipText(item);
        if (text != toolTip.GetToolTip(tile))
        {
            SetText(tile, text);
        }
        tile.Item = item;
    }

    // The tile's tooltip, and the same text as its name in UI Automation, which screen
    // readers read and the GUI test finds tiles by.
    private void SetText(IconTile tile, string text)
    {
        toolTip.SetToolTip(tile, text);
        tile.AccessibleName = text;
    }

    // Where a menu opened by a click on this tile belongs: at the mouse if it is over the
    // grid, else (Enter, the menu key, the GUI test) at the tile's centre.
    private Point MenuPoint(IconTile tile)
    {
        if (InUse)
        {
            return Cursor.Position;
        }
        var bounds = tile.RectangleToScreen(tile.ClientRectangle);
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    // Where the user last dragged the grid; or else next to the floating button; or else
    // the bottom-right corner of the monitor's free area, just above its taskbar, where
    // Windows' own flyout opens. Inside the free area always, in case the grid has grown.
    private Point Place()
    {
        var corner = new Point(area.Right - Width - Px(ScreenGap), area.Bottom - Height - Px(ScreenGap));
        return TopWindow.KeepInside(area, userPlace ?? NextToButton() ?? corner, Size);
    }

    // Next to the floating button, where its arrow points: right edges lined up, just
    // above it, or just below it when the button is in the top half of the monitor.
    private Point? NextToButton()
    {
        if (button == null)
        {
            return null;
        }
        var b = button.Bounds;
        var top = button.OpensDown ? b.Bottom + Px(ButtonGap) : b.Top - Px(ButtonGap) - Height;
        return new Point(b.Right - Width, top);
    }

    // Where the button goes while the user drags the grid: the reverse of NextToButton.
    // Right edges lined up, just below the grid; or just above it when below would be in
    // the top half of the monitor, where the button opens the grid downwards.
    private Point ButtonPlace(FloatingButton b)
    {
        var below = new Rectangle(Right - b.Width, Bottom + Px(ButtonGap), b.Width, b.Height);
        var place = FloatingButton.OpensDownAt(below, area)
            ? new Point(below.X, Top - Px(ButtonGap) - b.Height)
            : below.Location;
        return TopWindow.KeepInside(area, place, b.Size);
    }

    // The user is dragging the button: the grid moves along, next to it.
    public void FollowButton() => Location = Place();

    // Windows raises these when the user presses on the title bar and lets go; only a
    // press that moved the grid makes a new place (a click to focus it doesn't).
    protected override void OnResizeBegin(EventArgs e)
    {
        base.OnResizeBegin(e);
        dragStart = Location;
        beingDragged = true;
    }

    // While the user drags the grid, its button moves along (owner's choice: the button
    // and the grid stay together on their monitor).
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (beingDragged && button != null)
        {
            button.Location = ButtonPlace(button);
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        beingDragged = false;
        if (Location == dragStart)
        {
            return;
        }
        if (button != null)
        {
            // Next to its button again: where the button had no room (at an edge of the
            // monitor), the grid moves a little to make room for it.
            Location = Place();
        }
        else
        {
            userPlace = Location;
        }
        Dragged?.Invoke(Location);
    }

    protected override void WndProc(ref Message m)
    {
        // While the user drags the grid, Windows asks with WM_MOVING where it may go: keep
        // it on its monitor. It is laid out for that monitor's scaling only.
        if (m.Msg == WmMoving)
        {
            var r = Marshal.PtrToStructure<Win32Rect>(m.LParam);
            var size = new Size(r.Right - r.Left, r.Bottom - r.Top);
            var place = TopWindow.KeepInside(area, new Point(r.Left, r.Top), size);
            r = new Win32Rect { Left = place.X, Top = place.Y, Right = place.X + size.Width, Bottom = place.Y + size.Height };
            Marshal.StructureToPtr(r, m.LParam, false);
            // Handled: Windows moves the window to the changed rectangle.
            m.Result = 1;
            return;
        }
        base.WndProc(ref m);
    }

    // Makes the tiles and texts for this read, in place of any made before, and works out
    // the grid's size (contentSize). The caller sets the window's size.
    private void LayOut(TrayState state)
    {
        // The tooltips first, so the ToolTip doesn't hold on to disposed tiles. Dispose
        // also takes a tile out of the grid's Controls.
        toolTip.RemoveAll();
        foreach (var tile in tiles)
        {
            tile.Dispose();
        }
        tiles.Clear();
        captions.Clear();
        shown = state;

        var columns = Math.Clamp(Math.Max(state.Pinned.Count, state.Overflow.Count), MinColumns, MaxColumns);
        var top = Px(EdgeSpace);
        top = AddSection("Pinned", state.Pinned, columns, top);
        top = AddSection("Overflow", state.Overflow, columns, top);
        var noIcons = state.Pinned.Count == 0 && state.Overflow.Count == 0;
        var problem = state.Problem ?? (noIcons ? "No tray icons found." : null);
        if (problem != null)
        {
            top = AddProblem(problem, columns, top);
        }
        contentSize = new Size(2 * Px(EdgeSpace) + columns * Px(TileSize), top + Px(EdgeSpace));
    }

    // A section: its title, then its icons in rows. Returns where the next part starts.
    // An empty section is left out.
    private int AddSection(string title, List<TrayItem> items, int columns, int top)
    {
        if (items.Count == 0)
        {
            return top;
        }
        var left = Px(EdgeSpace);
        var tileSize = Px(TileSize);
        var bounds = new Rectangle(left, top, columns * tileSize, Px(TitleHeight));
        captions.Add(new Caption(title, titleFont, TitleColor, bounds, TextFormatFlags.VerticalCenter));
        top += Px(TitleHeight);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var tile = new IconTile(item, Picture(item, Px(IconSize)), Background, HoverBorder, Math.Max(1, Px(1)))
            {
                Bounds = new Rectangle(left + i % columns * tileSize, top + i / columns * tileSize, tileSize, tileSize),
                // The section ("Pinned" or "Overflow") as the tile's AutomationId in UI
                // Automation, for the GUI test.
                Name = title,
            };
            SetText(tile, TooltipText(item));
            // tile.Item, not item: the tile gets the newest item at each refresh.
            tile.Click += (_, _) => IconClicked?.Invoke(tile.Item, MouseButtons.Left, MenuPoint(tile));
            tile.RightClick += () => IconClicked?.Invoke(tile.Item, MouseButtons.Right, MenuPoint(tile));
            tiles.Add(tile);
            Controls.Add(tile);
        }
        var rows = (items.Count + columns - 1) / columns;
        return top + rows * tileSize;
    }

    // A grey line under the icons saying what couldn't be read, wrapped to the grid's width.
    private int AddProblem(string problem, int columns, int top)
    {
        // Wrapped at the grid's width; measured and drawn with the same flags, so the
        // height fits.
        var width = columns * Px(TileSize);
        var height = TextRenderer.MeasureText(problem, textFont, new Size(width, 0), ProblemFlags).Height;
        var bounds = new Rectangle(Px(EdgeSpace), top + Px(EdgeSpace), width, height);
        captions.Add(new Caption(problem, textFont, ProblemColor, bounds, ProblemFlags));
        return bounds.Bottom;
    }

    // A size at 100 % scaling in this monitor's pixels.
    private int Px(int size) => (int)Math.Round(size * scale);

    // The icon at the grid's size, or Windows' generic program icon if there is no
    // picture. The pictures come in sizes from 16 to 64 px (measured); high-quality
    // scaling keeps the big ones clean when made smaller.
    private static Bitmap Picture(TrayItem item, int size)
    {
        var picture = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(picture);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var source = LoadPng(item.Picture) ?? GenericPicture(size);
        graphics.DrawImage(source, 0, 0, size, size);
        return picture;
    }

    // PNG bytes as a picture; null if there are none or they aren't a picture.
    private static Bitmap? LoadPng(byte[]? png)
    {
        if (png == null)
        {
            return null;
        }
        try
        {
            // A Bitmap read from a stream needs that stream for as long as it lives;
            // the copy doesn't.
            using var stream = new MemoryStream(png);
            using var loaded = new Bitmap(stream);
            return new Bitmap(loaded);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Bitmap GenericPicture(int size)
    {
        using var icon = SystemIcons.GetStockIcon(StockIconId.Application, size);
        return icon.ToBitmap();
    }

    // The tooltip: the icon's name, its lines split on "\r" or "\n" (both occur,
    // measured); for an icon with an empty name, its program's file name.
    private static string TooltipText(TrayItem item)
    {
        var lines = item.Name.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0)
        {
            return string.Join("\n", lines);
        }
        return item.ExePath != null ? Path.GetFileName(item.ExePath) : "(no name)";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        foreach (var caption in captions)
        {
            // NoPrefix: a "&" in an error message is text, not a keyboard shortcut.
            TextRenderer.DrawText(e.Graphics, caption.Text, caption.Font, caption.Bounds, caption.Color, caption.Flags | TextFormatFlags.NoPrefix);
        }
    }

    // Windows can drop the grid's always-on-top flag: opened through the hotkey (which lets
    // the app take the front), the window is created without it, though TopMost is set
    // (traced in To do 3 step B; docs/research/winforms-dpi.md). So the grid sets it again
    // once it shows, after Windows has activated and ordered it, and checks it at every
    // refresh (ShowState).
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        TopWindow.KeepOnTop(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // A dark title bar, to match the grid. Windows draws title bars light unless asked.
        var dark = 1;
        DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    protected override void Dispose(bool disposing)
    {
        var menu = ContextMenuStrip;
        base.Dispose(disposing);
        if (disposing)
        {
            menu?.Dispose();
            toolTip.Dispose();
            titleFont.Dispose();
            textFont.Dispose();
        }
    }

    // The problem line wraps at word ends.
    private const TextFormatFlags ProblemFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
    private const int WmMoving = 0x0216;
    private const int DwmUseImmersiveDarkMode = 20;

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    // A text the grid draws itself, in this rectangle, with this font and colour.
    private sealed record Caption(string Text, Font Font, Color Color, Rectangle Bounds, TextFormatFlags Flags);
}

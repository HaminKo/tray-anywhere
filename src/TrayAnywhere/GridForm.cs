using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// The grid: the app icons in "Pinned" (shown on the taskbar) and "Overflow" (behind the
// arrow), with each icon's tooltip text on hover. It stays on top until its X, Escape,
// the hotkey, its floating button or its settings gear closes it, and can be dragged by
// its title bar within its monitor (owner's choice). Its tiles can be dragged into another
// order within their section; App saves that order and sorts each read by it. A new grid
// is made each time it opens, laid out at the scale of the monitor it opens on.
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
    // The settings gear: a square as high as a section title, its symbol in this size.
    private const int GearSymbolHeight = 14;
    // The gear symbol in Segoe Fluent Icons, which ships with Windows 11.
    private const string GearSymbol = "";

    // Dark, like the taskbar here: its icon pictures carry its dark background (owner's choice).
    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TitleColor = Color.FromArgb(220, 220, 220);
    private static readonly Color ProblemColor = Color.FromArgb(160, 160, 160);
    private static readonly Color HoverBorder = Color.FromArgb(110, 110, 110);

    // The scaling of the grid's monitor: 1.5 at 150 %.
    private readonly float scale;
    private readonly Font titleFont;
    private readonly Font textFont;
    private readonly Font gearFont;
    // At the right end of the first line, just under the title bar's X. Windows draws that
    // title bar, so the gear can't go beside the X (To do 19).
    private readonly Button gear;
    // ShowAlways: also while another window has the focus; the grid stays open then.
    private readonly ToolTip toolTip = new() { ShowAlways = true };
    // The grid's monitor without its taskbar, in physical pixels; the grid stays inside it.
    private readonly Rectangle area;
    // The floating button on the grid's monitor, or null if it has none (by default the main one).
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
    // The sections shown, Pinned first; an empty one is left out.
    private readonly List<Section> sections = [];
    // The number of tiles in a row, the same in both sections.
    private int columns;
    // The tile the user drags, or null; and its place in its section when the drag began.
    private IconTile? dragged;
    private int draggedFrom;
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
        gearFont = new Font("Segoe Fluent Icons", Px(GearSymbolHeight), GraphicsUnit.Pixel);

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

        // A right click on the grid's background: the app's menu (AppMenu). Right
        // clicks on tiles don't open it (see IconTile).
        ContextMenuStrip = menu;

        // Its font is set in OnLoad, as WinForms changes a control's font when the
        // window's scaling changes (see OnLoad).
        gear = new Button
        {
            Text = GearSymbol,
            ForeColor = TitleColor,
            // Its AutomationId and its name in UI Automation (screen readers, the GUI test).
            Name = "Gear",
            AccessibleName = "Settings",
            // Not reached with Tab: the grid opens with the focus on its first icon, so
            // Enter right after the hotkey can't open the settings by mistake.
            TabStop = false,
        };
        IconTile.UseFlatLook(gear, Background, HoverBorder, Math.Max(1, Px(1)));
        gear.Click += (_, _) => SettingsClicked?.Invoke();
        Controls.Add(gear);

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

    // The user dragged a tile to another place: the section's icons in their new order.
    public event Action<TrayItemKind, List<TrayItem>>? Reordered;

    // A click on the settings gear. App closes the grid and opens the settings window.
    public event Action? SettingsClicked;

    // Where the grid was when the user pressed on its title bar.
    private Point dragStart;

    // Runs once the window exists, just before it shows. WinForms first treats a new
    // window as being at the main monitor's scaling (150 %); on the 100 % monitor, the
    // first move then changes it to 100 % and Windows resizes the title bar (logged).
    // So: move, then set the size, then place it again with that size. That change also
    // shrinks controls' fonts, though they are in pixels (measured for Labels,
    // docs/research/winforms-dpi.md): the gear gets its font after the move.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Location = Place();
        gear.Font = gearFont;
        ClientSize = contentSize;
        Location = Place();
    }

    // Shows a newer read of the tray. With the same number of icons in each section and
    // the same problem line, the tiles stay and change only where an icon's picture or
    // tooltip did, so a tooltip being read or the hover border doesn't go away at every
    // refresh. Otherwise the grid is laid out again, on the same monitor: in its corner or
    // above its button it grows up and to the left, below its button down and to the left;
    // once dragged it keeps its top-left corner. Not while the user drags a tile: a new
    // layout would take the tile from under the mouse; the next refresh comes within 2 s.
    public void ShowState(TrayState state)
    {
        if (dragged != null)
        {
            return;
        }
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
        var tiles = AllTiles();
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

    // Kept on its monitor while dragged: it is laid out for that monitor's scaling only.
    protected override void WndProc(ref Message m)
    {
        if (!TopWindow.KeepOnMonitor(ref m, area))
        {
            base.WndProc(ref m);
        }
    }

    // Makes the tiles and texts for this read, in place of any made before, and works out
    // the grid's size (contentSize). The caller sets the window's size.
    private void LayOut(TrayState state)
    {
        // The tooltips first, so the ToolTip doesn't hold on to disposed tiles. Dispose
        // also takes a tile out of the grid's Controls.
        toolTip.RemoveAll();
        foreach (var tile in AllTiles())
        {
            tile.Dispose();
        }
        sections.Clear();
        captions.Clear();
        shown = state;

        columns = Math.Clamp(Math.Max(state.Pinned.Count, state.Overflow.Count), MinColumns, MaxColumns);
        var top = Px(EdgeSpace);
        // The gear on the first line, right-aligned with the tiles. Its tooltip again here:
        // RemoveAll above took it too.
        var gearSize = Px(TitleHeight);
        gear.Bounds = new Rectangle(Px(EdgeSpace) + columns * Px(TileSize) - gearSize, top, gearSize, gearSize);
        toolTip.SetToolTip(gear, "Settings");
        top = AddSection("Pinned", TrayItemKind.Pinned, state.Pinned, top);
        top = AddSection("Overflow", TrayItemKind.Overflow, state.Overflow, top);
        var noIcons = state.Pinned.Count == 0 && state.Overflow.Count == 0;
        if (noIcons)
        {
            // No section title: the gear has the first line to itself, above the problem line.
            top += gearSize;
        }
        var problem = state.Problem ?? (noIcons ? "No tray icons found." : null);
        if (problem != null)
        {
            top = AddProblem(problem, top);
        }
        contentSize = new Size(2 * Px(EdgeSpace) + columns * Px(TileSize), top + Px(EdgeSpace));
    }

    // A section: its title, then its icons in rows. Returns where the next part starts.
    // An empty section is left out.
    private int AddSection(string title, TrayItemKind kind, List<TrayItem> items, int top)
    {
        if (items.Count == 0)
        {
            return top;
        }
        var bounds = new Rectangle(Px(EdgeSpace), top, columns * Px(TileSize), Px(TitleHeight));
        captions.Add(new Caption(title, titleFont, TitleColor, bounds, TextFormatFlags.VerticalCenter));
        var section = new Section(kind, top + Px(TitleHeight), []);
        sections.Add(section);

        foreach (var item in items)
        {
            var tile = new IconTile(item, Picture(item, Px(IconSize)), Background, HoverBorder, Math.Max(1, Px(1)))
            {
                Bounds = Slot(section, section.Tiles.Count),
                // The section ("Pinned" or "Overflow") as the tile's AutomationId in UI
                // Automation, for the GUI test.
                Name = title,
            };
            SetText(tile, TooltipText(item));
            // tile.Item, not item: the tile gets the newest item at each refresh.
            tile.Click += (_, _) => IconClicked?.Invoke(tile.Item, MouseButtons.Left, MenuPoint(tile));
            tile.RightClick += () => IconClicked?.Invoke(tile.Item, MouseButtons.Right, MenuPoint(tile));
            tile.DragMoved += () => MoveDragged(section, tile);
            tile.Dropped += () => Drop(section, tile);
            section.Tiles.Add(tile);
            Controls.Add(tile);
        }
        return section.Top + Rows(section) * Px(TileSize);
    }

    // The tiles of both sections, in the order they show.
    private List<IconTile> AllTiles() => sections.SelectMany(section => section.Tiles).ToList();

    private int Rows(Section section) => (section.Tiles.Count + columns - 1) / columns;

    // Where the tile at this place in a section goes: in rows, left to right.
    private Rectangle Slot(Section section, int place)
    {
        var tileSize = Px(TileSize);
        return new Rectangle(Px(EdgeSpace) + place % columns * tileSize, section.Top + place / columns * tileSize, tileSize, tileSize);
    }

    // The place in a section under a point, or the nearest one: a tile dropped outside its
    // section stays in it, as Windows decides what is Pinned and what is Overflow.
    private int NearestPlace(Section section, Point point)
    {
        var tileSize = Px(TileSize);
        var column = Math.Clamp((point.X - Px(EdgeSpace)) / tileSize, 0, columns - 1);
        var row = Math.Clamp((point.Y - section.Top) / tileSize, 0, Rows(section) - 1);
        return Math.Min(row * columns + column, section.Tiles.Count - 1);
    }

    // A dragged tile moved with the mouse: it takes the place under its centre, and the
    // section's other tiles move to make room, as in Windows' overflow flyout.
    private void MoveDragged(Section section, IconTile tile)
    {
        if (dragged == null)
        {
            dragged = tile;
            draggedFrom = section.Tiles.IndexOf(tile);
            // No tooltip popping up over the tile on the move.
            toolTip.Active = false;
        }
        var b = tile.Bounds;
        var place = NearestPlace(section, new Point(b.X + b.Width / 2, b.Y + b.Height / 2));
        section.Tiles.Remove(tile);
        section.Tiles.Insert(place, tile);
        for (var i = 0; i < section.Tiles.Count; i++)
        {
            if (section.Tiles[i] != tile)
            {
                section.Tiles[i].Bounds = Slot(section, i);
            }
        }
    }

    // The user let go of a dragged tile: it goes into its place, and a new order goes out
    // to be saved. Also called when the grid closes during a drag (Escape): then nothing
    // more happens.
    private void Drop(Section section, IconTile tile)
    {
        dragged = null;
        if (Disposing || IsDisposed)
        {
            return;
        }
        toolTip.Active = true;
        var place = section.Tiles.IndexOf(tile);
        tile.Bounds = Slot(section, place);
        // Tab, the arrow keys and UI Automation (screen readers, the GUI test) go through
        // the tiles in the order of the grid's controls: make it the order they show in.
        var tiles = AllTiles();
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].TabIndex = i;
            Controls.SetChildIndex(tiles[i], i);
        }
        if (place != draggedFrom)
        {
            Reordered?.Invoke(section.Kind, section.Tiles.Select(t => t.Item).ToList());
        }
    }

    // A grey line under the icons saying what couldn't be read, wrapped to the grid's width.
    private int AddProblem(string problem, int top)
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
        DarkTitleBar.Use(Handle);
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
            gearFont.Dispose();
        }
    }

    // The problem line wraps at word ends.
    private const TextFormatFlags ProblemFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

    // A text the grid draws itself, in this rectangle, with this font and colour.
    private sealed record Caption(string Text, Font Font, Color Color, Rectangle Bounds, TextFormatFlags Flags);

    // Pinned or Overflow: where its first row of tiles starts, and its tiles in the order
    // they show.
    private sealed record Section(TrayItemKind Kind, int Top, List<IconTile> Tiles);
}

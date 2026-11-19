using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// One icon in the grid: its picture on a flat button. A Button, so Tab, the arrow keys
// and Enter work without extra code. A left click or Enter raises Click; a right click
// or the menu key raises RightClick. Pressed and moved, it follows the mouse within the
// grid (DragMoved), and the release drops it (Dropped) instead of clicking.
internal sealed class IconTile : Button
{
    // Windows sends this after a right click, and for the menu key or Shift+F10 on the
    // focused tile. Unhandled, it goes up to the grid, which would show its Exit menu; a
    // right click on a tile belongs to the tray icon.
    private const int WmContextMenu = 0x007B;

    public IconTile(TrayItem item, Bitmap picture, Color background, Color hoverBorder, int borderWidth)
    {
        Item = item;
        Image = picture;
        BackColor = background;
        FlatStyle = FlatStyle.Flat;
        // A border shows the icon under the mouse. A lighter background would show the
        // taskbar pictures' own dark background as a square.
        FlatAppearance.BorderSize = borderWidth;
        FlatAppearance.BorderColor = background;
        FlatAppearance.MouseOverBackColor = background;
        FlatAppearance.MouseDownBackColor = background;
        MouseEnter += (_, _) => FlatAppearance.BorderColor = hoverBorder;
        MouseLeave += (_, _) => FlatAppearance.BorderColor = background;
    }

    // The tray icon this tile stands for, from the latest read (the grid replaces it at
    // each refresh, so a click goes to the newest button). The attribute: WinForms asks
    // every settable property of a control to say whether its form designer saves it
    // (build error WFO1000); this app has no designer files.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TrayItem Item { get; set; }

    public event Action? RightClick;

    // The user drags the tile: it has just moved with the mouse.
    public event Action? DragMoved;

    // The user let go of the tile after dragging it.
    public event Action? Dropped;

    // True from a left press until its release.
    private bool pressed;
    // True once the press has moved far enough to be a drag.
    private bool dragging;
    // Where in the tile the left button went down; it stays under the mouse during a drag.
    private Point pressPoint;
    // True while the release that ends a drag runs: it isn't a click.
    private bool ignoreClick;

    // Shows a new picture. The old one was made for this tile only.
    public void SetPicture(Bitmap picture)
    {
        var old = Image;
        Image = picture;
        old?.Dispose();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            dragging = false;
            pressPoint = e.Location;
        }
    }

    // As FloatingButton's drag, but from the message's place in the tile rather than
    // Cursor.Position, so the GUI test can drag with messages and not the real mouse.
    // WinForms holds on to the mouse from press to release, so moves keep coming here
    // even when the mouse is outside the tile.
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pressed)
        {
            return;
        }
        var moved = new Size(e.X - pressPoint.X, e.Y - pressPoint.Y);
        // A small shake while clicking stays a click: Windows' own distance for a drag.
        var dragSize = SystemInformation.DragSize;
        if (!dragging && Math.Abs(moved.Width) <= dragSize.Width / 2 && Math.Abs(moved.Height) <= dragSize.Height / 2)
        {
            return;
        }
        if (!dragging)
        {
            dragging = true;
            // In front of the other tiles while it passes over them.
            BringToFront();
        }
        // The press point under the mouse again, the whole tile inside the grid.
        var area = Parent!.ClientSize;
        var place = Location + moved;
        Location = new Point(Math.Clamp(place.X, 0, area.Width - Width), Math.Clamp(place.Y, 0, area.Height - Height));
        DragMoved?.Invoke();
    }

    // Button runs its click inside OnMouseUp when the mouse is over it at the release, as
    // it is over a dragged tile: ignoreClick keeps that from clicking the tray icon.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        ignoreClick = dragging;
        base.OnMouseUp(e);
        ignoreClick = false;
        if (e.Button == MouseButtons.Left)
        {
            EndPress();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        if (!ignoreClick)
        {
            base.OnClick(e);
        }
    }

    // The mouse was taken away during a press (another window took it): a drag ends
    // where the tile is. After a normal release WinForms lets go of the mouse too, but
    // OnMouseUp has ended the press by then.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndPress();
    }

    private void EndPress()
    {
        pressed = false;
        if (dragging)
        {
            dragging = false;
            Dropped?.Invoke();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmContextMenu)
        {
            // Not during a drag: a drag never clicks the tray icon.
            if (!dragging)
            {
                RightClick?.Invoke();
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        // A Button doesn't dispose its Image; this one was made for this tile only.
        var picture = Image;
        base.Dispose(disposing);
        if (disposing)
        {
            picture?.Dispose();
        }
    }
}

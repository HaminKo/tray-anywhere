using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// One icon in the grid: its picture on a flat button. A Button, so Tab, the arrow keys
// and Enter work without extra code. A left click or Enter raises Click; a right click
// or the menu key raises RightClick.
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

    // Shows a new picture. The old one was made for this tile only.
    public void SetPicture(Bitmap picture)
    {
        var old = Image;
        Image = picture;
        old?.Dispose();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmContextMenu)
        {
            RightClick?.Invoke();
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

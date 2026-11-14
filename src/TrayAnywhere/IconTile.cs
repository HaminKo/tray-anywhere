using System.Drawing;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// One icon in the grid: its picture on a flat button. A Button, so Tab, the arrow keys
// and Enter work without extra code.
internal sealed class IconTile : Button
{
    // Windows sends this after a right click. Unhandled, it goes up to the grid, which
    // would show its Exit menu; a right click on a tile belongs to the tray icon.
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

    // The tray icon this tile stands for.
    public TrayItem Item { get; }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmContextMenu)
        {
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

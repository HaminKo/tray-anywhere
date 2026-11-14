using System.Drawing;

namespace Tray;

// Which part of the tray an item comes from. The overflow (the icons behind the
// arrow) is not read yet.
public enum TrayItemKind
{
    // An app's icon shown on the taskbar.
    Pinned,
    // A Windows button in the tray: network, volume, clock, the overflow arrow and so on.
    System,
}

// One button in the tray.
// Name: the tooltip text, often several lines; can be empty.
// Bounds: where it is on screen, in physical pixels (not scaled by the monitor's
// scaling), as measured on build 26300: see docs/research/uia-tray.md.
public sealed record TrayItem(string Name, TrayItemKind Kind, Rectangle Bounds);

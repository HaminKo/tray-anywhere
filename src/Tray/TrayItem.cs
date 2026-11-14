using System.Drawing;

namespace Tray;

// Which part of the tray an item comes from.
public enum TrayItemKind
{
    // An app's icon shown on the taskbar.
    Pinned,
    // A Windows button in the tray: network, volume, clock, the overflow arrow and so on.
    System,
    // An app's icon in the overflow, behind the "Show Hidden Icons" arrow.
    Overflow,
}

// One button in the tray.
// Name: the tooltip text, often several lines; can be empty. After an app changes its
// tooltip, Windows gives its first tooltip, a space, then the current one.
// Bounds: where it is on screen, in physical pixels (not scaled by the monitor's
// scaling), as measured on build 26300: see docs/research/uia-tray.md. Empty for
// overflow items while the flyout is closed.
public sealed record TrayItem(string Name, TrayItemKind Kind, Rectangle Bounds);

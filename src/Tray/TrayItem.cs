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

// How IconOwners found an icon's program. Nothing links an icon to its program exactly,
// so both ways are a guess; Name is the safer one.
public enum OwnerFoundBy
{
    // Not found: ExePath is null.
    None,
    // The icon's name points to the program (its first tooltip, file name or description).
    Name,
    // The icon's place in the tray, between icons found by name.
    Order,
}

// One button in the tray.
// Name: the tooltip text, often several lines; can be empty. After an app changes its
// tooltip, Windows gives its first tooltip, a space, then the current one.
// Bounds: where it is on screen, in physical pixels (not scaled by the monitor's
// scaling), as measured on build 26300: see docs/research/uia-tray.md. Empty for
// overflow items while the flyout is closed.
// ExePath, OwnerFoundBy: the program behind an app icon, filled in by IconOwners.Find.
// Picture: the icon as PNG bytes, or null. Taskbar icons: drawn from the taskbar as it
// looks now. Overflow icons: Windows' stored picture of the icon, filled in by
// IconOwners.Find; it can be older than the real one.
public sealed record TrayItem(
    string Name,
    TrayItemKind Kind,
    Rectangle Bounds,
    string? ExePath = null,
    OwnerFoundBy OwnerFoundBy = OwnerFoundBy.None,
    byte[]? Picture = null);

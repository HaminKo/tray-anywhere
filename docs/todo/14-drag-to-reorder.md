# To do 14: Drag to reorder icons (medium)

## Problem / Goal
"Drag to reorder icons." The grid shows the icons in the tray's order. Goal: drag a tile to another place in the grid, and the grid keeps that order across refreshes and restarts. Example: drag Steam to the front of Overflow; it is still first after the next refresh and after tray-anywhere restarts.

## Read first
- src/TrayAnywhere/GridForm.cs: `LayOut`, `AddSection` (tiles in rows), and the title-bar drag (how the grid itself is moved).
- src/TrayAnywhere/IconTile.cs: a Button; a left click forwards to the real icon, so a drag must not count as a click.
- src/TrayAnywhere/Settings.cs: settings.json, where the order would be kept.
- src/Tray/TrayItem.cs: `Name` and `ExePath`, what could identify an icon from one read to the next.

## Approach
- The grid's order only: the real tray keeps Windows' order (no documented way to set it; writing Windows' `UIOrderList` would change the owner's taskbar).
- Within each section: which icons are Pinned or Overflow stays Windows' choice.
- settings.json keeps a list of keys per section. The key is the open part: names change (tooltips with counts), and some icons have no program. Suggested: the program (`ExePath`), else the name; icons the list doesn't know go after the known ones, in tray order.
- A drag starts once the mouse moves farther than `SystemInformation.DragSize` with the button down; the drop moves the tile; no click is forwarded.
- GUI test step: drag the test's own icon tile with posted mouse messages (never the real mouse), check the order after a refresh and after a restart of the tested app (its own settings file).

## Ask the user
- Grid only, within each section? Suggested: yes.
- Should dragging a tile between Pinned and Overflow also move the real icon (Windows' setting)? Suggested: no; that writes Windows' settings, and Windows' taskbar settings page does it already.
- Where do new icons go? Suggested: at the end of their section.
- A way back to Windows' order? Suggested: "Reset icon order" in the right-click menu.

## Test
- Claude: the GUI test step above.
- User: drag a few icons with the mouse; a drag doesn't click the icon; a plain click still does; the order after Exit and a restart.

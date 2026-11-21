# To do 19: A settings gear in the grid (small)

## Problem / Goal
"Should we add a setting button to the tray as well? Like on the top right?" (owner, 2026-11-21, after To do 17 step C). Today the settings window opens only from "Settings..." in the right-click menu of a floating button or of the grid's background. A gear at the grid's top right would open it in one click, and show that there are settings without knowing the right-click menu.

Claude's advice (2026-11-21): little gain for one user who knows the menu, and settings change rarely while the grid opens many times a day; the owner chose to keep it as a Someday to-do: a gear left of the X; a click closes the grid and opens Settings on that monitor. Closing the grid matters: the grid is always on top and the settings window isn't, so a grid over the monitor's middle would cover the window (left open from To do 17 step A's review).

## Read first
- src/TrayAnywhere/GridForm.cs: the constructor (`FormBorderStyle.FixedToolWindow`: the X is Windows' own title bar button, not a control of the app), `AddSection` and the captions drawn in OnPaint (the "Pinned" line).
- src/TrayAnywhere/App.cs: `OpenSettings(monitor)`, `NewMenu`, and how the grid is closed (as its X and Escape do).
- src/TrayAnywhere/IconTile.cs: a flat dark button with a hover border, to look like.
- docs/research/winforms-dpi.md (sizes in physical pixels, `Px`).

## Approach
- Windows' title bar can't take a button of the app's own beside its X without drawing the title bar itself (a much bigger change). So the gear goes inside the grid at the top right, on the "Pinned" line, right-aligned, just under the X: a small flat button with the gear symbol (Segoe Fluent Icons / Segoe MDL2 Assets glyph E713, both ship with Windows 11), dark like the tiles, tooltip "Settings".
- A click: the grid closes as with its X, then App.OpenSettings for the grid's monitor (the same as "Settings..." in its menu).
- Its WinForms Name as AutomationId for the GUI test.

## Ask the user
- Where exactly: inside the grid on the "Pinned" line under the X, or a self-drawn title bar with the gear beside the X? Suggested: inside, on the "Pinned" line (no own title bar to keep working with dragging and scaling).
- Gear symbol or the word "Settings"? Suggested: the gear, with a tooltip.
- Keep "Settings..." in the right-click menus too? Suggested: yes (the floating buttons have no other way).

## Test
- Claude: a GUI test step: open the grid, Invoke the gear (UI Automation): the grid closes and the settings window shows in the middle of that monitor, with that monitor picked. Screenshots of the grid (the look changes).
- User: a click on the gear on each monitor; the gear's look at 100 % and 150 %.

# To do 3: Grid window on a secondary monitor, with click forwarding (large)

## Problem / Goal
The actual app: a small popup grid of the tray icons on the monitor the mouse is on, opened by a global hotkey or a small floating button. Sections "Pinned" and "Overflow", like the real taskbar. Each icon shows a picture and its tooltip text; left and right clicks go to the real tray icon.

## Read first
- docs/research/uia-tray.md and the findings of To do 2 (which overflow, icon and right-click methods worked).
- docs/decisions.md: hotkey + button, mouse's monitor, refresh strategy, settings file.
- src/Tray (the reading code) and src/TrayDump.

## Approach
- New WinForms project `src/TrayAnywhere/` that references src/Tray; per-monitor DPI aware (PerMonitorV2).
- Grid: a borderless, topmost form with one FlowLayoutPanel per section; closes on Escape or when it loses focus.
- Hotkey: `RegisterHotKey` (Win32). Floating button: a tiny borderless topmost form, draggable, its position saved in the settings file.
- Refresh as decided: read everything on opening; while open, re-read all icons (visible and overflow) every 2 s. The two reads can fail separately: a failed overflow read keeps the taskbar's icons (as TrayDump does). `ReadOverflowItems(openIfMissing: true)` at start-up and when the grid opens, `false` on the 2 s refresh. Opening the flyout blocks for ~0.15 s (up to 3 s on failure) and moves the keyboard focus to the taskbar: check that the grid still gets the focus when that happens as it opens. A read can fail when an icon goes away or explorer restarts during it (UI Automation's ElementNotAvailableException, or the tray not found): keep the last list and try again at the next tick.
- Icons: `TrayItem.Picture` from To do 2 step C (taskbar icons drawn live, overflow icons Windows' stored snapshot via `IconOwners.Find`), with a generic icon when it is null. The pictures come in sizes from 16 to 64 px: draw them all at one size. A full refresh with pictures and programs took about 150 ms (measured), so read on a background thread, not the UI thread.
- Clicks: `TrayClicks.LeftClick(item)` / `RightClick(item)` from To do 2 step D, with an item from the latest read; they throw InvalidOperationException if the icon is gone, so show nothing and refresh. Where a menu opens is the app's choice (most at the mouse). Steam's menu doesn't take the focus, so it stays open until clicked away.
- Suggested steps: A. window and hotkey with a plain text list; B. icons and sections; C. clicks; D. floating button; E. refresh.

## Ask the user
- Which hotkey? Suggested: Ctrl+Alt+T, if nothing on this PC uses it.
- Where does the floating button start? Suggested: one per secondary monitor, just above the right end of its taskbar.
- Show system icons (network, volume, clock)? Suggested: no; Windows already gives secondary taskbars a clock.
- Tooltip text under each icon, or only on hover? Suggested: on hover, to keep the grid small.

## Progress
Choices (owner, 2026-11-14): docs/decisions.md "The grid app". Three steps, one chat each.
- [x] Step A: the grid (src/TrayAnywhere, ~400 lines): one copy only, a read at start, the hotkey Alt+`, the grid with "Pinned" and "Overflow" tiles and tooltips, reads on a background thread. Owner tested it, 2026-11-14.
  - Changed after the owner's first test: Alt+` instead of Ctrl+Alt+T (awkward); the grid stays on top until its X, Escape or Alt+` (Windows' small dark title bar), is dragged within its monitor only, and reopens where it was last dragged on that monitor (in memory until step C saves it).
  - Why it stays on its monitor and draws its own text: docs/research/winforms-dpi.md.
  - The owner's Session Inbox popups sit in the bottom-right corner of both external monitors, above the grid.
  - Open notes (review): focus after a flyout flash at opening not confirmed; the tooltip shows an icon's first and current tooltip (Session Inbox); a second Alt+` during a read is ignored; Alt+` is the key left of 1 on US-style layouts only.
  - Step B must know: `IconTile.Item` is the icon to click; tiles swallow WM_CONTEXTMENU (the background has the Exit menu); icons kept after a failed read point to old buttons, so a click on one must do nothing and refresh; TrayReader runs one read at a time. Rebuilding the tiles in an open grid is safe (no text on them; titles are drawn in OnPaint), but the grid must never change monitor.
- [x] Step B: clicks and the 2 s refresh (src/TrayAnywhere, ~160 lines). Owner tested it, 2026-11-14.
  - Left and right clicks on a tile (also Enter and the menu key) go to the real icon on a background thread; a gone icon does nothing and the tray is read again at once. The refresh changes a tile in place when only its picture or tooltip changed, and lays the grid out again when icons come or go (in its corner it grows up and to the left; once dragged it keeps its top-left corner).
  - Fixed while testing: Steam showed no menu until a forwarded click let other programs come to the front (decisions.md); the grid lost "always on top" when opened by the hotkey, so `GridForm.KeepOnTop` sets it again after it shows and at each refresh (docs/research/winforms-dpi.md).
  - Steam's menu opens off to the side on the 150 % monitor: Steam's own bug; a README limitation and To do 7.
  - Step C must know: a window opened by a click on a floating button may also lose "always on top" (only the hotkey path was traced); check the buttons' own flag the same way. Review notes left open: a re-layout during a drag can make the grid jump; after a re-layout the keyboard focus and hover border are gone until the mouse moves.
- [x] Step C: the floating buttons and settings.json (src/TrayAnywhere: FloatingButton, Settings, TopWindow; ~570 lines with comments, planned ~300). Owner tested it, 2026-11-14.
  - One button per secondary monitor (dark, ^ arrow, Exit on right-click, rebuilt when monitors change); a click or Alt+` on another monitor moves the grid there. Choices: docs/decisions.md "Floating buttons and settings.json".
  - Changed after the owner's tests: the button alone says where the grid is on its monitor. The grid always opens at it, where its arrow points (v and below in the monitor's top half), and the two move together while either is dragged. The grid keeps a dragged place of its own only on the main monitor.
  - Checked with window probes (docs/research/winforms-dpi.md "the floating buttons"): sizes, on-top flags, placement; a title-bar drag can be faked with WM_ENTERSIZEMOVE, SetWindowPos and WM_EXITSIZEMOVE (useful for To do 4).
  - Review notes left open: a monitor change during the 150 ms read opens the grid for the old monitor; a scaling or taskbar-size change sends no "display changed" event, so the buttons keep their old size and limits until a restart (a Someday to-do if it happens); the grid's visible frame is ~7 px inside its window rectangle (estimated), so its edge sits slightly left of the button's.

## Test
- Claude: build and format checks; the GUI test once To do 4 exists.
- User: opens the grid on each external monitor with the hotkey and the button, clicks and right-clicks 3-4 icons, and checks that a newly started tray app shows up.

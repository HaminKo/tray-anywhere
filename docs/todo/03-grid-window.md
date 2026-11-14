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
- Refresh as decided: read everything on opening; while open, re-read the visible icons every 2 s.
- Icons: the method that worked in To do 2 (Windows' snapshot or the exe's icon), with a generic icon as the last fallback.
- Suggested steps: A. window and hotkey with a plain text list; B. icons and sections; C. clicks; D. floating button; E. refresh.

## Ask the user
- Which hotkey? Suggested: Ctrl+Alt+T, if nothing on this PC uses it.
- Where does the floating button start? Suggested: one per secondary monitor, just above the right end of its taskbar.
- Show system icons (network, volume, clock)? Suggested: no; Windows already gives secondary taskbars a clock.
- Tooltip text under each icon, or only on hover? Suggested: on hover, to keep the grid small.

## Test
- Claude: build and format checks; the GUI test once To do 4 exists.
- User: opens the grid on each external monitor with the hotkey and the button, clicks and right-clicks 3-4 icons, and checks that a newly started tray app shows up.

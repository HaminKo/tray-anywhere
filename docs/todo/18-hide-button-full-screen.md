# To do 18: Hide the floating button while a full-screen program is in front (small)

## Problem / Goal
The floating button is always on top, so it shows over full-screen video and games on its monitor (a known limitation in the README). To do 17 adds a switch to turn "always on top" off, but then any window over the button's corner hides it, a maximized one too. Windows' own taskbar instead hides only while a full-screen program is in front. Suggested by Claude in To do 17 (owner: "go with your suggestions", 2026-11-20); not seen as a problem yet.

## Read first
- src/TrayAnywhere/FloatingButton.cs, App.cs (`MakeButtons`), ToastMover.cs and SteamMenu.cs (their WinEvent hooks).
- docs/research/notifications.md (Windows hides toasts while a full-screen program runs on the main monitor).

## Approach
- A WinEvent hook on the foreground window changing (`EVENT_SYSTEM_FOREGROUND`) and on its moves: when the window in front covers a button's whole monitor (its bounds, not only the free area), that button hides; it shows again when another window comes to the front.
- Always on; no setting.

## Ask the user
- Hide or only drop below the full-screen window? Suggested: hide.
- Also for a maximized window? Suggested: no, only one that covers the taskbar too.

## Test
- Claude: a GUI test step with a test window made full-screen on a secondary monitor: the button there hides, the others stay; it shows again when the window closes.
- User: a full-screen video on a secondary monitor.

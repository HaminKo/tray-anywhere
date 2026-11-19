# To do 13: Notification balloons and toasts from tray apps (large)

## Problem / Goal
"Notification balloons and toasts from tray apps." Windows 11 shows notifications bottom right on the main monitor only, and shows a tray app's old-style balloon as a toast too (known Windows behaviour, not measured here). While the owner works on an external monitor they are easy to miss. Goal, to confirm with the owner: see those notifications on the monitor the owner works on. Example: Discord shows a message toast on the laptop screen while the mouse is on an external monitor.

Not measured yet: whether an app can read or move Windows' toasts at all on this build.

## Read first
- CLAUDE.md "The owner's hard rules" (no third-party software).
- docs/decisions.md 2026-11-16 "Steam's misplaced tray menu is moved ..." (a Steam-only mover; no mover for other programs' windows).
- src/TrayAnywhere/SteamMenu.cs (a WinEvent hook that watches and moves another program's window).
- Microsoft docs: UserNotificationListener (Windows.UI.Notifications.Management).

## Approach
Research first, the cheapest test first. A test toast can be sent from Windows PowerShell with the built-in `Windows.UI.Notifications` classes.
- Move Windows' own toast to the mouse's monitor, like Steam's menu: a probe first finds the toast window's class and process while a toast shows, then tries `SetWindowPos`. It belongs to the shell, so it may not move or may snap back. Widens the "Steam-only mover" decision: needs the owner's OK.
- Read toasts with UserNotificationListener (documented; Windows asks the user's permission once) and show a copy in tray-anywhere. Unknowns: whether it works for an app without package identity, and whether the Windows SDK projection it needs (a `net10.0-windows10.0.x` target, downloaded by the .NET SDK) is allowed under the no-third-party rule.
- Read the toast's text through UI Automation, as the tray code does. A copy only.
- Toasts come from every app, and nothing links a toast to its tray icon, so "only tray apps" may not be possible.

## Ask the user
- Move Windows' real toast to the monitor with the mouse, or show a copy in tray-anywhere? Suggested: move the real one (its buttons keep working), if a probe shows it can be moved.
- Every app's notifications, or only tray apps'? Suggested: every app's (nothing tells which toast belongs to a tray icon).
- Is the Windows SDK projection allowed? Suggested: decide only if moving the real toast fails.

## Test
- Claude: a test toast sent from PowerShell appears on the monitor with the mouse (a probe; the GUI test gets a step if the app changes).
- User: a real toast (e.g. a Discord message) while working on an external monitor; its buttons and a click on it still work.

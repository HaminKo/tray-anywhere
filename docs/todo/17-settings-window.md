# To do 17: A settings window (medium)

## Problem / Goal
"Move later items up": the settings window, until now under "Later, only if the user asks" with the note "once there are about four settings (To do 16 found one)".

Today the settings are all in the right-click menu of the floating buttons and the grid: Start with Windows, Floating button on this monitor (per secondary monitor), Reset icon order (an action, not a setting). settings.json also keeps where the buttons and grids were dragged and the icon order, set by dragging. Everything else is fixed in code: the hotkey (Alt+`), the refresh times, the icon size.

The 2026-11-19 decision (To do 16, the owner) says a window pays off only with about four settings, or one that must be typed (such as a hotkey). So the build session starts with the list of settings it would hold.

## Read first
- docs/decisions.md 2026-11-19 "No settings window: one menu item turns the floating button off per monitor" (what was rejected: the hotkey, refresh times and icon size as settings), and 2026-11-14 "The grid app: Alt+`, dark, ..." (the hotkey stays fixed until it clashes).
- docs/archive/16-settings-window.md: the same question asked last time, and its answers.
- src/TrayAnywhere/AppMenu.cs (the menu items), Settings.cs (settings.json: `ButtonsOff` and the rest), Autostart.cs, Hotkey.cs, App.cs (`InUseRefresh`, `UnusedRefresh`), GridForm.cs (tile size).

## Approach
- First the list of settings. A new one is a value in settings.json, read where the code has a constant now.
- A plain WinForms dialog, dark like the grid, opened from a "Settings..." item in the right-click menu; a change applies at once and is saved to settings.json. The menu items it replaces leave the menu, so each setting lives in one place.
- If the list stays at two or three on/off settings, the 2026-11-19 decision stands: menu items, and this to-do goes back to Later.

## Ask the user
- Which settings would the window hold, beyond the two in the menu now? Suggested: none new until one is missed; if one is, the hotkey is the likely first (the only one that must be typed).
- Build it now with the two existing settings? Suggested: no; wait until there is a third setting or one that must be typed, as decided on 2026-11-19.
- Keep the menu items as well as the window? Suggested: no, one place per setting; keep Exit and Reset icon order in the menu.

## Test
- Claude: a GUI test step: open the window from the menu (UI Automation), change each setting, check the tested app's own settings file and the effect.
- User: each setting by hand, and again after a restart.

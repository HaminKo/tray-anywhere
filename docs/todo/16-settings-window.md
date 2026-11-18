# To do 16: A settings window (medium)

## Problem / Goal
"A settings window (v1 uses a small right-click menu on the floating button, plus the settings file)." Today the right-click menu holds Start with Windows and Exit, and settings.json holds only where the buttons and the main monitor's grid were dragged. Everything else is fixed in code: the hotkey (Alt+`), the refresh times, a button on every secondary monitor, the icon size.

A window for Start with Windows alone adds code for nothing the menu doesn't do already; it pays off only with settings to put in it. So the goal, to confirm with the owner: which settings.

## Read first
- src/TrayAnywhere/AppMenu.cs (the menu), Settings.cs (settings.json), Hotkey.cs (the fixed Alt+`), App.cs (`InUseRefresh`, `UnusedRefresh`), GridForm.cs (tile size).
- docs/decisions.md: the refresh entries, and "Tests run at the app's real speed" (a refresh setting must not become a test-only shortcut).

## Approach
- First the list of settings; each is a small change of its own: a value in settings.json, read where the code has a constant now.
- A plain WinForms dialog, dark like the grid, opened from a "Settings..." item in the right-click menu; a change applies at once and is saved to settings.json.
- With three settings or fewer, menu items instead of a window.

## Ask the user
- Which settings? Suggested: the hotkey, and the floating button on or off per secondary monitor; nothing else until it is missed.
- A window or menu items? Suggested: menu items while there are three settings or fewer.
- Reset to defaults? Suggested: no button; the docs say to delete settings.json.

## Test
- Claude: a GUI test step: open the settings (UI Automation), change each setting, check the tested app's own settings file and the effect.
- User: each setting by hand, and again after a restart.

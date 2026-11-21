# To do 22: "Reset icon order" in the settings window too (small)

## Problem / Goal
"shouldn't reset icon order also be in settings?" (owner, 2026-11-21, while testing To do 21). Today "Reset icon order" is only in the right-click menus of the floating buttons and the grid, so it is found only by right-clicking; the settings window is where the owner now looks for what changes the app. Example: the owner opens Settings..., sees "Reset icon order" next to the other app-wide settings, clicks it, and the grid (open or next opened) shows Windows' own order again; the button is greyed while no order has been dragged.

## Read first
- src/TrayAnywhere/App.cs: `ResetIconOrder` (empties the saved order, refreshes an open grid), `NewMenu`, `OpenSettings` (how callbacks reach the settings window).
- src/TrayAnywhere/AppMenu.cs: the menu item, which stays.
- src/TrayAnywhere/Settings.cs: `ResetOrder`, `PinnedOrder`, `OverflowOrder` (empty: the tray's order).
- src/TrayAnywhere/SettingsForm.cs: the hotkey row from To do 21 (`defaultHotkey`, greyed while the hotkey is the default; `MakeFlat`), `ShowSettings`, `AddRow`.
- src/GuiTest/Program.cs: `ResetIconOrder` (the menu's check, `SavedOrderLength`), `HotkeyInSettings` (a settings-window step).

## Approach
- A flat "Reset icon order" button in the settings window, in the app-wide part under the hotkey row, made like the hotkey's Default button (`MakeFlat`). A click calls App's `ResetIconOrder` through a callback, as the menu does.
- Greyed while there is no dragged order (both lists empty), so the window also shows whether the order was changed; `ShowSettings` sets it, and the window shows it again when it is activated (an order dragged in the grid meanwhile).
- The right-click menus keep their "Reset icon order".
- README: one line in the settings paragraph.

## Ask the user
- Where in the window: under the hotkey row, or a row of its own at the very bottom? Suggested: its own row at the bottom, after the hotkey's note.
- Greyed while there is no dragged order? Suggested: yes, like Default.
- Ask before resetting ("Reset the icon order?")? Suggested: no: the menu item doesn't ask either, and an order is quickly dragged again.

## Test
- Claude: build and format; the GUI test, in a settings-window step: with a saved order in settings.json the button isn't greyed, a click empties the order in settings.json and greys it; with no order it starts greyed. The menu's existing check stays.
- User: drag an icon in the grid, open Settings... (the button isn't greyed), click it (the grid shows Windows' order again, the button greys), and the menu's Reset icon order still works.

# To do 21: Change the hotkey in the settings window, with a way back to the default (medium)

## Problem / Goal
"I also want you to be able to change the hotkeys in the settings, with a go back to default option" (owner, 2026-11-21, after To do 19). Today the hotkey that opens and closes the grid is Alt+`, a constant in the code (App.HotkeyName, the Register call in App's constructor). Example: the owner sets Ctrl+Alt+Space in the settings window; from then on, also after a restart, Ctrl+Alt+Space opens the grid and Alt+` does nothing; "Default" brings Alt+` back.

Earlier decisions said no to this twice (docs/decisions.md, 2026-11-14 "Hotkey Alt+`": "it moves to the settings file only if it ever clashes"; and 2026-11-19 "No settings window", replaced by To do 17: "a key-capture box is ~100 lines and only testable by hand"). Since then the settings window exists (To do 17), so "a second place to look" no longer holds; the size and the hand-only test of the key capture still do. The owner asks for it now.

## Read first
- src/TrayAnywhere/Hotkey.cs: `Register` (RegisterHotKey; false when another program has the keys), `Dispose` (UnregisterHotKey). One hotkey id per window.
- src/TrayAnywhere/App.cs: `HotkeyName`, the constructor (`hotkey.Register(Hotkey.Alt, Keys.Oemtilde)`, `HotkeyTaken`, `useHotkey` false for the GUI test's copy).
- src/TrayAnywhere/Program.cs: the "already running" and "already used by another program" messages (it quits today when Alt+` is taken).
- src/TrayAnywhere/SettingsForm.cs: the rows (`layout.Controls.Add`), `ShowSettings`, `ScaleTo`; how a change goes out (callbacks like `setButtonOn`).
- src/TrayAnywhere/Settings.cs: how a value with a default is kept (`ButtonLooks`, `MainButtonOn`).
- docs/decisions.md: "Hotkey Alt+`" (why Alt+`, keys measured as free or taken on this PC) and the GUI test's `--gui-test` entry (the test copy registers no hotkey; the test posts WM_HOTKEY).
- docs/local.md "Monitors": hotkeys measured free and taken on this PC.

## Approach
- settings.json keeps the hotkey (for example `"Hotkey": "Ctrl+Alt+Space"`); missing means the default, Alt+`.
- In the settings window, a "Hotkey:" row: a box showing the current keys ("Alt+`"), a click on it waits for the next key combination ("Press the new keys..."), and a "Default" button. A combination needs at least one of Ctrl, Alt, Shift (with another), Win; Escape cancels. While the box waits, the app's own hotkey is let go, so pressing the current keys reaches the box.
- A new hotkey is tried with RegisterHotKey before the old one goes: if another program has it, the old one stays and the window says so next to the box. The same at start: a saved hotkey that is taken no longer quits the app (today's message) but starts without a hotkey and says which keys are taken; the floating buttons still work.
- The key's name as the keyboard shows it ("`" rather than "Oemtilde"): from Windows (GetKeyNameText or MapVirtualKey), to be measured in the build session.
- The README's and the messages' "Alt+`" become the hotkey as set.

## Ask the user
- Only the one hotkey (open and close the grid)? Suggested: yes, the app has only that one.
- Any keys (a box that records the next key combination), or a short list to pick from (Alt+`, Ctrl+Alt+Space, Win+Alt+Space, ...; radio buttons like the button sizes)? The list is smaller and the GUI test can pick from it; the box allows any keys but is tested only by hand. Suggested: the box, as asked; the list if the plan comes out large.
- A hotkey that another program already has: keep the old one and say so in the window? Suggested: yes.
- At start, a saved hotkey that is now taken: start without a hotkey and say so, instead of quitting? Suggested: yes, the buttons still work.
- Allow a key without Ctrl, Alt or Win (for example F9, or Shift+F9)? Suggested: function keys alone yes, letters and digits only with Ctrl, Alt or Win (else typing that letter anywhere opens the grid).
- Split in two steps (A: the hotkey from settings.json, re-registering and "Default"; B: the key-capture box)? Suggested: decide after the plan's size; one step if under ~300 changed lines.

## Test
- Claude: build and format; the GUI test opens the settings window, checks the row shows "Alt+`" by default, writes another hotkey into the test's settings.json and checks the window shows it after a restart, and checks "Default" brings back Alt+` in the window and in settings.json. The test copy registers no hotkey (it runs beside the owner's), and the test never uses the real keyboard, so the key capture itself and the real keys stay a hand test.
- User: set a new hotkey, press it on each monitor (the grid opens there), restart the app (it still works, the old Alt+` doesn't), try keys another program has (the window says so), then "Default" (Alt+` works again).

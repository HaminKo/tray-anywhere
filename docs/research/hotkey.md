# The hotkey: key names, registering, and testing the key capture

How the app names, takes and records its hotkey (To do 21). Code: src/TrayAnywhere/HotkeyKeys.cs, Hotkey.cs, and the hotkey row in SettingsForm.cs.

## Key names (measured 2026-11-21, this PC, US keyboard layout)
A PowerShell probe called Windows' `MapVirtualKey` and `GetKeyNameText` for 33 keys.
- `GetKeyNameText` (with the scan code from `MapVirtualKey(vk, MAPVK_VK_TO_VSC_EX)`): good for character keys (`` ` ``, `A`, `1`, `-`, `[`, `\`, `'`, `,`) and for Space, F1 to F12, Enter, Tab, Backspace, Esc. Wrong or empty for the rest: the arrow keys, Home, End, Page Up/Down, Insert and Delete came out as the number pad's ("Num 4", "Num 8", "Num 9", "Num 3", "Num 7", "Num 1", "Num 0", "Num Del"), because `MAPVK_VK_TO_VSC_EX` doesn't mark them as extended keys; F13 and F24 gave nothing; Pause gave "Right Control"; Print Screen gave "Sys Req".
- `MapVirtualKey(vk, MAPVK_VK_TO_CHAR)`: the character for character keys (`` ` `` for VK_OEM_3, `A`, `1`, `-`, `'`, `,`), 0 for F9, the arrows, Page Up, Pause and Print Screen, and the control characters 13, 9 and 8 for Enter, Tab and Backspace. The number pad's keys give the top row's characters (`0`, `*`, `/`, `+`, `.`), so they can't be named by their character.
- Used: the character for a key above space that isn't on the number pad; else WinForms' `Keys` name, with plain names for Enter, Backspace, Page Up and Page Down (`Keys` has two names for each).

## Registering
- `RegisterHotKey` with the same window and id as a hotkey already registered keeps the old one too (Microsoft's documentation). So a change unregisters first, registers the new keys, and registers the old ones again if that fails.
- Keys another program registered with `RegisterHotKey` never reach the settings window as key presses (Windows hands them to that program), so the box can't see them. The "another program uses those keys" note then shows only when `RegisterHotKey` refuses keys the window did see. Measured in the owner's hand test of To do 21 (1 try): with the box waiting, Win+E opened File Explorer and the box kept its keys.
- Win+`, measured as taken on 2026-11-14, was free on 2026-11-21: in the owner's hand test it reached the box and became the hotkey. So it can't stand for "keys another program has" in a test. A probe (`RegisterHotKey` on its own thread, let go at once; error 1409 is "already registered") found Win+E, Win+R, Win+D, Win+L and Win+Shift+F23 taken: Windows' own shortcuts, so very likely held by Windows itself (Windows can't be asked who holds a hotkey, only whether it is taken).

## Testing the key capture (measured, To do 21)
- The GUI test posts WM_KEYDOWN to the settings window; WinForms passes it to `ProcessCmdKey` as for a real key. Ctrl, Alt and Shift can't be sent along: WinForms reads them from the keyboard's state (`Control.ModifierKeys`), not from the message. So the test sends only keys that need no modifier (A, refused; F9, taken; Escape) and writes Ctrl+Alt+Space into settings.json to check how it is shown.
- A WinForms label with empty text gets, in UI Automation, the name of the label before it: the note under the hotkey read "Hotkey:" (1 run). So the note is never empty: while idle it says how the box works.
- In settings.json, F9 alone was written as `"Hotkey": { "Modifiers": "None", "Key": "F9" }`; the default is not written at all.

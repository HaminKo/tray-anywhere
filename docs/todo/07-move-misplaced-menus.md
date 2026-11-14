# To do 7: Move a tray app's menu that opens away from the mouse next to it (medium)

## Problem / Goal
"After a forwarded click, move a tray app's menu that opens away from the mouse or off the grid's monitor next to the mouse" (owner's go-ahead, 2026-11-14). Example: on the right monitor (150 %, top-left corner 2560,-865), a right click on Steam's tile in the grid opens Steam's menu 1280 px right of and 433 px above the mouse; with the grid in that monitor's right half the menu is entirely off-screen (measured, 3 of 3 clicks). It is Steam's own bug: its menu lands at mouse + monitor's top-left corner x (scale - 1), so it opens at the mouse on the 100 % monitor and on the laptop screen (measured, 2 of 2 on the 100 % monitor).

Also seen: OneDrive opens its window above the real icon on the laptop screen (To do 2), and so did Malwarebytes' tray view once (logged in To do 3 step B). Whether those should move is open (see below).

## Read first
- docs/research/uia-tray.md "Steam's menu didn't open" and "Where Steam's menu opens" (the measurements and the logger used).
- docs/decisions.md "A forwarded click lets any program come to the front".
- src/TrayAnywhere/App.cs `ClickIcon`; src/Tray/TrayClicks.cs.

## Approach
- First a spike of a few minutes: with Steam's menu open on the right monitor, move it with `SetWindowPos` from a script; the owner checks that the hover highlight and a click on a menu item still work. If they don't, stop and keep the known limitation.
- If they do: after a forwarded click, watch for about 1 s for a new visible top-level window of any program (Steam's menu belongs to steamwebhelper, not steam.exe). If it lands on the grid's monitor far from the mouse, or not fully on any screen, move it so its corner is at the mouse, inside the monitor's free area, opening away from the edges as a menu does. Win32 `EnumWindows`, `GetWindowRect`, `SetWindowPos`; the code goes in src/TrayAnywhere (it moves windows, it doesn't read the tray).
- The hard part: telling the click's window apart from unrelated windows that appear in that second (the logger saw other programs' popups come and go). Perhaps only small popup windows, or only windows that appear within ~0.5 s.
- Then the README's line about Steam's menu under "Known limitations" goes.

## Ask the user
- Which windows to move: only ones that land far from the mouse on the grid's monitor or off-screen (Steam's case), or also windows that open on the laptop screen (OneDrive, Malwarebytes)? Suggested: only the first; decide on the second after seeing it work.

## Test
- Claude: build and format checks; the spike's result in docs/research/uia-tray.md.
- User: right-click Steam's tile with the grid at the left and at the right of the right monitor: the menu opens at the mouse and its items work. On the top-left monitor nothing changes; Discord's menu still opens at the mouse; OneDrive's window still opens where it did.

# To do 6: When no icons are hidden, read an empty overflow instead of failing (small)

## Problem / Goal
"When no icons are hidden (no arrow, maybe no overflow window), read an empty overflow instead of failing." Example: every tray icon is turned on in Settings > Personalization > Taskbar > Other system tray icons, then Explorer restarts. Windows probably shows no "Show Hidden Icons" arrow then, and Explorer may never create the overflow window. `Taskbar.ReadOverflowItems` throws ("Found no tray button named Show Hidden Icons" or "Found no window with class TopLevelWindowForOverflowXamlIsland"), TrayDump exits 1, and the app's refresh would fail on every read, though "nothing hidden" is a normal state.

Not seen on this PC (12 hidden icons, 2026-11-14); raised by the code review of To do 2 step B. Unknown: whether the arrow really disappears, and whether the overflow window exists, when nothing is hidden.

## Read first
- docs/research/uia-tray.md "Spike step B" (the overflow window, the fallback that opens the flyout once, the arrow found by its name).
- src/Tray/Taskbar.cs: `ReadOverflowItems`, `OpenAndCloseOverflow`.

## Approach
- First measure: turn every icon on, restart Explorer, then check whether the arrow is there and whether the overflow window exists (a read-only probe, before and after).
- If there is no arrow and no window: return an empty list instead of throwing. Telling "nothing hidden" apart from "the tree changed" is the open part: perhaps no arrow on the taskbar means nothing hidden, while an arrow without a window keeps the error.

## Ask the user
- May the build session turn every icon on for the test and back off afterwards? Suggested: yes, the user turns them back off by hand (Settings, same page).

## Test
- Claude: TrayDump with every icon on, after an Explorer restart: exit code 0, "0 in the overflow", no flash.
- User: the same, then all icons back to their old setting, and TrayDump lists the overflow again.

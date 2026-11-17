# To do 9: An icon matched by order can take the picture of a program without a tray icon (medium)

## Problem / Goal
"An icon matched by order can take the picture of a program that has no tray icon" (owner, 2026-11-14, after To do 3 step B). Example, measured once: with VLC running but showing no tray icon, Copilot's overflow icon (empty name, matched by order) got VLC's picture and program, in TrayDump and so in the grid. Icons with empty names are matched by their place among Windows' stored tray entries whose programs run; a running program that has an entry but no icon right now shifts the order.

## Read first
- docs/research/uia-tray.md "Spike step C" (how matching works) and "Pictures between reads" (the VLC case).
- src/Tray/IconOwners.cs `Find`; src/Tray/IconSettings.cs.

## Approach
- Research first: what tells an entry whose program runs but shows no icon apart from one with an icon? Ideas: Windows' stored entry fields (a last-seen time or a flag), counting icons against candidates between two name matches, the program's windows.
- If nothing reliable: show the generic picture for an order match when the counts don't add up, instead of a possibly wrong one.

## Ask the user
- When unsure, a generic picture or the best guess? Suggested: the generic picture; a wrong picture is worse than none.

## Test
- Claude: TrayDump with VLC running (no tray icon) and without: Copilot keeps its own picture or gets the generic one, never VLC's.
- User: the grid with VLC running shows no VLC picture on another app's icon.

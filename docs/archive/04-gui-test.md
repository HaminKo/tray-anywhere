# To do 4: GUI test and screenshots (medium)

## Problem / Goal
Once the grid window exists (To do 3), Claude needs a way to see it: a test that starts the app, opens the grid and checks what it shows, plus screenshots Claude can read.

## Read first
- The gui-test skill ("Setting it up").
- src/TrayAnywhere from To do 3.

## Approach
- Follow the gui-test skill's setup for a desktop window. Drive the app with UI Automation, which is already in the project, so nothing new is needed.
- Screenshot the grid on a secondary monitor into a gitignored folder.
- Add the commands to CLAUDE.md "Build specifics" and docs/local.md.

## Ask the user
- ~~Which monitor for test screenshots? Suggested: whichever the mouse is on, as in the app.~~ Answered (owner, 2026-11-14, Claude's suggestion): each secondary monitor, opened through its button, one file per monitor (or one by name). Not the main monitor: only the mouse picks it, and the test never moves the mouse.
- Answered the same way (owner, Claude's suggestions): the test shows a tray icon of its own for the refresh and the clicks, never clicking the owner's apps; it stops at once if tray-anywhere is running and never closes it; the app gets a `--settings PATH` option, UI Automation names (grid, button, tiles with their section and tooltip) and a title on the hotkey's hidden window.

## Progress
- Step A (done, tested by the owner 2026-11-14): the GUI test, `dotnet run --project src/GuiTest`, 40 checks in about 8 s, posted messages and UI Automation only. ~690 lines in src/GuiTest (planned ~350; not trimmed), ~40 in the app: `--settings PATH` (other arguments refused), AutomationIds and tile names, the hotkey window's title.
  - The test starts the copy of the app next to it (a ProjectReference) and helpers live in AppWindow.cs (one window's messages), TestedApp.cs (finding windows), Checks.cs (Check, WaitUntil, WaitFor).
  - For step B: open a grid with `WaitForGrid` (it gives the front back); a menu opened by a posted message doesn't show once the app has been in front, so Exit is only tested in a fresh app (docs/research/gui-testing.md).
- Step B (done, tested by the owner 2026-11-14): screenshots, `dotnet run --project src/GuiTest -- screenshots [DISPLAYn]` (src/GuiTest/Screenshots.cs, ~75 lines): the grid with its button per secondary monitor, cropped to the grid's visible frame, grey around them (owner, Claude's suggestion), PNGs in `$env:TEMP\tray-anywhere-gui\`, paths printed.
  - The title bar is drawn inactive (`WM_NCACTIVATE` 0) so every run gives the same picture; without it, it varied between runs (docs/research/gui-testing.md). winforms.md added to the gui-test skill.

## Test
- Claude: the test passes and a screenshot shows the grid with icons.
- User: runs the test command once.

# GUI testing the WinForms app

The GUI test (src/GuiTest, To do 4) starts the real app and works it through messages posted to the app's own windows and UI Automation, never the real mouse or keyboard. What worked, what didn't, and measurements. Windows 11 build 26300, .NET SDK 10.0.404, three monitors (docs/local.md).

## 2026-11-14: tools (measured)
- Nothing to install: UI Automation (`System.Windows.Automation`, through `<UseWPF>`), WinForms (`NotifyIcon`, `Screen`) and Win32 calls all ship with Windows and .NET. No test framework: xUnit and MSTest are NuGet packages, which the project doesn't use; a console program with a small `Check` helper prints one line per check and exits 0 or 1.
- A `ProjectReference` from the test (an Exe) to the app (a WinExe) copies `TrayAnywhere.exe`, its .dll, `.deps.json` and `.runtimeconfig.json` (and Tray.dll) into the test's bin folder. The test starts that copy: `dotnet run --project src/GuiTest` always builds it first, and the app's own bin folder isn't locked while the test runs.
- The test is per-monitor DPI aware (`Application.SetHighDpiMode(PerMonitorV2)`), so `GetWindowRect`, `Screen` and UI Automation give physical pixels, as the app uses.

## 2026-11-14: run time and results (measured)
- 40 checks; 7.2 to 8.7 s by the test's own clock in most runs, up to 12.2 s while the PC was busier; 9.5 and 11.5 s wall time for `dotnet run --project src/GuiTest` with an up-to-date build (2 runs).
- After the Exit fix below: 42 of 44 runs passed. Two failed once each, neither again:
  - "the grid stays open after clicks on its icons: it closed". That check then asked UI Automation for the grid once, without the retries the waits have; possibly a missed lookup (not shown: in the next 8 runs UI Automation found the grid every time, and the window handle said open). It now checks the grid's window handle (`IsWindowVisible`).
  - "a dragged grid takes its button along, below it: not within 5 s". The expected button place was worked out from the grid's size before the drag; if another app's icon came or went just then, a refresh laid the grid out at another size (inferred, not shown). The check now measures the grid and the button after the drag, and the waits now say what they found when they time out. Then 10 of 10 passed.
- Most of the time is waiting for the app: two starts, and the test icon appearing and going at the 2 s refresh. Since To do 8 the refresh is every 10 s with the mouse off the grid, so these two waits take about 10 s each; see "the slower refresh in the test" below.
- Shown to fail: with the button's top-half rule flipped in the app (`OpensDownAt`), the test stopped at "it is below the button, right edges lined up" with the grid's and the button's rectangles.
- ~~With tray-anywhere already running, it stops at once with a message, exit code 1.~~ Since To do 10 it runs beside it; see "beside the everyday copy" below.

## 2026-11-14: the test's own tray icon (measured)
- A WinForms `NotifyIcon` in a console program (pumping its messages with `Application.DoEvents`) appeared in the overflow (a new exe; Windows' default here) and got `MouseClick` with `Left` for TrayDump's `click` (UI Automation Invoke) and `Right` for `rclick` (ShowContextMenu), each also as MouseDown, MouseUp and Click. One run each.
- So the test checks the grid's refresh and the forwarded clicks on an icon of its own, and never clicks the owner's apps.

## 2026-11-14: driving the windows (measured)
- Posted `WM_LBUTTONDOWN`/`UP` click a floating button (as in To do 3 step C); UI Automation `Invoke` clicks a tile (WinForms runs its Click); a posted `WM_CONTEXTMENU` reaches a tile's right-click; `WM_HOTKEY` posted to the app's message-only hotkey window (found by its title with `FindWindowEx(HWND_MESSAGE, ...)`; since To do 10 the one of the test copy's process among those of that title, as the everyday copy has one too) opens the grid on the mouse's monitor; a posted `WM_KEYDOWN` Escape reaches the grid's `ProcessCmdKey`; a posted `WM_SYSCOMMAND SC_CLOSE` (what the X sends) closes it; `WM_ENTERSIZEMOVE`, `SetWindowPos`, `WM_EXITSIZEMOVE` drag it (To do 3 step C). All passed in the first test run.
- **The grid takes the foreground.** After a posted click on a button, the grid was the foreground window (4 of 4, probe): `GridForm`'s `Activate()` succeeded. ~~Probably because the owner wasn't using the PC then.~~ Replaced by the test runs: only the **first** grid opening after each start of the app took the front (5 of 5 runs; the later openings in the same run didn't), so it looks like the one-time right Windows gives a newly started program (inferred; the rules aren't documented in detail). The window in front at the start was an Explorer window, not VS Code.
- So the test gives the front back to the window that had it when the app started, after each grid opening (`TestedApp.GiveFrontBack`). `SetForegroundWindow` from the test returned true, and the user's window was in front again within 200 ms (2 of 2 runs; not yet at the moment the call returned). Otherwise a key typed during the test would go to the grid, where Space or Enter clicks the focused tile: a real tray icon.
- **The right-click menu from a posted `WM_CONTEXTMENU`** (probe, polling the app's visible windows every 10 ms):
  - Fresh app, never in front: the button's menu opened within 15 to 37 ms and stayed until Escape, 10 of 10 (5 with the menu-key place -1, 5 with a real point).
  - The grid in front: its menu opened, 4 of 4.
  - After the grid closed and the foreground went back to VS Code: the button's menu never showed, 4 of 4. Matches test run 3, where it failed the same way after the grid had closed.
  - Test runs 1 and 2: the grid's menu didn't show after a restart (the button's did, after grids). Not explained; possibly the grid then couldn't take the front (inferred).
  - So WinForms' menus depend on the app's activation history, which posted messages don't fake. A real right-click activates the app first, so the user isn't affected. The test checks Exit only in a freshly started app, on its button, before any grid opens; the grid's Exit is the owner's hand test.
  - To do 5: after an item in the button's menu was clicked through UI Automation's Invoke, the menu opened again from another posted `WM_CONTEXTMENU`, three times in a row (Start with Windows on, off, then Exit), in a freshly started app with no grid opened: 3 of 3 test runs. The test waits for the old menu to go first, in case WinForms runs an item's Click before it closes the menu (as its code is remembered; not measured). Start with Windows reads as a UI Automation Toggle (on/off), as WinForms gives a `CheckOnClick` item.

## 2026-11-14: Start with Windows in the test (measured)
- The app writes to the test's own key, HKCU\Software\tray-anywhere-gui\Run (`--run-key`; since To do 11 `--registry-key Software\tray-anywhere-gui`, see "Startup apps in the test" below), which the test deletes at the start and end. After 3 runs the key was gone and Windows' Run key had no tray-anywhere value, as before.
- 49 checks (9 new); 17.8, 13.1 and 13.0 s by the test's clock (3 runs, the first right after a build), against 7.2 to 12.2 s before. Not measured how much of that the new checks take (estimated: under a second, three menu openings and two registry writes); the rest varies with how busy the PC is (see "run time" above).

## 2026-11-14: screenshots (measured)
- `dotnet run --project src/GuiTest -- screenshots [DISPLAYn]` (src/GuiTest/Screenshots.cs) starts the app with no saved places (each button in its corner, the grid above it), and on each secondary monitor posts a click on the button, waits for the grid with tiles, and saves `grid-DISPLAY5.png` in `$env:TEMP\tray-anywhere-gui\`.
- The picture: `Graphics.CopyFromScreen` of the box around the grid's visible frame and the button (both always on top, so nothing covers them). The visible frame from `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`: `GetWindowRect` includes Windows' invisible border, 9 px at 150 % and 7 px at 100 % on the right (measured in the pictures). The rest of the box (the gap, the strip beside the button) is painted grey, so a window behind them doesn't show; the rounded corners still show a few pixels of what's behind. Rejected: `PrintWindow` per window (two pictures to put together, and DWM's frame may not be drawn the same).
- A wait of 0.5 s after the tiles exist, for Windows' fade-in and the painting. Not measured how short it could be.
- **The title bar** differed between runs, so the pictures did:
  - The app's first grid takes the front and the test gives it back: inactive title (grey text, plain x). The next grid (moved to the other monitor) showed an active title (white text, red X) though `GetForegroundWindow` was the user's window, but not in every run.
  - A grid opened and closed first, so every picture is of a later grid: DISPLAY5 active in 1 of 4 runs and inactive in 3, DISPLAY6 in 4 different files.
  - A fresh start of the app per monitor: the second start's grid kept the front (the grid was the foreground window at the picture, 3 of 3 runs, while the owner was in Chrome; the first start's grid didn't). Not looked into further: the screenshots start the app once, and the GUI test opens no grid after its restart. The truly active grid gave the same file as "active though not in front".
  - `WM_NCACTIVATE` TRUE sent to the grid before the picture: DISPLAY5 active 5 of 5, DISPLAY6 inactive 4 of 5. `WM_NCACTIVATE` FALSE: both files the same in 5 of 5 runs (md5), title inactive. Kept: FALSE, then 0.2 s for the redraw. The keyboard stays with the user's window either way.
- Pictures: DISPLAY5 (150 %) 397 x 392 px, 19.7 KB, about 210 tokens; DISPLAY6 (100 %) 265 x 263 px, 12.7 KB, about 95 tokens (tokens estimated as width x height / 750).
- Run time: 3.6 to 5.0 s by its own clock in most of ~20 runs, once 6.8 s; 9.1 and 9.6 s wall time for `dotnet run` with an up-to-date build.
- Errors: a monitor without a button (`screenshots DISPLAY1`) or an unknown argument prints a message, exit code 1.
- The pictures show the owner's real tray icons, so they differ whenever those change (a run after the review: a fourth pinned icon and a badge had appeared).
- Not handled (none seen; from the code review): the real mouse resting over the grid or the button (a tooltip, a hover look), another always-on-top window or a notification over them, a machine so busy that 0.5 s isn't enough. Any of these changes the picture; take it again.

## 2026-11-15: the slower refresh in the test (measured, To do 8)
- The grid refreshes every 2 s only while the mouse is over it, else every 10 s. The test never moves the mouse, so its icon shows at the 10 s refresh: after 10.7 s, and the tile went 8.9 s after the icon did (1 run). The two waits were raised from 8 to 15 s.
- A new check: the icon must take over 5 s to show (halfway between the 2 s and 10 s rates). The test reads the real cursor position at each look, and if the mouse was over the grid at any of them, the check is skipped instead, since the app then reads every 2 s. A mouse that crosses the grid between two looks (50 ms apart) could still make it fail (not seen).
- 50 checks (1 new), 29.0 s by the test's clock (1 run), against 13 to 18 s before.

## 2026-11-15: beside the everyday copy (measured, To do 10)
- The test starts the app with `--gui-test` (its own one-copy lock, no Alt+`). With the everyday copy running from publish/, it passed twice: 51 checks (1 new: the everyday copy still runs at the end), 26.1 and 25.7 s by the test's clock. Both copies' buttons were on screen, overlapping in DISPLAY6's corner; the test finds windows and posts messages by the test copy's process id, so they didn't mix up.
- The everyday copy afterwards: the same process id, and its two buttons at the same physical places and sizes before and after the second run (2516,-92 32x32 on DISPLAY6, its corner; 6352,1119 48x48 on DISPLAY5, its saved place), read with UI Automation and `GetWindowRect`.
- Pitfall when measuring from PowerShell: a script that isn't per-monitor DPI aware gets scaled coordinates from both UI Automation's BoundingRectangle and `GetWindowRect`, and different ones in different calls (the DISPLAY6 button read 3774,-138 48x48 in the first call, 2516,-92 32x32 in later ones: 1.5 times, the main monitor's 150 % against that monitor's 100 %). Why they changed between calls: not looked into. After `SetThreadDpiAwarenessContext(-4)` (per-monitor aware v2) both gave the physical places every time.
- `-- screenshots` with the everyday copy running stops with a message, exit code 1 (1 run).
- Not tested by Claude: the owner using the everyday copy (Alt+`, its button) while a test runs.

## 2026-11-15: Startup apps in the test (measured, To do 11)
- The app now takes `--registry-key Software\tray-anywhere-gui` and writes below it to Run and Explorer\StartupApproved\Run, as below Windows' CurrentVersion key; the test still deletes the whole key at the start and end.
- 55 checks (4 new: a "turned off" (03 and a FILETIME) counts as off and a click deletes it; a "turned on" (02 and zeros) counts as on and the click that unticks deletes it). Passed in 3 of 4 runs: 31.5 and 30.1 s by the test's clock (the third run's time wasn't kept). Windows' own Run and StartupApproved values stayed as they were in all 4 (the test's check, and a watcher logging them every 0.3 s).
- The failed run, the first after the build: "one of them is on DISPLAY5: none is", at the first button check, right after both buttons were found. That check comes before the Start with Windows step, in code this step didn't change; cause not looked into (seen once).

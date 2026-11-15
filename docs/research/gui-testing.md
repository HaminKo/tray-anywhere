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
- Most of the time is waiting for the app: two starts, and the test icon appearing and going at the 2 s refresh.
- Shown to fail: with the button's top-half rule flipped in the app (`OpensDownAt`), the test stopped at "it is below the button, right edges lined up" with the grid's and the button's rectangles.
- With tray-anywhere already running, it stops at once with a message, exit code 1.

## 2026-11-14: the test's own tray icon (measured)
- A WinForms `NotifyIcon` in a console program (pumping its messages with `Application.DoEvents`) appeared in the overflow (a new exe; Windows' default here) and got `MouseClick` with `Left` for TrayDump's `click` (UI Automation Invoke) and `Right` for `rclick` (ShowContextMenu), each also as MouseDown, MouseUp and Click. One run each.
- So the test checks the grid's refresh and the forwarded clicks on an icon of its own, and never clicks the owner's apps.

## 2026-11-14: driving the windows (measured)
- Posted `WM_LBUTTONDOWN`/`UP` click a floating button (as in To do 3 step C); UI Automation `Invoke` clicks a tile (WinForms runs its Click); a posted `WM_CONTEXTMENU` reaches a tile's right-click; `WM_HOTKEY` posted to the app's message-only hotkey window (found by its title with `FindWindowEx(HWND_MESSAGE, ...)`) opens the grid on the mouse's monitor; a posted `WM_KEYDOWN` Escape reaches the grid's `ProcessCmdKey`; a posted `WM_SYSCOMMAND SC_CLOSE` (what the X sends) closes it; `WM_ENTERSIZEMOVE`, `SetWindowPos`, `WM_EXITSIZEMOVE` drag it (To do 3 step C). All passed in the first test run.
- **The grid takes the foreground.** After a posted click on a button, the grid was the foreground window (4 of 4, probe): `GridForm`'s `Activate()` succeeded. ~~Probably because the owner wasn't using the PC then.~~ Replaced by the test runs: only the **first** grid opening after each start of the app took the front (5 of 5 runs; the later openings in the same run didn't), so it looks like the one-time right Windows gives a newly started program (inferred; the rules aren't documented in detail). The window in front at the start was an Explorer window, not VS Code.
- So the test gives the front back to the window that had it when the app started, after each grid opening (`TestedApp.GiveFrontBack`). `SetForegroundWindow` from the test returned true, and the user's window was in front again within 200 ms (2 of 2 runs; not yet at the moment the call returned). Otherwise a key typed during the test would go to the grid, where Space or Enter clicks the focused tile: a real tray icon.
- **The right-click menu from a posted `WM_CONTEXTMENU`** (probe, polling the app's visible windows every 10 ms):
  - Fresh app, never in front: the button's menu opened within 15 to 37 ms and stayed until Escape, 10 of 10 (5 with the menu-key place -1, 5 with a real point).
  - The grid in front: its menu opened, 4 of 4.
  - After the grid closed and the foreground went back to VS Code: the button's menu never showed, 4 of 4. Matches test run 3, where it failed the same way after the grid had closed.
  - Test runs 1 and 2: the grid's menu didn't show after a restart (the button's did, after grids). Not explained; possibly the grid then couldn't take the front (inferred).
  - So WinForms' menus depend on the app's activation history, which posted messages don't fake. A real right-click activates the app first, so the user isn't affected. The test checks Exit only in a freshly started app, on its button, before any grid opens; the grid's Exit is the owner's hand test.

# WinForms and per-monitor scaling

How WinForms (.NET 10, PerMonitorV2) behaves when a window opens on a monitor whose scaling differs from the main monitor's. Updated in place; wrong findings are marked wrong, not deleted.

## 2026-11-14: a new form on the 100 % monitor (measured, To do 3 step A)
Setup: the main monitor (laptop) is at 150 %, so the system DPI is 144; DISPLAY6 is at 100 %, DISPLAY5 at 150 %. The grid window: `FixedToolWindow`, `AutoScaleMode.None`, sizes in physical pixels from `GetDpiForMonitor`, fonts in `GraphicsUnit.Pixel`.

- Logged with `OnDpiChanged` and `DeviceDpi`: a form created on DISPLAY6 gets "DpiChanged 96 → 144" while its window is created, whether its `Location` was the monitor's corner or its middle, and reports `DeviceDpi` 144 in `OnLoad`. The first move in `OnLoad` then gives "DpiChanged 144 → 96".
- That second change: WinForms resized the Labels' fonts from 12 px to 8 px (×96/144, although they were set in pixels), and Windows kept the window's outer size, so the client area grew from 256x184 to 262x201. The tiles' positions and sizes (`AutoScaleMode.None`) did not change.
- On DISPLAY5 (150 %, like the main monitor): no DPI change at all.
- Step A's first version (borderless, never moved after its window existed) showed right-sized titles on DISPLAY6: no move, so no second change (inferred from the log above; that version wasn't logged).
- Laying the grid out again in `OnDpiChanged` after a move between monitors (directly, or later with `BeginInvoke`) also gave titles at 2/3 size; with `BeginInvoke` the window also kept its old size (not explained).
- What works (measured at 100 % and 150 %): text drawn by the form itself (`TextRenderer.DrawText` in `OnPaint`, with its own pixel-size fonts), and in `OnLoad`: move, set `ClientSize`, place again. The grid is kept on its monitor while dragged (`WM_MOVING`), so it never changes DPI after that. In src/TrayAnywhere/GridForm.cs.

## 2026-11-14: the grid not always on top (measured, To do 3 step B)
Not about scaling, but WinForms window behaviour of the same grid.

- Seen by the owner: with the grid open on the right monitor, VS Code came to the front (after Exit in Steam's menu) and covered the grid. A window logger showed the grid neither closed nor moved: still visible at its place, but without `WS_EX_TOPMOST`, below VS Code in the z-order. Then the hotkey closed the hidden grid, and a second press opened a new one, also without the flag.
- Probe (a WinForms program in a temp folder that loads the app's DLL and uses its classes through reflection; the flag read with `GetWindowLong` every 10 ms): `GridForm` shown directly (with and without icons, on both 150 % monitors, and when it took the front with `SetForegroundWindow`) and `App.OpenOrCloseGrid` called directly: on top in 11 of 12 runs (the odd one came right after a hotkey run; not explained). Opened through a registered hotkey (the app's `Hotkey` class with Ctrl+Alt+F12, pressed with `keybd_event`): not on top, 6 of 6. The real app with Alt+`: not on top from the moment its window existed (87 ms after the press).
- Trace of the window messages (a copy of `GridForm` that logs them), opened through the hotkey: the window is created without the flag (already off in `OnHandleCreated`, though `TopMost` is true); it is activated while being created (`WM_ACTIVATE` before `OnLoad`); while it shows, Windows places it three times behind another window (`WM_WINDOWPOSCHANGING` with that window as `insertAfter`); `OnShown` comes after all of that. Why the flag is left out on this path is not known. Probably it has to do with the app being allowed to take the front after a hotkey (inferred).
- Fix (in src/TrayAnywhere/GridForm.cs `KeepOnTop`): `SetWindowPos(HWND_TOPMOST)` with `SWP_NOACTIVATE`, only when the flag is missing, in `OnShown` and at every refresh. With it: on top in 4 of 4 probe runs (3 through the hotkey, 1 direct). Only when missing, because setting it also lifts the grid above other always-on-top windows, such as the owner's notification popups in the same corner. Not through the `TopMost` property: its `SetWindowPos` has no `SWP_NOACTIVATE`, so it would also activate the grid. (`TopMost = true` on an open grid did bring the flag back, measured once.)
- The owner's notification popup script ran into something similar earlier: a popup sank below normal windows while keeping its flag; it now sets `HWND_TOPMOST` again when it finds itself sunk.
- Not tested, though first reported as tested: a grid without `ShowInTaskbar = false` (to drop its hidden owner window), and setting `TopMost` in `OnActivated`. Both test runs loaded the app's original DLL instead of the changed copy: the probe's folder held a copy of TrayAnywhere.dll from a project reference, and `Assembly.LoadFrom` with another path returned that one. For later probes: no reference to the DLL under test, and print `Assembly.Location`.

## Sources
- High DPI support in Windows Forms: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms
- Own measurements on this PC (logs written from the app, removed after).

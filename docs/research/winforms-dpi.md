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

## Sources
- High DPI support in Windows Forms: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms
- Own measurements on this PC (logs written from the app, removed after).

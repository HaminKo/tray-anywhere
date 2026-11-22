# Full-screen windows (To do 18)

How a full-screen program looks to the app, for hiding the floating button under it.

## The top windows on this PC (measured 2026-11-21, build 26300)
- Method: a PowerShell script (Add-Type C#, per-monitor DPI aware, so physical pixels) listed every top-level window front to back (`EnumWindows` gives them in z-order), with `IsWindowVisible`, DWM's cloak (`DWMWA_CLOAKED`), the extended styles, `GetWindowRect` and DWM's visible bounds (`DWMWA_EXTENDED_FRAME_BOUNDS`). Three monitors: the main one 2560 × 1600, two external ones.
- About 440 top-level windows, 46 to 51 of them visible.
- Visible windows whose visible bounds cover a whole monitor:
  - StarCraft (class `OsWindow`): full-screen on the main monitor; not always on top, no caption. It didn't have the focus (VS Code on an external monitor had it), and no visible, uncloaked window was above it on that monitor.
  - TextInputHost's `Windows.UI.Core.CoreWindow` (the touch keyboard and emoji panel): covers the main monitor, but cloaked (value 2: by the shell).
  - `Progman` (the desktop): covers the whole virtual screen, all three monitors; last in the z-order; a tool window. No visible top-level `WorkerW`.
- Windows' main taskbar (`Shell_TrayWnd`) was not always on top and second to last, just above `Progman`: below StarCraft, while StarCraft didn't have the focus. So Windows keeps its taskbar under a full-screen program on that monitor, not only while that program has the focus. The external monitors' taskbars (`Shell_SecondaryTrayWnd`), with no full-screen window there, were always on top.
- Maximized windows: DWM's visible bounds are exactly the monitor's free area (3840 × 2088 on a 3840 × 2160 monitor with a 72 px taskbar). `GetWindowRect` is 8 to 11 px bigger on each side (Windows' invisible resize border), so it reaches into the neighbouring monitor: a maximized window on one external monitor overlaps the other by 11 px.
- Minimized windows are at about (-32000, -32000), on no monitor.
- Several hidden `Windows.UI.Core.CoreWindow`s are 1 × 1 at 0,0; the toast window is 0 px tall while no toast shows (notifications.md).
- No visible window failed `DWMWA_EXTENDED_FRAME_BOUNDS`.
- A full-screen window that never had the focus (made with `WS_EX_NOACTIVATE` and shown with `SW_SHOWNOACTIVATE`, as the GUI test's; measured with a PowerShell copy of it): ~~it goes in front of the user's normal windows (above a maximized VS Code there), below the always-on-top ones~~. Wrong for the GUI test's own window: the same calls from GuiTest.exe put it near the back (273rd of about 310, behind VS Code and Explorer windows; the cause isn't known). Moved in front with `SetWindowPos(HWND_TOP, SWP_NOACTIVATE | SWP_SHOWWINDOW)` instead, the app found it in one GUI test run of two (not measured why; one of the user's windows there coming to the front would do it). Replaced: the test's window is always on top, as many games are, and shown with `SW_SHOWNOACTIVATE`.
- With the PowerShell copy in front (a normal window, no focus): Windows takes the always-on-top flag off that monitor's taskbar (`Shell_SecondaryTrayWnd`, a tool window), but the taskbar stays in front of the full-screen window. A rule "the first window that isn't always on top decides" therefore stopped at the taskbar (the first GUI test run failed so); passing over tool windows that don't cover the monitor too reached the full-screen window on both external monitors.
- Cost: one pass over all top-level windows, with DWM's bounds for each visible one, took 0.10 to 0.17 ms (the mean of 20 passes, four runs); with `GetWindowRect` instead, 0.07 ms.

## Not handled (no case on this PC; code review, To do 18)
- "Show my taskbar on all displays" off: a monitor without a taskbar has its free area equal to its bounds, so a maximized window there covers it and its button would hide. Both external monitors here have a taskbar (measured above).
- An overlay that stays visible over a whole monitor (always on top, see-through, click-through; screen recorders and game overlays can have one) would keep that monitor's button hidden. None here (measured above). Passing over click-through windows (`WS_EX_TRANSPARENT`) would fix it; the GUI test's own window is not click-through, so it would still count.
- A top-level `WorkerW` over every monitor in front of `Progman` (wallpaper programs make one on older Windows) would count as full-screen on an empty desktop. None here; from memory, not checked: since 24H2 Explorer makes `WorkerW` a child of `Progman`.

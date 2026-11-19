# Notifications (toasts): where Windows shows them, and whether they can be moved or read

Windows 11 shows notification banners ("toasts") at the bottom right of the main monitor only. A tray app's old-style balloon (`Shell_NotifyIcon` with `NIF_INFO`) is shown as a toast too. To do 13 asks to see them on the monitor the owner works on.

## 2026-11-18: To do 13 step A, probes (build 26300.9711, 26H2)
Method: Windows PowerShell 5.1 scripts (per-monitor DPI aware, physical pixels) sent test toasts with the built-in `Windows.UI.Notifications` classes under PowerShell's app id `{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe`, then looked at windows with Win32 calls. One run each unless said otherwise. Monitors as in docs/local.md (primary DISPLAY1 at 150 %, DISPLAY6 at 100 %).

### The toast window (measured)
- One window shows every toast: class `Windows.UI.Core.CoreWindow`, title `New notification` (Windows' display language, English here), process ShellExperienceHost. Styles `WS_POPUP | WS_VISIBLE | WS_CLIPSIBLINGS` (0x94000000), extended `WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP` (0x00200008). No owner.
- **EnumWindows doesn't list it** (it sits in a special z-order band), so a probe that compares EnumWindows lists sees nothing when a toast shows. `FindWindow("Windows.UI.Core.CoreWindow", "New notification")` and `WindowFromPoint` find it.
- It stays visible all the time: with no toast it is 0 px tall at the main monitor's work-area corner, (1966,1528)-(2560,1528). For a toast the shell gives it a size: one toast (1966,1270)-(2560,1528), 594 x 258; two stacked (1966,1058)-(2560,1528). Its right and bottom edges are the work area's (2560, 1528). The same window handle in every probe: the shell reuses it.
- It shows 0.5 to 0.7 s after `Show()` (two measurements). Each toast went away about 6 s after it showed (two measurements), the window back to 0 px tall after the last.

### Do Not Disturb while a full-screen app runs (measured)
- During the probes StarCraft covered the whole main monitor (0,0)-(2560,1600), not in front (Discord on DISPLAY6 was). `SHQueryUserNotificationState` returned 2, `QUNS_BUSY` ("a full-screen application is running"). A normal test toast then **showed no banner**: it went straight to the notification centre (it is in `ToastNotificationManager.History`). So in that state there is no toast window to move.
- Which switch caused it is inferred, not measured: Windows 11's Settings > System > Notifications > "Turn on do not disturb automatically" > "When using an app in full-screen mode".
- A toast with `scenario="urgent"` breaks through: it showed as "New important notification" with a "View notification" button, its text hidden. PowerShell's key `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\<app id>` has `AllowUrgentNotifications = 1`.

### Moving it (measured)
- `SetWindowPos` from another process (no activation, no z-order change) moves it: to DISPLAY6's bottom right, it returned true, stayed there 4 s (no snap-back) and drew there. Its DPI (`GetDpiForWindow`) changed 144 -> 96.
- Moved to the 100 % monitor, it keeps its size in pixels (594 x 258), and its content draws at the new scale (about 363 px wide instead of 545), at the window's **bottom left**; the rest is see-through. So on a monitor with a lower scale, the window must go further left to bring the content to the corner: left = work-area right - width x (new scale / old scale) puts the content's right edge as far from the corner as on the main monitor (worked out from one picture, not measured).
- **Resizing it** to the new scale (396 x 172) does not lay the content out again: it stays where the old size put it and is cut off at the bottom. Move only.
- When another toast arrives, the shell moves the window back to the main monitor with its new size. When a toast goes, the shell resizes it there (and to 0 px tall at the end).
  - **Not always** (owner's test, 2026-11-18, below): after the app has moved it, the shell sometimes leaves the window where it is and moves it again later, even below the screen.
- A click on a moved toast: not measured (needs the real mouse).

### Hooks (measured)
- An out-of-context WinEvent hook (`SetWinEventHook` limited to ShellExperienceHost's process id, events 0x8002 to 0x800B) reports `EVENT_OBJECT_LOCATIONCHANGE` for the toast window at each change above (each reported 2 or 3 times). No `EVENT_OBJECT_SHOW` or `HIDE`: the window is never hidden.
- The hook reported the probe's own move 12 ms after the call.

### Tray balloons and app ids (measured)
- `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings` has keys `NotifyIconGeneratedAumid_<number>` (9 here): Windows' app ids for balloons from tray icons. Toasts from apps with their own app id (Discord: `com.squirrel.Discord.Discord`) look like any other app's. A program that reads notifications could tell balloons apart by that prefix; a mover can't, since one window shows every toast.

### Balloons (measured once)
- A WinForms `NotifyIcon.ShowBalloonTip` from Windows PowerShell, during the full-screen Do Not Disturb above: no banner (the toast window stayed 0 px tall), and no new `NotifyIconGeneratedAumid_` key.
- With Windows accepting notifications (state 5), the GUI test's balloon showed through the same toast window (one run).

### In the code (step B, 2026-11-18)
- src/TrayAnywhere/ToastMover.cs: `FindWindow` for the toast window every 5 s; a hook on its process's `EVENT_OBJECT_LOCATIONCHANGE`, set again when the window is new (the shell restarted). On each event, if the window has a height: its place is left = work-area right - width x (mouse's monitor's scale / main monitor's scale), bottom = work-area bottom, on the mouse's monitor (the main one too: there it is the shell's own place); moved there (no resize) unless it is there already. So the app's own move (which raises an event too) and a second copy of the app (the GUI test's, same place) don't move it again.
  - First version (wrong, owner's test): it moved the window only while it was on the main monitor, and left it alone once moved.
- Checked live (one run): the built app, mouse on the main monitor, an urgent test toast: the app left it at the main monitor's corner. The move itself waits for a run with the mouse on a secondary monitor (the GUI test's step, or the owner's test).
- The GUI test's step (`ToastToMouse`): its own tray icon shows a balloon; the toast window must end at the expected place on the mouse's monitor. Skipped while `SHQueryUserNotificationState` isn't 5 (`QUNS_ACCEPTS_NOTIFICATIONS`) or no popup shows within 5 s.

### Owner's test, first round (2026-11-18)
- Step 3 (a test toast, mouse on DISPLAY5): the toast showed on DISPLAY5, but its top at the taskbar, the rest below the screen (owner's screenshot).
- Probes after it (polling every 5 to 20 ms and the hook, the owner's copy running; measured):
  - Before the next toast, the empty window was still on DISPLAY5, (5806,995)-(6400,995), 0 px tall, cloaked: after the owner's toast, the shell had not taken it back to the main monitor (its usual place, (1966,1528)).
  - The next toast (mouse on DISPLAY5) showed at (5806,1158)-(6400,1386): its bottom 163 px below DISPLAY5's work area (1223), 91 px below the screen. The shell moved it back to the main monitor 6 s later. The app, which only moved windows on the main monitor, left it there.
  - The toast after that (mouse on DISPLAY5, window parked on the main monitor before): the shell sized it on the main monitor, the app moved it to (5806,995)-(6400,1223), correct, and it stayed 6 s.
  - The empty window parked at (5806,995) by a probe, mouse on the main monitor: the shell first moved it back to the main monitor (0 px tall), then sized it there; correct.
- Not found: which shell step put the window 163 px too low, and why the shell sometimes doesn't take the window back. Possibly clicking or hovering the toast (the owner may have; not measured). The fix doesn't depend on it: the app now checks every place the window takes.

### Owner's test, second round (2026-11-18, after the fix)
- The owner re-published the fixed build and repeated the test toast with the mouse on DISPLAY5, several times (closed with its X, and left to go away by itself): "tests look good". No flicker or jump reported.

### Not measured (from the code review, no case seen here)
- A secondary monitor with a higher scale than the main one: the toasts would draw larger than the window and may be cut off. Not on this PC (the main monitor has the highest scale, 150 %).
- On a lower-scale monitor the window reaches past the work area's right edge by width x (1 - ratio), 198 px on DISPLAY6, over DISPLAY5's left edge, see-through, while a toast shows (about 6 s). Whether a click there reaches the window below: not measured.

### Reading notifications instead (measured, partly)
- `UserNotificationListener.Current.GetAccessStatus()` from Windows PowerShell (a program without package identity) returned `Allowed`. Not tried: reading the list (`GetNotificationsAsync`).
- .NET 10 has no built-in WinRT. Calling it needs Microsoft's Windows SDK projection (a `net10.0-windows10.0.x` target framework, which the .NET SDK downloads from NuGet and which adds its DLLs to the app) or hand-written COM interop for each WinRT interface used. Not tried.
- Sources: [SHQueryUserNotificationState](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate), [UserNotificationListener](https://learn.microsoft.com/uwp/api/windows.ui.notifications.management.usernotificationlistener), [Toast scenarios (urgent)](https://learn.microsoft.com/windows/apps/design/shell/tiles-and-notifications/toast-schema#toastscenario).

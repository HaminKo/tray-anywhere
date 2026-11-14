# The tray in UI Automation (and what Windows stores about tray icons)

How the Windows 11 taskbar's tray looks to UI Automation, and other places that hold tray icon data. Updated in place; wrong findings are marked wrong, not deleted.

## 2026-11-14: first probe (build 26300.9711, 26H2)
Method: PowerShell 5.1 with System.Windows.Automation, `FindAll(Descendants, ControlType = Button)` under the top-level window with class `Shell_TrayWnd`. Read-only; the overflow flyout was not opened.

### Taskbar windows (measured)
- `Shell_TrayWnd`: the primary taskbar.
- `Shell_SecondaryTrayWnd`: one per other monitor (two here). Not searched for buttons yet.

### Tray buttons on the primary taskbar (measured)
All are XAML (`FrameworkId = XAML`) and support only `Invoke` and `ScrollItem`.

| AutomationId | ClassName | What |
|---|---|---|
| `NotifyItemIcon` | `SystemTray.NormalButton` | App icons (OneDrive, Session Inbox, one with an empty name) |
| `SystemTrayIcon` | `SystemTray.NormalButton` | "Show Hidden Icons" (the overflow arrow), the input indicators |
| `SystemTrayIcon` | `SystemTray.AccentButton` | Network, Power (battery) |
| `SystemTrayIcon` | `SystemTray.OmniButtonCenter` / `Left` / `Right` | Volume, Clock, Notifications |
| `SystemTrayIcon` | `SystemTray.ShowDesktopButton` | Show Desktop |

- The `Name` is the tooltip text, often several lines, e.g. "OneDrive - Personal\nBacked up and synced". One app icon had an empty name.
- Every element reports the same `ProcessId`: explorer's. So UI Automation does not say which app owns an icon.
- Only `Invoke` (a left click) is offered: no pattern for a right click.
- Odd: the Session Inbox icon's rectangle was 613 px wide (others 48). Still not explained after spike step A; details there.
- Time: ~0.5 s for the whole primary taskbar (all buttons, task list included), measured once with a Stopwatch in PowerShell. A search limited to the tray is faster: measured in spike step A below.

### Coordinates (measured numbers, interpretation inferred)
In the same PowerShell process, UI Automation gave the primary taskbar as `0,1528,2560,72`, while WinForms `Screen.AllScreens` gave the primary screen as 1707x1067. So UI Automation returned physical pixels and WinForms returned values scaled by 150 %. The app must be per-monitor DPI aware and use one kind of coordinates throughout.

### Windows' tray icon list in the registry (measured)
`HKCU\Control Panel\NotifyIconSettings` holds one subkey per tray icon Windows has seen (51 here). Values: `ExecutablePath`, `IconGuid`, `IconSnapshot`, `InitialTooltip`, `IsPromoted`, `Publisher`, `UID`.
- `IconSnapshot` is a PNG (178 to 3607 bytes seen): a picture of the tray icon, possibly older than the current one.
- `IsPromoted = 1` means "shown on the taskbar" (OneDrive and powershell.exe here); missing means hidden in the overflow.
- `InitialTooltip` is often empty.
- Old entries stay: one exe can have several (claude.exe 6, vlc.exe 3). So this is "icons ever seen", not "icons now".
- Undocumented: may change in a Windows update.
- Possible use (untested): match tray items to entries to get the owning exe and a real icon picture, instead of the exe's own icon.

## 2026-11-14: Referencing UI Automation from .NET 10 (measured, SDK 10.0.404)
- `System.Windows.Automation` (UIAutomationClient.dll, UIAutomationTypes.dll) is in the `WPF` profile of the Windows Desktop framework: `FrameworkList.xml` in the SDK's `packs\Microsoft.WindowsDesktop.App.Ref\10.0.15\data`.
- A `net10.0-windows` library with only `<UseWindowsForms>true</UseWindowsForms>`: build fails, CS0234 "The type or namespace name 'Automation' does not exist in the namespace 'System.Windows'". With `<UseWPF>true</UseWPF>`: builds. Used in src/Tray/Tray.csproj.
- A project that only references src/Tray (TrayDump, a plain console program without UseWPF) still gets `Microsoft.WindowsDesktop.App` in its runtimeconfig and runs.
- `AutomationElement.RootElement.Current.Name` is an empty string on this PC, so it is no use as a check. Finding `Shell_TrayWnd` among the root's children works from the .NET 10 console.
- Not tried: `<FrameworkReference Include="Microsoft.WindowsDesktop.App" />` (the whole framework, both profiles).

## 2026-11-14: Spike step A: the visible tray items (build 26300.9711)
Method: read-only PowerShell probes (a tree dump that skips the task list, the same read with and without DPI awareness, the read split into timed phases), then TrayDump (C#, .NET 10) on the live taskbar.

### Where the tray buttons are (measured)
- Path: `Shell_TrayWnd` → child pane with class `Windows.UI.Input.InputSite.WindowClass` → the tray buttons are that pane's **direct children**, left to right. Read with `FindAll(TreeScope.Children, AutomationId = NotifyItemIcon or SystemTrayIcon)`. Used in src/Tray/Taskbar.cs.
- The same pane also holds `TaskbarFrame` (class `Taskbar.TaskbarFrameAutomationPeer`): Widgets, Start, Search, Task View and the task list buttons. Searching only the pane's direct children never walks into it.
- Other children of `Shell_TrayWnd`: `TrayNotifyWnd` (the old Win32 tray window, covering the tray area, no UI Automation children) and `ReBarWindow32` → `MSTaskSwWClass` "Running applications". Neither holds tray buttons.
- 12 buttons here: 3 `NotifyItemIcon` (Session Inbox, one with an empty name, OneDrive) and 9 `SystemTrayIcon` (Show Hidden Icons, two input indicators, Network, Volume, Power, Clock, Notifications, Show Desktop). The tray shows these 12 in the same order (compared with the probe's output, not yet by the owner).
- App buttons have a 24x24 `Image` child: the icon itself.
- Session Inbox, still odd: the button is 613 px wide (x 1947 to the screen's right edge, 2560), but its first `Image` child is a normal 24x24 at 1959,1552; a second `Image` (`MainVisual`) reports an empty rectangle (infinite values). Cause unknown. It matters for a simulated click at the button's centre (step D): that point lands on the Volume button. The `Image` child's rectangle is the better target.
- Names: the tooltip text. Lines are separated by `\r` and/or `\n`. The clock's date holds invisible format characters (Unicode category Cf; they print as "?" in PowerShell and vanish when TrayDump drops Cf characters; probably left-to-right marks, inferred). The Session Inbox name changes while it runs (it is that tool's own tooltip).

### Coordinates and DPI (measured)
- The same rectangles with and without `SetProcessDpiAwarenessContext(PerMonitorV2)` (PowerShell probe): the XAML tray buttons report physical pixels either way. So TrayDump needs no DPI manifest. The app (To do 3) still needs to be per-monitor aware for its own window and screen coordinates (see "Coordinates" above).
- The InputSite pane itself reports `0,1528,1706,48` in both modes: width and height scaled by 150 %, the position not. Not used.

### Time (measured)
- Phases, PowerShell, 4 runs, the later 3 warm: finding `Shell_TrayWnd` with UI Automation among the desktop's 35 top-level windows (`RootElement.FindFirst(Children, ClassName)`) 97 to 371 ms; Win32 `FindWindow` + `AutomationElement.FromHandle` 1 to 5 ms; finding the pane 7 to 9 ms; `FindAll` of the buttons 11 to 13 ms; reading Name, AutomationId and BoundingRectangle of 12 buttons 10 to 12 ms.
- TrayDump with the UI Automation desktop search: first read 248 to 308 ms, second read 125 to 148 ms (3 runs).
- TrayDump with `FindWindow` (the code now): first read 106 to 136 ms (includes loading UI Automation), second read 16 to 29 ms in 3 of 4 runs; one 246 ms, not explained. So the planned 2 s refresh of the visible icons costs about 20 to 30 ms.
- Result: src/Tray/Taskbar.cs finds `Shell_TrayWnd` with `FindWindow` (Win32 P/Invoke) and uses UI Automation from there.

## 2026-11-14: Spike step B: the overflow (build 26300.9711)
Method: read-only PowerShell probes with the flyout closed (Win32 `EnumWindows` / `EnumChildWindows` for hidden windows, then UI Automation from the window handles). The flyout was not opened by a probe.

### Where the overflow items are (measured, flyout closed)
- Top-level window: class `TopLevelWindowForOverflowXamlIsland`, title "System tray overflow window.", owned by explorer, **exists while the flyout is closed** (not visible).
- Its Win32 child `Windows.UI.Composition.DesktopWindowContentBridge` has a child `Windows.UI.Input.InputSite.WindowClass` (same class as the taskbar's tray pane). Both are hidden.
- `AutomationElement.FromHandle(top-level window)` shows **no** descendants while hidden (UI Automation skips hidden windows when walking down). `FromHandle(InputSite window)` works: its **direct children** are the overflow buttons, 12 here, all `NotifyItemIcon`, class `SystemTray.NormalButton`, each with an `Image` child. The same as on the taskbar.
- So the overflow can be read **without opening the flyout**: no flicker. Path: `FindWindow` → `FindWindowEx` (bridge) → `FindWindowEx` (InputSite) → `FromHandle` → `FindAll(Children, NotifyItemIcon)`. `FindWindowEx` from the top-level window straight to InputSite finds nothing: it is a grandchild.
- While closed, every button and image reports `BoundingRectangle` empty and `IsOffscreen = true`. So no positions until the flyout opens.
- Names: the tooltip, as on the taskbar. Some start with a space (" Claude", " Discord", " Phone Link - Connected"); the dump trims it.
- The 12 here: Claude, Copilot, Discord, New quick note, Phone Link, Java Update, Google Drive, Safely Remove Hardware, Malwarebytes, Steam, Battle.net, Windows Security. Not yet compared with the open flyout.
- Time: 16 to 28 ms per read (PowerShell, 4 runs, warm), including the three window lookups.
- Explorer had been running for a while, and the flyout had probably been opened in that session. Unknown yet: whether the items are there right after Explorer starts, before the flyout is first opened.

### The hidden list is live (measured)
Method: a ~40-line test program (WinForms `NotifyIcon`, compiled with the .NET Framework `csc.exe` that ships with Windows, in a temp folder) shows a tray icon, changes its tooltip A → B → C → D every 6 s, writes the time of each change to a log, then removes the icon. A PowerShell probe read the hidden overflow meanwhile; the flyout stayed closed throughout (`IsWindowVisible` false at every read).
- A new icon from a new exe went into the overflow (Windows' default for new icons here) and was in the hidden list at the first read, 0.4 s after start.
- Tooltip changes: with reads every 300 ms, each change showed 54 to 240 ms after the program made it (4 changes). With one read 3 s after each change: every read current (4 of 4).
- Removal: gone from the hidden list at the next read after the program removed it (within 1 s, and in the sparse run 3 s later).
- So the overflow can be refreshed as often as the visible icons, without the flyout: the app's refresh decision changed with this (docs/decisions.md).
- One earlier run (one read per phase, exe compiled seconds before) read stale names twice: "A" 2.5 s after B was set, "A B" 2.5 s after C was set. Not reproduced in the two later runs with a logging exe. Most likely the brand-new exe started late (a virus scan of a new file is a usual cause; inferred, not measured), so the program's changes came later than the probe assumed. That run had no log of its own to check.

### Name after a tooltip change (measured)
- The `Name` is the icon's **first tooltip, a space, then the current tooltip**: "TrayProbe A" while the tooltip is still the first one; "TrayProbe A TrayProbe B", then "... A TrayProbe C", "... A TrayProbe D". Not every change appended: only the first tooltip and the current one.
- Fits the leading spaces above (" Claude", " Discord"): an empty first tooltip, a space, then the current one (inferred).
- The new icon's `NotifyIconSettings` entry has `InitialTooltip = 'TrayProbe A'`, the first tooltip. Possible use in step C: match an item by its name starting with an entry's `InitialTooltip` (untested). One test exe got no entry at all within the test (TrayProbeIcon3, shown 15 s); not explained.
- The visible taskbar follows the same rule (one case seen): Session Inbox read "Session Inbox: all clear Session Inbox: 1 session needs you\nvideo-downloader" in TrayDump, its first tooltip and its current one. OneDrive's "OneDrive - Personal\nBacked up and synced" has a line break, not a space: probably one two-line tooltip (inferred).

### After an Explorer restart: no overflow window until the flyout opens (measured)
- Owner's test (2026-11-14): Explorer restarted from Task Manager, tray icons back, arrow not touched: TrayDump found no `TopLevelWindowForOverflowXamlIsland`. Still none about 15 minutes later: a probe listed every window of the shell's Explorer, top-level and nested, and none had "Overflow" or "XamlIsland" in its class. So Explorer creates the window the first time the flyout opens, not at start or after a delay.
- Opening it by script: `InvokePattern.Invoke()` on the "Show Hidden Icons" button. First opening after the restart: window visible after 124 ms, its 12 buttons readable at 163 ms. Invoking the arrow again closes it: hidden 55 ms later. Once the window exists: open 61 ms, closed 123 ms after the first invoke. So the flash lasts about 0.1 to 0.2 s.
- After closing, the window stays (hidden) with its 12 buttons, as before the restart.
- Focus: while open, the flyout is the foreground window; after closing, the taskbar (`Shell_TrayWnd`) is, not the window that had the focus before (a browser window in the probe). `SetForegroundWindow(previous window)` from the probe returned false and changed nothing: Windows doesn't let a background program take the focus.
- The arrow has no language-independent mark: AutomationId `SystemTrayIcon` and class `SystemTray.NormalButton` are shared with the input indicators, `HelpText` and `AccessKey` are empty, patterns only Invoke and ScrollItem. Only its `Name`, "Show Hidden Icons" on this English Windows, tells it apart. It was the first button of the tray pane in every read so far.

### Names after an Explorer restart (measured)
- After the restart, 4 of 12 overflow buttons had an empty `Name`: by their place in the list, probably Claude, Copilot, Discord and Windows Security (inferred; they had names before). Empty while the flyout was hidden, while it was open for 1.5 s, and after it closed again. So it is not the hidden read: most likely those apps added their icons again after the restart without a tooltip (inferred; Windows asks running apps to add their icons again when the taskbar comes back).
- Java Update's icon did not come back; Task Manager's did (it was running, opened for the restart).

### In the code
- src/Tray/Taskbar.cs `ReadOverflowItems(openIfMissing, out openedFlyout)` follows the path above; items get kind `Overflow` and an empty rectangle. Taskbar and overflow share one button reader, and every window lookup uses Win32 `FindWindowEx`.
- If the overflow window is missing and `openIfMissing` is true: invoke the arrow (found by its name), wait up to 3 s for the window to show (else an error, so a slow opening isn't left open), invoke the arrow again to close it, then read. TrayDump passes true and prints a line when it did this.
- TrayDump on the live taskbar: 12 on the taskbar, 12 in the overflow (the same 12 as the probes); overflow read 19 ms, after the taskbar reads in the same process.

## Open questions for the spike (To do 2)
- Overflow, step B: answered in "Spike step B". The hidden list is live; it doesn't exist after an Explorer start until the flyout opens once; its order matched the open flyout (owner, 2026-11-14).
- Right click: does `IUIAutomationElement3::ShowContextMenu` (COM UI Automation; not in the .NET System.Windows.Automation wrapper) open the app's menu? Otherwise a simulated right-click on the real icon.
- Owner exe: can tray items be matched to NotifyIconSettings entries reliably? Known answer to test step C against: the icon with the empty name is the owner's crash-monitor script, a .bat that runs a PowerShell script in `powershell.exe` (owner, 2026-11-14; path in docs/local.md). That fits the `IsPromoted = 1` entry for powershell.exe.
- Session Inbox's 613 px rectangle (step A): why, and does it matter for clicks?

## Sources
- UI Automation for Win32: https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32
- System.Windows.Automation (.NET): https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation
- IUIAutomationElement3::ShowContextMenu: https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationelement3-showcontextmenu
- NotifyIconSettings: no official documentation; own observation on this PC.

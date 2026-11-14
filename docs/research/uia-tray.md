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

## Open questions for the spike (To do 2)
- Overflow: which window holds it while open (probably `TopLevelWindowForOverflowXamlIsland`, unchecked), does it keep its items while closed, and how visible is opening and closing it?
- Right click: does `IUIAutomationElement3::ShowContextMenu` (COM UI Automation; not in the .NET System.Windows.Automation wrapper) open the app's menu? Otherwise a simulated right-click on the real icon.
- Owner exe: can tray items be matched to NotifyIconSettings entries reliably? Known answer to test step C against: the icon with the empty name is the owner's crash-monitor script, a .bat that runs a PowerShell script in `powershell.exe` (owner, 2026-11-14; path in docs/local.md). That fits the `IsPromoted = 1` entry for powershell.exe.
- Session Inbox's 613 px rectangle (step A): why, and does it matter for clicks?

## Sources
- UI Automation for Win32: https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32
- System.Windows.Automation (.NET): https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation
- IUIAutomationElement3::ShowContextMenu: https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationelement3-showcontextmenu
- NotifyIconSettings: no official documentation; own observation on this PC.

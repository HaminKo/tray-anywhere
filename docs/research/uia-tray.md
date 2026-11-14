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
- Odd: the Session Inbox icon's rectangle was 613 px wide (others 48). Not explained yet; check in the spike.
- Time: ~0.5 s for the whole primary taskbar (all buttons, task list included), measured once with a Stopwatch in PowerShell. A search limited to the tray should be faster (not measured).

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

## Open questions for the spike (To do 2)
- Overflow: which window holds it while open (probably `TopLevelWindowForOverflowXamlIsland`, unchecked), does it keep its items while closed, and how visible is opening and closing it?
- Right click: does `IUIAutomationElement3::ShowContextMenu` (COM UI Automation; not in the .NET System.Windows.Automation wrapper) open the app's menu? Otherwise a simulated right-click on the real icon.
- Owner exe: can tray items be matched to NotifyIconSettings entries reliably?

## Sources
- UI Automation for Win32: https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32
- System.Windows.Automation (.NET): https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation
- IUIAutomationElement3::ShowContextMenu: https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationelement3-showcontextmenu
- NotifyIconSettings: no official documentation; own observation on this PC.

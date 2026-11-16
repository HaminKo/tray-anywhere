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

## 2026-11-14: Spike step C: owner exe and icon picture (build 26300.9711)
Method: read-only PowerShell probes (registry, processes, COM, PrintWindow), compared with TrayDump on the live tray: 3 app icons on the taskbar (the crash monitor with no name, Session Inbox, OneDrive) and 12 in the overflow (4 with no name since the Explorer restart).

### NotifyIconSettings, in more detail (measured)
- 43 subkeys now (the first probe noted 51; not explained). Root values: `Version` = 3, `MigrationStatus` = "Migration started", and `UIOrderList`.
- `UIOrderList` (REG_BINARY, 344 bytes) is 43 numbers of 8 bytes, little-endian: each is a subkey's name, every subkey once. So it is an order of all entries.
- `ExecutablePath` is either a plain path or `{known-folder GUID}\rest`, e.g. `{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}` = Program Files (x86), `{6D809377-6AF0-444B-8957-A3773F02200E}` = Program Files. All 43 resolved with `SHGetKnownFolderPath` (shell32).
- `IconSnapshot` pictures are 16x16 to 48x48 PNGs. Drawn on one sheet they look like the apps' tray icons.
- One entry per exe and `UID` (or `IconGuid`): two PowerShell scripts with a tray icon each (the crash monitor and Session Inbox, both `powershell.exe`, both UID 1, inferred from the WinForms default) share **one** entry. Its snapshot is the crash monitor's warning triangle; Session Inbox shows a grey circle (seen in a PrintWindow render, below). So one of the two would get the wrong picture.
- An exe that runs doesn't always have an icon: `jucheck.exe` (Java Update) runs, but its icon didn't come back after the Explorer restart. `explorer.exe` has 3 entries (a plug icon with UID 0; Safely Remove Hardware, IconGuid `{7820AE78-…}`; Bluetooth, `{6DA68F06-…}`) and shows 1 icon now. `Malwarebytes.exe` has 2 entries (UID 1 and 2).
- A running check by full path through WMI (`Win32_Process.ExecutablePath`) missed Task Manager: the path is empty for it, probably because it runs elevated (inferred). `QueryFullProcessImageName` with limited query rights may work there (untested).
- Last-write times of the subkeys: 22 of 43 are 2025-04-03 20:29:49 (the same second, probably a migration; inferred), including Steam, Battle.net and OneDrive, whose icons are in the tray now. So entries are not rewritten when an icon is added again, and the time doesn't tell which have an icon now.

### Order (measured, one comparison)
- The overflow's order in step B (names read before the Explorer restart): Claude, Copilot, Discord, New quick note, Phone Link, Java Update, Google Drive, Safely Remove Hardware, Malwarebytes, Steam, Battle.net, Windows Security.
- Today's `UIOrderList`, keeping only entries whose exe runs and that aren't promoted: Discord, Claude, Copilot (`copilotapp.exe`), Malwarebytes (UID 2), New quick note, Phone Link, Java Update, explorer (plug), Google Drive, Task Manager, explorer (Safely Remove Hardware), Malwarebytes (UID 1), Steam, Battle.net, Windows Security, explorer (Bluetooth).
- The flyout's order is that list with the extra entries skipped, **except Discord** (first there, third in the flyout). Discord's subkey was written today at 12:36, after step B's reads, so it may have moved since; unknown. Promoted entries that run: powershell.exe, then OneDrive: the taskbar's order too.

### Dead ends (measured)
- **ITrayNotify** (the undocumented COM interface Chromium used to list and promote tray icons): `CLSID_TrayNotify` `{25DEAD04-1EAC-4911-9E3A-AD0A4AB560FD}`, `ITrayNotifyWin8` `{D133CE13-…}` and the callback `INotificationCB` `{D782CCBA-AFB0-43F1-94DB-FDA3779EACCB}` are registered (proxy in ActXPrxy.dll). `RegisterCallback` returns S_OK with handle 0; Explorer asks the callback for `INotificationCB` but never calls `Notify`, within 2 s of pumping messages (one probe). Chromium's code says the calls came during `RegisterCallback` on older Windows. Not usable here.
- **UI Automation**: every supported property of an app button and its `Image` child, dumped: nothing names the owner (ProcessId is explorer's; RuntimeId is opaque).
- **Win32**: `TrayNotifyWnd` has no child windows on this build, so the Windows 10 way (a `ToolbarWindow32` with each icon's window handle and ID) is gone.
- **Screen copy** (`CopyFromScreen` of the tray area): returned a full-screen window that covered the taskbar at the time, not the icons.
- **The hidden overflow window** has a 0x0 rectangle: `PrintWindow` has nothing to render.

### The open flyout, captured (measured, 13:10)
- With the owner's OK, a probe opened the flyout, rendered it with `PrintWindow` (it has a rectangle while open: 351x231 here, buttons on a 60 px grid) and cut out each button's `Image` child. Real order: Discord, Claude, Copilot, New quick note, Phone Link, Google Drive, Task Manager, Safely Remove Hardware, Malwarebytes, Steam, Battle.net, Windows Security.
- That is exactly the `UIOrderList` order above with the extra entries skipped: 12 of 12, Discord included. So step B's Discord difference was a move since then. The order holds (one comparison).
- Snapshots can be old: Windows Security's live icon has a green check, its snapshot a yellow warning; Discord's live icon has a red badge, its snapshot none. The other 10 looked the same.
- While the flyout is open, the arrow's `Name` is "Show Hidden Icons Hide" (its tooltip changed to "Hide"; the naming rule from step B), so a search for exactly "Show Hidden Icons" fails then. After closing it is "Show Hidden Icons" again (TrayDump).
- Probe mistake, for later probes: PowerShell passes `$null` to a `string` P/Invoke parameter as an empty string, so `FindWindowEx(..., 'TopLevelWindowForOverflowXamlIsland', $null)` looks for an empty title and found nothing (this window's title is "System tray overflow window."). The probe then took the window as missing and left the flyout open for a few minutes. Use `[NullString]::Value`. The C# code passes a real null.

### Program descriptions as a name hint (measured)
- `FileVersionInfo.FileDescription` of the tray programs: Google Drive, Malwarebytes, Steam, Task Manager, Copilot, Claude, Discord match their icon's name; "Microsoft Phone Link", "Microsoft OneDrive", "Windows PowerShell", "Windows Security notification icon" don't; Battle.net's has an odd character. 17 files in 32 ms (PowerShell, first read).
- Without the description, "Google Drive" has no hint (the exe is `GoogleDriveFS.exe`, no first tooltip), and an order-only guess gives it Java Update's entry.

### PrintWindow renders the taskbar (measured)
- `PrintWindow(Shell_TrayWnd, PW_RENDERFULLCONTENT)` (flag 2) rendered the taskbar with its icons while a full-screen window covered it: arrow, warning triangle, grey circle, OneDrive cloud. 34 to 59 ms for the 2560x72 taskbar (5 runs, PowerShell). The `Image` child's rectangle (24x24 here) gives each icon's place in that picture. It is the live picture, on the taskbar's background (not transparent). Taskbar icons only.

### In the code (step C)
- src/Tray/IconSettings.cs reads the entries in `UIOrderList` order and resolves known-folder paths. src/Tray/IconOwners.cs matches: candidates are entries whose program runs (full paths through `QueryFullProcessImageName` with limited query rights, so elevated programs count) and whose `IsPromoted` fits the icon's place. An icon whose name starts with an entry's first tooltip, or holds its exe's file name or `FileDescription`, takes the first such candidate after the previous match; icons without such a name take the candidates between two matches in order. Each `TrayItem` gets `ExePath`, `OwnerFoundBy` (Name, Order, None) and `Picture` (PNG bytes).
- Taskbar icons: `Taskbar.ReadVisibleItems` draws the taskbar once per read and cuts out each app icon's `Image` rectangle. The thread is made per-monitor DPI aware for `GetWindowRect` and `PrintWindow`, so it works from TrayDump, which isn't DPI aware (24x24 crops, measured). `Bitmap` builds with only `<UseWPF>` in Tray.csproj. Overflow icons: the matched entry's snapshot.
- Result on the live tray (TrayDump, 13:16, Task Manager closed by then): 14 app icons, 6 found by name, 7 by order, 1 not found (Session Inbox: its exe's only entry was already taken by the crash monitor). All 13 right, compared with the flyout capture and the names. The pictures were right too, except Windows Security's old snapshot (yellow warning; live: green check).
- Owner's test (2026-11-14): the saved pictures match the taskbar and the open flyout, except overflow icons with a notification badge (Discord's red dot is missing from its snapshot). Session Inbox's live taskbar picture showed its current "2" badge. Stored snapshots come in different sizes (16 to 64 px; Malwarebytes' is 64), so the grid must scale them to one size.
- Time, warm, release build, about 36 % CPU load from other programs: `ReadVisibleItems` 77 to 104 ms with the pictures (5 runs; it was 16 to 29 ms without them), of which `PrintWindow` alone was 24 to 58 ms. Overflow 15 to 28 ms. `IconOwners.Find` 43 to 51 ms (registry 1 to 2 ms; the rest is the processes and the exes' version info). So a full refresh costs about 150 ms. First runs were slower (taskbar 238 to 264 ms, owners 83 to 90 ms).
- Known limits, not seen here (code review): a short exe name or description inside an unrelated tooltip (e.g. "Code" in "Unicode") is a false name match and shifts the order matches after it; a running program without an icon that sits before the last unnamed icon of a gap shifts the icons after it; an entry without `IsPromoted` that Windows shows on the taskbar would count as overflow (new icons went to the overflow here, measured in step B).

## 2026-11-14: Spike step D: left and right clicks (build 26300.9711)
Method: a probe (C# console on .NET 10, per-monitor DPI aware, in a temp folder) clicks one tray button, then lists the visible top-level windows that are new or changed 0.3, 1 and 2 s later, with process, class, rectangle and monitor, plus the foreground window and the mouse position. The mouse was on an external monitor (DISPLAY5 or DISPLAY6) for every click. The owner was using the PC meanwhile, so foreground changes after about 1 s may be theirs (inferred). Only OneDrive, Steam and Discord were clicked (owner's OK).

### Left click: InvokePattern (measured)
- `Invoke()` returned after 31 to 64 ms (3 clicks).
- OneDrive (taskbar): its "Activity Center" window opened on DISPLAY1 just above the icon and became the foreground window within 1 s.
- Discord (overflow, flyout closed, icon with no name): Discord's main window came to the front on DISPLAY6, where it was, as the foreground window.
- Steam (overflow, flyout closed): Steam's tray menu (process steamwebhelper, class `SDL_app`, title "Menu", 253x455) opened on DISPLAY6 at the mouse. A real left click on Steam's icon shows the same menu (owner's test). The menu did not get the foreground and stayed open after the probe ended; posting `WM_KEYDOWN`/`WM_KEYUP` Escape to its window closed it. 0.3 s after the click a hidden Search window (SearchHost) had the foreground, and at 2 s the overflow flyout was open with the mouse on the arrow: not explained, possibly the owner (inferred).
- So **Invoke works on overflow buttons while the flyout is closed**: no flyout, no flash. This was untested before step D.

### Right click: IUIAutomationElement3::ShowContextMenu (measured)
- Returned S_OK after 17 to 26 ms (2 clicks).
- Discord (overflow, flyout closed): Discord's tray menu (class `Chrome_WidgetWin_1`, 224x218, no title) opened on DISPLAY6 at the mouse and got the foreground. A left click opened the main window instead, so ShowContextMenu is a real right click here, and it works while the flyout is closed.
- OneDrive (taskbar): the Activity Center again, the same as a left click. A real right click on OneDrive's icon does the same (owner's test).
- Not needed, so not built: a simulated mouse click on the icon (moves the mouse, needs the icon on screen, and lands in a full-screen window that covers the taskbar), and opening the flyout for overflow clicks.

### Where the menu or window appears (measured, 3 apps)
- Up to the app: Steam and Discord put their menus at the mouse, on the external monitor; OneDrive puts its Activity Center above its icon on the primary taskbar. So in the grid most menus should open next to the mouse (inferred from 2 of 3 apps).

### Calling ShowContextMenu from .NET (measured)
- .NET's System.Windows.Automation has no right click, and UIAutomationCore.dll exports no plain function for it (its name appears only in the DLL's type information). So it takes COM UI Automation: create `CUIAutomation` (CLSID `{FF48DBA4-60EF-4201-AA87-54103EEF594E}`), get the pane with `IUIAutomation::ElementFromHandle(pane window)`, find the button among its children, ask it for `IUIAutomationElement3` (`{8471DF34-AEE0-4A01-A7DE-7DB9AF12C296}`) and call `ShowContextMenu`.
- `ShowContextMenu` is entry 91 of the interface's method table (3 of IUnknown, 82 of IUIAutomationElement, 6 of IUIAutomationElement2, from uiautomationclient.h). The probe called it there and Discord's menu opened, which confirms the number.
- The overflow now had 11 icons (Task Manager closed); 3 with no name (Discord, Claude, Copilot, by order).

### In the code (step D)
- `Taskbar.ReadButtons` stores each item's button and its pane's window in an internal `TrayButton` on `TrayItem`. src/Tray/TrayClicks.cs: `LeftClick` invokes the stored button; `RightClick` creates `CUIAutomation`, walks the pane's children (control view) to the one with the stored button's runtime ID, and calls `ShowContextMenu` at entry 91. Any failure becomes an InvalidOperationException.
- TrayDump on the live tray: `rclick` on Discord (overflow) opened its menu, `rclick` on OneDrive (taskbar) and `click` on Discord ran without error; 29 to 82 ms per click, start of the COM object included. A bad number prints a message, exit 1.
- Owner's test (2026-11-14): `click` and `rclick` on Steam, Discord and OneDrive did what a real click on each icon does, and each menu appeared where expected (Steam and Discord at the mouse on the external monitor, OneDrive above its icon).
- Probe mistake, for later probes: pressing Escape with `keybd_event` to close a menu goes to whatever window has the focus; twice the menu had already closed and the key went to another program. Post the key to the menu's own window instead (worked for Steam's menu), or leave the menu to the owner.

## 2026-11-14: To do 3 step B: the grid's 2 s refresh (build 26300)

### Pictures between reads (measured)
- `TrayDump pictures` three times, 2 s apart, without the tray changing: 13 of 14 app icons gave byte-for-byte the same PNG each time (taskbar icons drawn live, and overflow snapshots). So the grid can compare picture bytes to tell whether an icon changed.
- The 14th, Copilot's overflow icon (empty name, matched by order), was Copilot's picture in the first read and VLC's in the next two, with `vlc.exe` as its program. VLC had just been started (by another session) and showed no tray icon. So a program that runs, has an entry in NotifyIconSettings and no icon now can take the place of an icon matched by order: a wrong picture and program in the grid, not only in TrayDump. Seen once; the order matching is in `IconOwners.Find` ("Spike step C").
- That run, Discord, Claude and Copilot had empty names in the overflow (they had names earlier in the day), so 7 of 14 icons were matched by order, which makes such mix-ups likelier. See "Names after an Explorer restart" above.

### CPU of the 2 s refresh (measured, one run)
- A probe (console, .NET 10, Release, in a temp folder) did the grid's read (`ReadVisibleItems`, `ReadOverflowItems(false)`, `IconOwners.Find`) every 2 s for 60 s, with 60 s without reading before and after. CPU time from `Process.TotalProcessorTime`; Explorer is the sum of all explorer.exe processes.
- Reading: the probe 3938 ms CPU in 60 s (6.6 % of one thread, about 130 ms per read); Explorer 7547 ms (12.6 %). Not reading: Explorer 2766 ms and 4547 ms (4.6 % and 7.6 %); the probe under 50 ms. So the reads cost Explorer about 6 % of one thread (estimated from the difference; the owner and another session were using the PC). Reads took 119 to 368 ms, median 187 ms (30 reads).
- 2026-11-15, To do 8: this 6 % is not reliable. Explorer's own load, with the grid closed, wanders between 0 and about 14 % of one thread in waves lasting tens of seconds, so one minute against the next can differ by that much without any read. See "To do 8: the slower refresh" below.

### Steam's menu didn't open (measured, cause unknown yet)
- Owner's test of the grid, about 16:10: a right click on Steam's tile showed nothing. Then `TrayDump rclick 20` and `click 20` (Steam, overflow) each returned without error (59 and 54 ms), but no Steam window became visible (all visible top-level windows of steam.exe and steamwebhelper.exe listed 0.3 to 2 s later). In To do 2 step D (before 13:55) both opened Steam's menu. One steamwebhelper process had started at 13:52. Owner's tests right after: a real right click on Steam's real icon (laptop, overflow arrow) opened its menu; a right click on Discord's tile in the grid opened Discord's menu. So only Steam ignores the forwarded clicks now.
- Possible causes: (1) Steam now shows its menu only if it may take the foreground. A real click comes while Explorer is the foreground program, which can pass that right on; a forwarded click comes while another program is in front. (2) A forwarded click sends Steam other messages than a real one, and Steam now reacts only to the real ones. (3) A stale Steam button: ruled out, TrayDump reads afresh and lists one Steam icon. Test for (1): TrayDump `rclick` fired 5 s after start, with the owner clicking the laptop's taskbar meanwhile (Explorer in front) and once without.
- Result (owner, about 16:15): with Explorer in front, Steam's menu appeared, though not at the mouse; without, nothing. So (1) is the cause: Steam shows its menu only if it may come to the front. Probably Explorer passes that right on when it is in front itself (inferred). Why it worked in To do 2 is not known; the steamwebhelper that started at 13:52 may draw the menu differently (inferred).
- Fix: `TrayClicks` calls `AllowSetForegroundWindow(ASFW_ANY)` before each click, which works when the caller is the program in front (the grid, just clicked). Confirmed by the owner's retest: Steam's menu opened from the grid.

### Where Steam's menu opens (measured, 5 right clicks from the grid, 17:18)
- Method: a logger (PowerShell, per-monitor aware, physical pixels) listed every visible top-level window of every program every 200 ms with the mouse position, while the owner right-clicked Steam's tile. The menu is steamwebhelper's `SDL_app` "Menu" window, per-monitor aware, 378x682 at 150 % and 248x456 at 100 %.
- Top-left monitor (100 %, top-left corner 640,-1080): the menu's corner at the mouse, 2 of 2 (menu opened up and to the left near the right edge, up and to the right near the left edge).
- Right monitor (150 %, top-left corner 2560,-865): the menu's corner at the mouse plus (1280, -432.5), 3 of 3 to within 2 px: menu opening upward and to the right of that point with the grid at the monitor's left edge (left 4038 and 4011), to its left with the grid at the right edge (right edge 7442, beyond the monitor's 6400: off-screen).
- Fits all 5: the corner lands at mouse + monitor's top-left corner x (scale - 1). So at the mouse on the 100 % monitor and on the laptop screen (corner 0,0), and moved on any monitor with other scaling that isn't at 0,0. Why (inferred): two parts of Steam convert between scaled and physical pixels with different origins for that monitor. Steam's own bug; a real click never meets it, because the real tray is on the laptop screen.

## 2026-11-15: To do 7 step A: moving Steam's menu (build 26300)
Method: a logger (C# console, .NET 10, per-monitor aware, physical pixels, in a temp folder, not committed) listed every top-level window that became visible, every 50 ms, with program, class, styles, owner, DPI awareness, place, and the time since the last mouse button release (read with GetAsyncKeyState every 10 ms). 1.5 s after Steam's "Menu" window showed, it moved it with `SetWindowPos` (no size change, no z-order change, no activation) so that a corner sat at the mouse, opening up and to the right, flipped where there was no room. The owner right-clicked Steam's tile twice on the right monitor (150 %), and clicked Discord's and OneDrive's tiles.

### Steam's menu (measured, 2 right clicks)
- One window for every menu: steamwebhelper, class `SDL_app`, title "Menu", 380x682, styles POPUP, CAPTION, SYSMENU, MINIMIZEBOX; extended styles TOPMOST and TOOLWINDOW; owned by another steamwebhelper window; per-monitor aware. Created at the first right click, then hidden and shown again: the second click showed the same window.
- Timing: visible 157 and 281 ms after the button release. The first time it showed at 32767,32767 and was put in place 80 ms later; the second time it showed at its previous place and was put in place 76 ms later. So a mover must not act on the first place it sees.
- Place: mouse 6325,991, menu (7225,-124)-(7605,558): its bottom-right corner at mouse + (1280, -433), fully off-screen (the monitor ends at 6400). Mouse 3155,1004, menu (4436,-111)-(4816,571): its bottom-left corner at mouse + (1281, -433). Both fit the formula in "Where Steam's menu opens" to within 1 px.
- The move worked: `SetWindowPos` returned true, and the menu stayed where it was put (no move back while open). The owner saw it appear at the mouse ("steam menu moves"). When the menu closed, Steam first moved it back to its wrong place, then hid it (78 ms).
- Hover and a click on an item after the move (owner, 2026-11-16, setting back on): the highlight follows the mouse, and "Store" works. In the first two tries the menu was closed by a click outside it.
- It took the foreground when it showed.

### Moving it at once (measured, 2026-11-16 00:42, setting on, 16 right clicks)
- The logger looked for Steam's menu by class and title (`FindWindow("SDL_app", "Menu")`) every ~15 ms during 1.5 s after a right click, and moved it whenever it was visible and the click wasn't within 50 px of it.
- First move 51 to 246 ms after the button release, as soon as the menu showed at its previous place; Steam then put it at its wrong place 13 to 35 ms later, and the logger moved it again. So two moves per opening.
- On every close, Steam first moves the menu back to its wrong place while it is still visible, then hides it 30 to 90 ms later (seen on every close in this run and the earlier ones).
- Owner (2026-11-16): "it feels instant, but if I spam I can see a flash at a wrong spot very briefly". The flash comes from Steam's own moves (at opening and before closing): a mover outside Steam can only correct them after they happen, here within ~15 ms (one check), about one frame at 60 Hz (estimate).

### Moving it from a WinEvent hook (measured, 2026-11-16 01:04, setting on, 49 openings)
- Instead of checking every 15 ms, the logger set two out-of-context WinEvent hooks (`SetWinEventHook`: EVENT_OBJECT_SHOW to EVENT_OBJECT_HIDE, and EVENT_OBJECT_LOCATIONCHANGE; WINEVENT_SKIPOWNPROCESS), handled on its own message loop. For a window event (OBJID_WINDOW) of class `SDL_app`, title "Menu", process steamwebhelper, it moved the menu whenever it was visible and the mouse wasn't within 50 px of it. Nothing runs inside Steam.
- Lag from Steam's event to the move, by `GetTickCount` (15.6 ms steps): at show, 0 ms in 41 of 49 and one step (15 to 16 ms) in 8; after Steam's own move, 0 ms in 59 of 60 and one step in 1. Our own move raises a LOCATIONCHANGE too; the menu is then near the mouse, so it is left alone (no loop).
- Owner, spamming right clicks with the grid in the left half of the right monitor: "much better - spamming makes it only flash very briefly every like 15 spam clicks or so" (with 15 ms checks: a flash at most spam clicks). The remaining flashes may be the late corrections (inferred, not matched one by one).

### Other windows in the same run (measured)
- Discord's tray menu (right click): `Chrome_WidgetWin_1`, no title, 416x479, POPUP; TOPMOST and TOOLWINDOW; no owner; shown 264 ms after the click at (6009,512)-(6425,991) with the mouse at 6045,937: at the mouse, 25 px past the monitor's right edge (probably its shadow, inferred). Discord's main window was already open, so the left click showed no new window.
- OneDrive's Activity Center (left click): `OneDriveReactNativeWin32WindowClass`, 540x960, POPUP; TOOLWINDOW, not TOPMOST; no owner; system DPI aware; on the laptop screen, 265 ms after the click.
- Unrelated popups that came and went during the run: the owner's Session Inbox popups (POPUP; TOPMOST, TOOLWINDOW, NOACTIVATE), in the bottom-right corner of both external monitors, at times unrelated to any click. They look like menus by their styles and sit on the grid's monitor far from the mouse, so a rule for any program's menu would move one that appears within the watch time after a click. Explorer's own popups (overflow flyout, tooltips) appeared on the laptop screen.

### Steam's "Scale text and icons to match monitor settings" off (measured, 23:51, same logger, nothing moved)
- The owner turned the setting off (Steam > Settings > Interface) and restarted Steam. The menu window is now 248x456 on every monitor (380x682 on the 150 % monitor before), still per-monitor aware: Steam no longer scales its content, so the menu is small on 150 % monitors (owner: "the steam menu is small now on my right monitor").
- Right monitor (150 %): at the mouse in 6 of 6 left and right clicks, with the grid in the left half and in the right half (a corner within 15 px of the mouse, 5 of them within 2 px). So the setting removes the offset.
- Top-left monitor (100 %), mouse at 2526,-164, 34 px from its right edge: the first right click opened the menu at the mouse; the second opened it at (2649,-536)-(2897,-80), 123 px right of the mouse and on the right monitor. Seen once; cause unknown. With the setting on, that monitor gave the menu at the mouse 2 of 2 (To do 3).
- So the only upstream fix in reach costs Steam's scaling on the 150 % monitors.

### Where the wrong place comes from
- The forwarded click carries no position: UI Automation's Invoke and ShowContextMenu take no point, and Discord's menu, sent the same way, opens at the mouse. So the app can't be giving Steam a wrong point (inferred from the code and the Discord measurement).
- Known to Steam's users: since Valve moved the tray menu into Chromium (June 2023), it can open in the wrong place with display scaling above 100 %. A 2023 beta's changelog says it fixed "systray menu opening at wrong location for certain Windows scaling settings or when scaling is disabled in Steam" (as quoted in the forum threads, not checked in Valve's own notes). Users report that turning off "Scale text and icons to match monitor settings" (Steam > Settings > Interface, restart needed) fixed it (one user). Tested: see the section above.
- Why real clicks rarely show it (inferred): the real tray is on the primary monitor, whose top-left corner is 0,0, so the offset corner x (scale - 1) is zero there.
- Sources: https://steamcommunity.com/discussions/forum/0/6516193260168858474 (tray menu and Chromium with display scaling), https://steamcommunity.com/discussions/forum/0/6516193260165168475, https://steamcommunity.com/groups/SteamClientBeta/discussions/0/3826413850815275817 (Steam Client Beta threads).

## 2026-11-15: To do 8: the slower refresh (build 26300)
The grid now reads every 2 s only while the mouse is over it, else every 10 s (docs/decisions.md, 2026-11-15).

### CPU with the grid open and unused (measured)
- Method: a PowerShell script (not kept) started the published Release app with a temp settings file, measured 60 s with the grid closed, opened the grid with a posted `WM_HOTKEY`, waited 1 s, measured 60 s with it open, closed it and measured 60 s again. CPU time from `TotalProcessorTime`, sampled every second; Explorer is the sum of all explorer.exe processes. The grid was never in front and the mouse never over it (sampled every second). The same run then measured the previous commit's app (2 s refresh). 26 icons in the tray then (16 app icons).
- The app, grid open and unused: new 1359 ms in 60 s (2.2 % of one thread), old 2891 ms (4.8 %). An earlier run of the new one: 1141 ms (1.9 %). Closed: 0 to 31 ms per minute for both.
- Reads every 10 s, as meant: the app's CPU came in single seconds 9 to 10 s apart (6 reads), 110 to 330 ms each. With the old 2 s refresh, 60 to 140 ms each (30 reads). So a read after a 10 s pause costs about twice a read 2 s after the last (inferred: caches gone cold), and the app's CPU fell by about half rather than to a fifth (Claude's estimate before measuring: 1.3 %, too low).
- Explorer: closed 5.0 %, open 5.9 %, closed again 6.6 % (new); 2.5 %, 8.5 %, 2.4 % (old); and in the first run of the new one 2.4 %, 8.5 %, 2.6 %. The per-second values show no burst at the reads; Explorer's own load rose and fell in waves of tens of seconds in every phase, grid open or not (0 to about 200 ms per second). So the reads' cost to Explorer is below what a minute's measurement can show, for both refresh rates (inferred from the three runs).

## Open questions for the spike (To do 2)
- Overflow, step B: answered in "Spike step B". The hidden list is live; it doesn't exist after an Explorer start until the flyout opens once; its order matched the open flyout (owner, 2026-11-14).
- Right click: does `IUIAutomationElement3::ShowContextMenu` (COM UI Automation; not in the .NET System.Windows.Automation wrapper) open the app's menu? Otherwise a simulated right-click on the real icon. Step D: yes, also for overflow icons while the flyout is closed (Discord's menu, measured); no simulated click needed. Details in "Spike step D".
- Owner exe: can tray items be matched to NotifyIconSettings entries reliably? Known answer to test step C against: the icon with the empty name is the owner's crash-monitor script, a .bat that runs a PowerShell script in `powershell.exe` (owner, 2026-11-14; path in docs/local.md). That fits the `IsPromoted = 1` entry for powershell.exe. Step C: no exact link exists (every other source is a dead end), so matching is a best guess from name hints, running exes, `IsPromoted` and `UIOrderList`. It found 13 of 14 app icons, all right (measured once). Details in "Spike step C".
- Session Inbox's 613 px rectangle (step A): why, and does it matter for clicks? In step C's dump it was 48 px wide, like the others. Step D: clicks don't use rectangles (Invoke and ShowContextMenu act on the element), so it doesn't matter for them; still not explained.

## Sources
- UI Automation for Win32: https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32
- System.Windows.Automation (.NET): https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation
- IUIAutomationElement3::ShowContextMenu: https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationelement3-showcontextmenu
- NotifyIconSettings: no official documentation; own observation on this PC.
- ITrayNotify and INotificationCB: Chromium's chrome/browser/ui/views/status_icons/status_tray_state_changer_win.h (interface layout as Chromium used it; undocumented by Microsoft).
- PrintWindow and PW_RENDERFULLCONTENT: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow

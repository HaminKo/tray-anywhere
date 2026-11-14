# To do 2: Spike: console dump of visible and overflow tray items (medium)

## Problem / Goal
Before building any window, confirm on this PC that UI Automation can list every tray icon, and that left and right clicks can be passed to them. Example output:

    [pinned]   OneDrive - Personal / Backed up and synced   exe=OneDrive.exe   at 2043,1528
    [overflow] Steam                                        exe=steam.exe      at ...

Already known (docs/research/uia-tray.md): visible icons are XAML buttons under `Shell_TrayWnd` with AutomationId `NotifyItemIcon` (apps) or `SystemTrayIcon` (system icons and the "Show Hidden Icons" arrow). Their name is the tooltip; they support only Invoke; all report explorer as their process. Windows keeps a list of tray icons with exe path, a PNG snapshot and `IsPromoted` in `HKCU\Control Panel\NotifyIconSettings`.

## Read first
- docs/research/uia-tray.md, including its open questions and sources.
- src/Tray and src/TrayDump from To do 1.

## Approach
Steps, each one a build step:
- A. Visible items: find `Shell_TrayWnd`, list the tray buttons with name and rectangle, searching only the tray part, not the whole taskbar. Time it.
- B. Overflow: invoke "Show Hidden Icons", wait for the overflow window (probably `TopLevelWindowForOverflowXamlIsland`, unchecked), read its buttons, close it again. Measure the time and how visible the flyout is; try to keep it short. Also check whether the overflow window keeps its items while closed. If the flicker can't be avoided, ask the user: accept it, or turn every icon on in Settings so nothing is in the overflow.
- C. Owner exe and picture: try matching items to NotifyIconSettings entries (tooltip, IsPromoted, running processes). If it works, the entry's IconSnapshot PNG is the icon; otherwise the exe's icon.
- D. Clicks: `TrayDump click N` (left: InvokePattern) and `TrayDump rclick N` (right: try `IUIAutomationElement3::ShowContextMenu` through COM UI Automation; otherwise a simulated right-click on the real icon, then move the cursor back). Note where each app's menu appears.
- Write every finding (measured or estimated) and dead end into docs/research/uia-tray.md when it happens.

## Ask the user
- Include system icons (network, volume, clock) in the dump? Suggested: yes, marked `[system]`; whether the grid shows them is decided in To do 3.
- If the overflow flyout flickers visibly: accept it, or turn every icon on in Settings? Suggested: decide after seeing it.

## Progress
- A (done 2026-11-14): `Taskbar.ReadVisibleItems()` in src/Tray reads the primary taskbar's tray buttons (FindWindow `Shell_TrayWnd`, then the InputSite pane, then its direct children) into `TrayItem` (name, kind `Pinned`/`System`, rectangle in physical pixels). TrayDump prints them numbered, with the time of a first and a second read; on failure, a message naming the missing part and exit code 1. The owner checked the list against the tray.
- Owner's choices: system icons in the dump as `[system]`; line format number, kind, position, size, name last; tooltip lines joined with " / ".
- For the next steps:
  - B: add `Overflow` to `TrayItemKind`; overflow items get their own lines.
  - C: the icon with no name is the owner's PowerShell crash monitor (powershell.exe).
  - D: Session Inbox's button is 613 px wide; aim a simulated click at its `Image` child, not the button's centre.
- B (next): overflow, as in Approach.

## Test
- Claude: runs the dump and compares it with the real taskbar (count, names).
- User: compares the dump with the tray and the overflow flyout; tries `click` and `rclick` on 2-3 apps (e.g. OneDrive, Steam, Discord) and says where each menu appeared.

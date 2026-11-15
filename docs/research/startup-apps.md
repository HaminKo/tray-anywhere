# Startup apps: how Windows marks a Run entry "disabled"

Task Manager's Startup apps (and Settings > Apps > Startup) can turn off an entry of the per-user Run key without touching it: Windows keeps the Run value and writes a value of the same name under HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run. Undocumented by Microsoft; what is below comes from forensics blogs and this PC.

## From the sources (not measured here)
- REG_BINARY, 12 bytes. First 4 bytes (a DWORD): 2 or 6 = enabled, 3 = disabled. The other 8 bytes: when it was disabled (a FILETIME), all zeros when enabled.
  - Wrong for this PC (build 26300, measured 2026-11-15, below): Task Manager's Disable wrote 1, not 3. The app now counts an odd first byte as off (1 and 3), an even one as on.
- Turning it on again in Task Manager writes 02 00 00 00 and 8 zero bytes back; the Run value is never changed.
- Sysinternals Autoruns disables differently (moves the value to an AutorunsDisabled subkey); not handled here.
- Sources: [windowsir: StartupApproved\Run](https://windowsir.blogspot.com/2022/07/does-autostart-really-mean-autostart.html), [part II](https://windowsir.blogspot.com/2022/07/startupapprovedrun-pt-ii.html), [Hexacorn, Beyond good ol' Run key, part 104](https://www.hexacorn.com/blog/?p=5992).

## 2026-11-15: this PC (measured)
- Every other Run entry (OneDrive, Steam, Discord, Battle.net, GoogleDriveFS, Edge) has a StartupApproved value 02 00 00 00 00 00 00 00 00 00 00 00. tray-anywhere, written by its own Start with Windows (To do 5), has none.
- Task Manager > Startup apps > Disable on tray-anywhere (which had no value) wrote `01 00 00 00 A1 45 99 77 32 75 DD 01`: first byte 1, then a FILETIME that decodes to 13:52:30.633 local, the moment of the click (a watcher polling every 0.3 s saw it at 13:52:30.654). The Run value stayed as it was. One measurement, by a watcher script reading both values (not part of the project).
- No other place in HKCU held a "disabled" for it (a `reg query HKCU /s /f tray-anywhere`, before the Disable): only Run and RunNotification\StartupTNotitray-anywhere = 1 (Windows' "a new startup app" notice, not a disabled state).
- The owner's hand test with the new build (13:57 to 13:59, same watcher). Which of Task Manager or Settings wrote each value wasn't noted, so the writes aren't told apart by tool:
  - Turned off with no value there: `01` and a FILETIME (3 times). Turned off while `02 00…` was there: `03` and a FILETIME (once). Turned on: `02` and 11 zero bytes (twice). So the first byte looks like flags: 1 = off, 2 = a value written by "turn on" before; 3 = both. The odd/even rule fits all of them.
  - Each menu click with the new build deleted the value within the 0.3 s poll, and Task Manager and Settings then showed it on again (the owner's report). With no value, both show it on.
- The watcher script (not part of the project): polls both values every 0.3 s from PowerShell and logs each change with a timestamp.

## What the app does (To do 11)
- src/TrayAnywhere/Autostart.cs: the tick is off when the value's first byte is odd (1 or 3); any other value, or none, is on. ~~First byte 3 only~~ (wrong on this PC: Task Manager wrote 1). A click in the menu deletes the value, ticked or not (decision: docs/decisions.md, 2026-11-15).
- The GUI test writes 01 with a FILETIME, then 02 with zeros, into its own copy of the key (HKCU\Software\tray-anywhere-gui\Explorer\StartupApproved\Run) and checks the tick and that each click deletes the value.

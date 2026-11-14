# tray-anywhere

Windows 11 shows tray icons (next to the clock, and the hidden ones behind the arrow) only on the main monitor's taskbar. tray-anywhere shows the same icons in a small popup on another monitor. Clicking an icon there clicks the real one, with the left or the right mouse button.

It uses only what comes with Windows and .NET: no extra libraries.

## Status
Early. The popup shows the icons; clicking them and a floating button come next.

## Build and run
You need the .NET 10 SDK:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Then, in the project folder:

```powershell
dotnet run --project src/TrayAnywhere
```

Press Alt+` (Alt and the key left of 1): the popup opens on the monitor the mouse is on and stays on top until you close it with its X, Escape or Alt+` again. Drag its title bar to move it; it opens there next time. To quit, right-click the popup's background and choose Exit.

A console test lists the tray icons the app can find, with the program behind each one:

```powershell
dotnet run --project src/TrayDump
```

Add `-- pictures` to also save each icon's picture to a temp folder, or `-- click 5` / `-- rclick 5` to left- or right-click icon 5 of the list.

## Known limitations
- Some apps open their right-click menu next to the real tray on the main monitor, not at the mouse. tray-anywhere can't change that.
- Icon pictures may look a little different from the real tray icons.
- A Windows update can change how the taskbar is built and break icon detection until tray-anywhere is updated.

## Credits
- [.NET](https://dotnet.microsoft.com) and WinForms by Microsoft, MIT license.
- Windows UI Automation, part of Windows.

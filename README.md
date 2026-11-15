# tray-anywhere

Windows 11 shows tray icons (next to the clock, and the hidden ones behind the arrow) only on the main monitor's taskbar. tray-anywhere shows the same icons in a small popup on another monitor. Clicking an icon there clicks the real one, with the left or the right mouse button.

It uses only what comes with Windows and .NET: no extra libraries.

## Status
Early. The popup shows the icons and passes clicks on them to the real ones. Starting with Windows comes next.

## Build and run
You need the .NET 10 SDK:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Then, in the project folder:

```powershell
dotnet run --project src/TrayAnywhere
```

Press Alt+` (Alt and the key left of 1): the popup opens on the monitor the mouse is on and stays on top until you close it with its X, Escape or Alt+` again. Left- or right-click an icon in it as you would in the real tray. While it is open it keeps up with icons that come, go or change. Drag its title bar to move it; it opens there next time.

Each monitor other than the main one also gets a small button with an arrow, just above the clock. Click it to open or close the popup where the arrow points. The button and the popup stay together: drag either one and the other comes along. To quit, right-click the button or the popup's background and choose Exit.

Where you dragged the buttons and the popup is kept in `$env:APPDATA\tray-anywhere\settings.json`.

A console test lists the tray icons the app can find, with the program behind each one:

```powershell
dotnet run --project src/TrayDump
```

Add `-- pictures` to also save each icon's picture to a temp folder, or `-- click 5` / `-- rclick 5` to left- or right-click icon 5 of the list.

A GUI test starts the app with a settings file of its own, works its buttons and popup, and checks what they show. It needs a second monitor and a closed tray-anywhere, and it shows a test tray icon of its own for a few seconds. It never uses your mouse or keyboard:

```powershell
dotnet run --project src/GuiTest
```

## Known limitations
- Some apps open their right-click menu next to the real tray on the main monitor, not at the mouse. tray-anywhere can't change that.
- Steam's menu can open away from the mouse, even off-screen, on a monitor with other scaling than 100 %. Use the popup on another monitor for Steam, or on the left part of that one.
- Icon pictures may look a little different from the real tray icons.
- The floating button stays on top, so it covers a corner of the window under it, also over full-screen video. Drag it somewhere else if it is in the way.
- A Windows update can change how the taskbar is built and break icon detection until tray-anywhere is updated.

## Credits
- [.NET](https://dotnet.microsoft.com) and WinForms by Microsoft, MIT license.
- Windows UI Automation, part of Windows.

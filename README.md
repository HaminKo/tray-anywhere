# tray-anywhere

Windows 11 shows tray icons (next to the clock, and the hidden ones behind the arrow) only on the main monitor's taskbar. tray-anywhere shows the same icons in a small popup on another monitor. Clicking an icon there clicks the real one, with the left or the right mouse button.

It uses only what comes with Windows and .NET: no extra libraries.

## Status
Early. The popup shows the icons and passes clicks on them to the real ones, and the app can start with Windows.

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

A GUI test starts the app with a settings file of its own, works its buttons and popup, and checks what they show. It needs a second monitor, can run while tray-anywhere is running, and shows a test tray icon of its own for a few seconds. It never uses your mouse or keyboard:

```powershell
dotnet run --project src/GuiTest
```

Add `-- screenshots` to save a picture of the popup on each secondary monitor to a temp folder. Close tray-anywhere first: the pictures are copies of the screen.

## Start with Windows
`dotnet run` runs the app from its build folder, which the next build replaces. For every day, publish it to a folder of its own and start it from there:

```powershell
dotnet publish src/TrayAnywhere -o publish
.\publish\TrayAnywhere.exe
```

Then right-click a floating button or the popup's background and choose Start with Windows. Windows now starts this copy each time you sign in. Choose it again to turn it off. If you move the folder, choose it again from the new place.

To update, choose Exit and run both lines again; the setting stays.

## Known limitations
- Some apps open their right-click menu next to the real tray on the main monitor, not at the mouse. tray-anywhere can't change that.
- Icon pictures may look a little different from the real tray icons. Hidden icons show the picture Windows saved when the app first showed its icon, so a badge or status change (like a red dot) may be missing.
- The floating button stays on top, so it covers a corner of the window under it, also over full-screen video. Drag it somewhere else if it is in the way.
- A Windows update can change how the taskbar is built and break icon detection until tray-anywhere is updated.
- With Windows' "Hidden icon menu" turned off (Settings > Personalization > Taskbar > Other system tray icons), hidden icons can't be read after Windows restarts the taskbar. Turn it back on to see them.

## Credits
- [.NET](https://dotnet.microsoft.com) and WinForms by Microsoft, MIT license.
- Windows UI Automation, part of Windows.

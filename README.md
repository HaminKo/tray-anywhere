# tray-anywhere

Windows 11 shows tray icons (next to the clock, and the hidden ones behind the arrow) only on the main monitor's taskbar. tray-anywhere shows the apps' icons from there (not Windows' own, such as network, volume and the clock) in a small popup on another monitor. Clicking an icon there clicks the real one, with the left or the right mouse button.

It uses only what comes with Windows and .NET: no extra libraries.

## Status
Early. The popup shows the icons and passes clicks on them to the real ones, and the app can start with Windows.

## Install
You need Windows 11 and the .NET 10 Desktop Runtime, free from Microsoft. Install the runtime once:

```powershell
winget install Microsoft.DotNet.DesktopRuntime.10
```

1. Download the newest tray-anywhere zip from the [Releases](../../releases) page.
2. Unzip it into a folder of its own that you keep, for example `Documents\tray-anywhere`.
3. Run `TrayAnywhere.exe` from that folder. tray-anywhere isn't signed, so the first time Windows may say "Windows protected your PC": choose More info, then Run anyway.

To update, choose Exit tray-anywhere (see below), unzip the new version into the same folder, replacing the files, and run it again. Your settings and Start with Windows stay.

To uninstall, turn Start with Windows off, choose Exit tray-anywhere and delete the folder. Your settings stay in `%APPDATA%\tray-anywhere` (paste that into File Explorer's address bar); delete that folder too if you like.

## How to use
Press Alt+` (Alt and the key left of 1 on a US keyboard; you can pick other keys, see Settings below): the popup opens on the monitor the mouse is on and stays on top until you close it with its X, Escape or the same keys again. Left- or right-click an icon in it as you would in the real tray. Drag an icon to another place within its section to change the popup's order; it keeps that order, and "Reset icon order" in the right-click menu or the settings window brings back Windows' order. While it is open it keeps up with icons that come, go or change. Drag its title bar to move it; it opens there next time.

Each monitor other than the main one also gets a small button with an arrow, just above the clock. Click it to open or close the popup where the arrow points. The button and the popup stay together: drag either one and the other comes along. While a full-screen video or game is in front on that monitor, the button hides, as Windows' taskbar does; the hotkey still works there. To quit, right-click the button or the popup's background and choose Exit tray-anywhere.

Settings... in the same right-click menu, or the gear at the popup's top right, opens a small window. A change there applies at once; close it with its X or Escape. To turn a monitor's button on or off, click that monitor in the window's picture of your monitors (laid out as in Windows' Display settings) and tick or untick "Show the floating button". Under it, pick that button's color (one follows Windows' accent color), its size, and whether it stays on top of other windows. The main monitor has no button at first, as its taskbar has the real tray. On a monitor without a button, the hotkey opens the popup at the bottom right.

To change the hotkey, click the keys next to "Hotkey:" near the bottom of the settings window and press the new ones; Escape keeps the old ones, and Default brings back Alt+`. Every key except F1 to F24 needs Ctrl, Alt or Win with it; Shift alone isn't enough.

Where you dragged the buttons, the popup and its icons, which monitors have a button and how each looks, and your hotkey are kept in `%APPDATA%\tray-anywhere\settings.json`. Delete that file (with tray-anywhere closed) to go back to the defaults.

Windows' notifications (and tray apps' balloons) also follow the mouse: while it is on another monitor, they show at that monitor's bottom right instead of the main one's.

## Start with Windows
Right-click a floating button or the popup's background, choose Settings... and tick Start with Windows. Windows now starts tray-anywhere from its folder each time you sign in. Untick it to turn it off. If you move the folder, tick it again from the new place.

## Build from source
You need the .NET 10 SDK:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Then download the source (Code > Download ZIP on this page, or `git clone` with the address shown there) and, in that folder:

```powershell
dotnet run --project src/TrayAnywhere
```

`dotnet run` runs the app from its build folder, which the next build replaces. For every day, publish it to a folder of its own and start it from there:

```powershell
dotnet publish src/TrayAnywhere -o publish
.\publish\TrayAnywhere.exe
```

To update, choose Exit tray-anywhere, get the new source (`git pull`, or download it again) and run both lines again.

A console test lists the tray icons the app can find, with the program behind each one:

```powershell
dotnet run --project src/TrayDump
```

Add `-- pictures` to also save each icon's picture to a temp folder, or `-- click 5` / `-- rclick 5` to left- or right-click icon 5 of the list.

A GUI test starts the app with a settings file of its own, works its buttons and popup, and checks what they show. It needs a second monitor, can run while tray-anywhere is running, and shows a test tray icon of its own for a few seconds. It never uses your mouse or keyboard:

```powershell
dotnet run --project src/GuiTest
```

Add `-- screenshots` to save pictures of the settings window and of the popup on each secondary monitor to a temp folder. Close tray-anywhere first: the pictures are copies of the screen.

How the release zip is made: [docs/releasing.md](docs/releasing.md).

## Known limitations
- Some apps open their right-click menu next to the real tray on the main monitor, not at the mouse. tray-anywhere can't change that.
- Icon pictures may look a little different from the real tray icons. Hidden icons show the picture Windows saved when the app first showed its icon, so a badge or status change (like a red dot) may be missing.
- The floating button stays on top, so it covers a corner of the window under it (not of a full-screen one: there it hides). Drag it somewhere else, untick "Always on top" for that monitor in Settings... (then any window over it hides it; the hotkey still works), or turn the button off there.
- tray-anywhere isn't signed. With Smart App Control turned on (Windows Security > App & browser control), Windows may block it, and Windows has no way to allow a single app there.
- Keys another program already uses can't be the hotkey: Windows gives them to that program, so the settings window doesn't see them. If another program takes tray-anywhere's hotkey while it is closed, it says so at start and runs without one; pick other keys in the settings window. With no floating button on any monitor, it closes instead; delete settings.json to go back to Alt+`.
- A Windows update can change how the taskbar is built and break icon detection until tray-anywhere is updated.
- While a full-screen app or game runs on the main monitor, Windows shows no notifications at all (Do Not Disturb turns on by itself), so there is nothing to move. Settings > System > Notifications > "Turn on do not disturb automatically" can change that, but then notifications also show over your game.
- On Windows in another language than English, notifications don't move, and hidden icons show only after you open the arrow on the main taskbar once after each sign-in: tray-anywhere finds those parts of Windows by their English names.
- When tray-anywhere starts (with Start with Windows, at each sign-in), the hidden-icons panel on the main taskbar may flash open for a moment, and the keyboard focus moves to the taskbar: Windows makes its list of hidden icons only once that panel has been opened.
- With Windows' "Hidden icon menu" turned off (Settings > Personalization > Taskbar > Other system tray icons), hidden icons can't be read after you sign in or Windows restarts the taskbar. Turn it back on to see them.

## Credits
- [.NET](https://dotnet.microsoft.com) and WinForms by Microsoft, MIT license.
- Windows UI Automation, part of Windows.

## License
MIT: see [LICENSE](LICENSE).

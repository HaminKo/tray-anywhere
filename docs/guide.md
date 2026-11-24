# tray-anywhere: more details

The basics are in the [README](../README.md). This page has the details.

## The popup
Press Alt+` (Alt and the key left of 1 on a US keyboard; you can pick other keys, see [Settings](#the-hotkey) below): the popup opens on the monitor the mouse is on and stays on top until you close it with its X, Escape or the same keys again. Left- or right-click an icon in it as you would in the real tray.

Drag an icon to another place within its section to change the popup's order; it keeps that order, and "Reset icon order" in the right-click menu or the settings window brings back Windows' order.

While it is open it keeps up with icons that come, go or change. Drag its title bar to move it; it opens there next time.

## Using the popup with the keyboard
In the popup, Tab and the arrow keys move between icons, Enter clicks the icon, and the menu key (or Shift+F10) right-clicks it.

## The floating buttons
Each monitor other than the main one also gets a small button with an arrow, just above the clock. Click it to open or close the popup where the arrow points. The button and the popup stay together: drag either one and the other comes along.

While a full-screen video or game is in front on that monitor, the button hides, as Windows' taskbar does; the hotkey still works there.

To quit, right-click the button or the popup's background and choose Exit tray-anywhere.

## The settings window
Settings... in the same right-click menu, or the gear at the popup's top right, opens a small window. A change there applies at once; close it with its X or Escape.

To turn a monitor's button on or off, click that monitor in the window's picture of your monitors (laid out as in Windows' Display settings) and tick or untick "Show the floating button". Under it, pick that button's color (one follows Windows' accent color), its size, and whether it stays on top of other windows.

The main monitor has no button at first, as its taskbar has the real tray. On a monitor without a button, the hotkey opens the popup at the bottom right.

## The hotkey
To change the hotkey, click the keys next to "Hotkey:" near the bottom of the settings window and press the new ones; Escape keeps the old ones, and Default brings back Alt+`. Every key except F1 to F24 needs Ctrl, Alt or Win with it; Shift alone isn't enough.

## Where your settings are kept
Where you dragged the buttons, the popup and its icons, which monitors have a button and how each looks, and your hotkey are kept in `%APPDATA%\tray-anywhere\settings.json`. Delete that file (with tray-anywhere closed) to go back to the defaults.

## Settings are kept by monitor name
tray-anywhere keeps each monitor's settings (where its button and the popup were dragged, the button's look, and whether a secondary monitor's button is off) under Windows' name for that monitor, such as DISPLAY5. Windows can give a monitor a new name after it is plugged in again; that monitor then starts with the default settings.

## A settings file that can't be read
If settings.json can't be read (for example after editing it by hand), tray-anywhere says so at start and runs with the default settings. It doesn't change the file until it can read it again: fix it and start tray-anywhere again, or delete it to go back to the defaults.

## Notifications
Windows' notifications (and tray apps' balloons) also follow the mouse: while it is on another monitor, they show at that monitor's bottom right instead of the main one's.

# tray-anywhere: more details

The basics are in the [README](../README.md). This page has the details.

## Using the popup with the keyboard
In the popup, Tab and the arrow keys move between icons, Enter clicks the icon, and the menu key (or Shift+F10) right-clicks it.

## Settings are kept by monitor name
tray-anywhere keeps each monitor's settings (where its button and the popup were dragged, the button's look, and whether a secondary monitor's button is off) under Windows' name for that monitor, such as DISPLAY5. Windows can give a monitor a new name after it is plugged in again; that monitor then starts with the default settings.

## A settings file that can't be read
If settings.json can't be read (for example after editing it by hand), tray-anywhere says so at start and runs with the default settings. It doesn't change the file until it can read it again: fix it and start tray-anywhere again, or delete it to go back to the defaults.

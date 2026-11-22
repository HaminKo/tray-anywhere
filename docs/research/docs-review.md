# Docs review before the first public release (2026-11-22)

A fresh subagent that hadn't seen the project checked README.md, docs/releasing.md and LICENSE against the code, read-only (the "Before a public release" list in docs/todo.md). It reported 18 findings. Claude checked each against the code before the owner decided; "checked" below means Claude read the code path, "not checked" means only the reviewer's word. The owner decided each batch ("go with your suggestions").

## Fixed in the README
- Line 3 said "the same icons": the grid shows only apps' icons, Windows' own (network, volume, clock) are filtered out (TrayReader.cs). Checked.
- "Exit" is "Exit tray-anywhere" in the menu (AppMenu.cs). Checked.
- The hotkey rule: every key except F1 to F24 needs Ctrl, Alt or Win, not only typing keys; Shift alone isn't enough (HotkeyKeys.IsAllowed). Checked.
- A hotkey taken at start: with no floating button on any monitor the app closes instead of running without one (Program.cs). Checked.
- settings.json also keeps the hotkey. Checked.
- `-- screenshots` also saves the settings window (GuiTest). Checked.
- Known limitations added: hidden icons also need English Windows (the arrow's English name, Taskbar.cs; accepted on 2026-11-17, checked); the flyout's flash and the focus moving to the taskbar at start, so at each sign-in with Start with Windows (App.cs, checked); "Hidden icon menu" off fails after every sign-in, not only after a taskbar restart (from the code and the 2026-11-17 decision).
- The default key is left of 1 only on a US keyboard (HotkeyKeys.cs comment; other layouts from the reviewer, not checked).
- `%APPDATA%\tray-anywhere` instead of `$env:APPDATA\...`: the README path is for File Explorer, which doesn't understand PowerShell's form. The owner's "no %VAR%" rule is for terminal commands.
- Build from source now says how to get the source and to get the new source before updating; no clone URL, as it holds the GitHub login.

## Kept for docs/guide.md (to add when the README trim makes it)
- Each monitor's settings are kept by Windows' name for it (DISPLAY5); a re-plugged monitor that gets a new name loses its button's place, look and on/off (accepted on 2026-11-14).
- Keyboard use in the grid: Tab and the arrow keys move between icons, Enter clicks, the menu key or Shift+F10 right-clicks (IconTile.cs, checked).
- A settings.json that can't be read: a message at start, the default places, and the file isn't written until it can be read (Program.cs, checked).

## Dropped
- The grid, the settings window and the buttons can be dragged only within their own monitor: seen at once when dragging.
- TrayDump also lists Windows' own icons, and `click N` counts them: the list shows the numbers.
- ARM64 PCs: the reviewer read the exe as x64, so an ARM64 PC would need the x64 runtime, which winget may not install there by default. Not checked, and nothing here can test it; add it if a visitor reports it.
- Double-clicks aren't passed on, so an app that opens only on a double-click might not respond. The reviewer's guess ("likely, untested"); every app on the owner's PC works. A to-do if one turns up.

## docs/releasing.md
- Step 4's "Runs" check (the app still running after 5 s) also passes while the app shows an error box, which keeps it running; and while a GUI test runs, the check's copy (the test's lock) only shows "already running". Checked (Program.cs's message boxes, the shared lock). Fixed: step 4 says to watch for a message box and not to run the GUI test at the same time. Rejected: the script finding the message box itself (it would have to tell it from the floating buttons' windows, to be measured by running the app, for a step done by hand a few times a year).
- Also from the reviewer, not checked: PowerShell 5.1's Start-Process doesn't quote an argument with a space, so a TEMP path with a space would split `--settings`. Not this PC's case; left, as quoting makes the line harder to read.

# tray-anywhere

## Goal
Windows 11 shows tray icons (next to the clock, and the hidden "overflow" ones behind the arrow) only on the primary monitor's taskbar. tray-anywhere shows those icons in a popup grid on a secondary monitor, split into "Pinned" and "Overflow", and passes left and right clicks on them to the real icons. One user (the owner), on one PC.

The owner's hard rules:
- No third-party software, libraries or NuGet packages: only what ships with Windows or .NET (UI Automation, WinForms, Win32 P/Invoke, System.Text.Json).
- Small, readable code: the owner wants to read and understand every line. Plain code over clever code; comments say why.
- The tray-reading code stays isolated in its own files, so a Windows update that changes the taskbar's UI Automation tree is easy to fix.
- Target: Windows 11 24H2/25H2 and later, several monitors with different scaling.

Don't build what's under "Later" in docs/todo.md until asked.

## User and environment
- Windows 11 (this PC: 26H2, build 26300). The laptop screen is primary; two external monitors.
- C# on .NET 10 (LTS) with WinForms; SDK 10.0.404, installed with winget.
- Shell: PowerShell 5.1 in the VS Code terminal.
- Real paths: docs/local.md (gitignored).

## Working style
- One to-do per chat, built with the /build-todo skill (`/build-todo N`, or `/build-todo N, step B` for one step of a bigger to-do); new to-dos are written with /add-todo.
- Commit after each working step; end a step with the finish-step skill.

## Build specifics
- Checks, run from the project folder (TrayAnywhere.slnx there builds every project):
  - After edits: `dotnet build` (warnings are errors), ~10 s.
  - Once per step: `dotnet format --verify-no-changes`, ~11 s; and, from To do 2 on, the tray dump on the live taskbar: `dotnet run --project src/TrayDump`.
  - Once per step, each its own call: the GUI test, `dotnet run --project src/GuiTest`, ~30 s (exit 0; runs beside the owner's tray-anywhere; needs a secondary monitor). A UI change gets a step in it (src/GuiTest/Program.cs), in the same build step.
  - Screenshots, only for a step that changes the look: `dotnet run --project src/GuiTest -- screenshots [DISPLAYn]`, ~10 s, needs a secondary monitor and tray-anywhere not running: ask the owner to Exit it. The grid with its button per secondary monitor, `$env:TEMP\tray-anywhere-gui\grid-DISPLAYn.png` (paths printed), ~300 tokens for both (estimate).
- Docs to update when behaviour changes: README.md (headline features, install, how to use, Start with Windows, build from source, known limitations, Credits) and the docs/ pages it links to; docs/research/uia-tray.md when the taskbar's UI Automation tree or the reading code changes; docs/research/notifications.md when Windows' toast window or ToastMover changes.
- Must keep working: `dotnet run --project src/TrayDump` lists every icon on the primary taskbar's tray and in the overflow, with each app icon's program (exit code 0; 1 if a read fails); `-- click N` / `-- rclick N` then left- or right-click icon N, overflow icons included, without opening the flyout. `dotnet run --project src/TrayAnywhere`: Alt+` opens the grid ("Pinned", "Overflow") on the mouse's monitor and closes it again: on a secondary monitor always at its floating button, where the button's arrow points (^ above, v below in the monitor's top half); on the main monitor, or a secondary one with its button off, bottom right, or where it was last dragged there; each secondary monitor's button does the same and has Start with Windows and Exit on right-click; "Floating button on this monitor" in the right-click menu of a secondary monitor's button or grid turns that button off (kept in settings.json) and, from the grid's menu, on again; there the button and the open grid move together while either is dragged; the buttons' places and the grid's place on a monitor without a button are kept in settings.json across restarts; the grid stays on top; its X and Escape close it, a click elsewhere doesn't; left and right clicks on its icons reach the real ones; a tile dragged within its section moves there without a click, and that order stays across refreshes and restarts (settings.json) until Reset icon order in the right-click menu; while open it follows icons that come and go; Steam's tray menu opens at the click on every monitor; Windows' notifications show at the bottom right of the mouse's monitor while it is on a secondary one; right-click on its background: the same menu; Start with Windows ticked writes the running exe to the per-user Run key, unticked deletes it; it shows no tick while Task Manager's Startup apps has it turned off, and a click clears that. `dotnet publish src/TrayAnywhere -o publish` makes the folder to run from. The GUI test checks these, except the real Alt+` and mouse (dragging a button or a tile, a click elsewhere), Steam's real menu (it checks a window like it), the main monitor's dragged grid place, the grid's menu, signing in with Start with Windows on, where menus appear, and notifications while Windows hides them (a full-screen program) or the mouse is on the main monitor, and a click on a moved one: those stay in the user's test checklist when a step touches them.
- Review checklist: none yet: the built-in code-review skill.

## Status
Now and next: the "Where we are" block at the top of docs/todo.md.

## Decisions
Reasons, who decided and what was rejected: docs/decisions.md.
- C# on .NET 10 with WinForms; no NuGet packages.
- The grid opens with a global hotkey and with a small floating button, on the monitor the mouse is on.
- Refresh: read everything when the grid opens; while it is open, re-read all icons every 2 s while the mouse is over the grid, else every 10 s (checked every 0.5 s). The overflow is read from its hidden window; the flyout is opened only once, when Explorer hasn't created that window yet, never by the refresh. No window and no arrow: an empty overflow, or an error naming Settings' "Hidden icon menu" while that is off.
- Clicks act on the tray button itself (Invoke; ShowContextMenu through COM UI Automation), not a simulated mouse click; overflow icons without opening the flyout.
- Overflow icons show Windows' stored snapshot (the icon's first picture): no live picture exists without opening the flyout (To do 12's dead ends in docs/research/uia-tray.md).
- An icon's program and stored picture: matched to Windows' tray list by name, then by order; where extra programs run, only those Explorer shows an icon for take part, else no program (generic picture beats a wrong one).
- Settings in `$env:APPDATA\tray-anywhere\settings.json`; autostart via the per-user Run registry key, set by Start with Windows in the right-click menu for the running copy, which runs every day from `publish/`; a click there wins over a \"turned off\" in Task Manager's Startup apps.
- Checks: `dotnet build` (warnings as errors) and `dotnet format`; no unit tests at first.
- Layout: src/Tray holds only the code that reads and clicks the tray and is the only app project that uses UI Automation (`<UseWPF>`); src/TrayDump is the console tool; src/TrayAnywhere is the app (To do 3); src/GuiTest is the GUI test, which uses UI Automation on the app's own windows.
- GUI test: works the real app through posted window messages and UI Automation, never the real mouse or keyboard; clicks only a tray icon of its own; writes only to `$env:TEMP\tray-anywhere-gui` and its own registry key HKCU\Software\tray-anywhere-gui (the app's `--registry-key`), never Windows' Run or Startup apps keys; runs beside the owner's copy (the app's `--gui-test`: its own lock, no hotkey, and it also moves the test's own window like Steam's menu).
- Tests run at the app's real speed: no test-only option that shortens its timers or waits.
- Steam's tray menu, which Steam misplaces on scaled monitors, is moved to the click by a Steam-only mover on a WinEvent hook; no mover for other programs' windows, except Windows' toast window.
- Windows' notification popups (toasts and tray balloons) move to the mouse's secondary monitor: a mover only for the shell's toast window (hook on ShellExperienceHost; move, never resize); always on. Nothing to move while Windows shows none (a full-screen program on the main monitor).
- Releases: a zip of the framework-dependent `dotnet publish` on a GitHub release, version in TrayAnywhere.csproj (docs/releasing.md); no installer, not self-contained, not signed.
- Implicit usings off; a short hand-written .editorconfig.
- Grid: Alt+`; dark; app icons only, tooltip on hover; reads the tray before it shows; stays on top until its X, Escape or Alt+`; draggable within its monitor. Floating button: one per secondary monitor, each can be turned off in the right-click menu; no settings window until there are about four settings.
- Icon order: the grid's own, dragged within each section, kept in settings.json by the program's file name (else the icon's name); new icons at the end; Windows' order and sections are never changed.

## Project docs
- README.md: for visitors: what it does, install, how to use, autostart, build from source, known limitations, credits.
- docs/releasing.md: how to make a release zip and upload it.
- docs/local.md: this PC's paths and copy-ready commands (gitignored).
- docs/todo.md: the to-do list. docs/todo/: one file per to-do. docs/archive/: finished to-dos' files (read only when a to-do names one).
- docs/decisions.md: decisions with reasons.
- docs/research/: one file per topic; the taskbar's UI Automation tree is in uia-tray.md, WinForms and per-monitor scaling in winforms-dpi.md, Task Manager's Startup apps marker in startup-apps.md, release packaging in release.md, Windows' notification window in notifications.md.

## Never in tracked files
- Personal paths (the user folder, drive paths): they go in docs/local.md.
- The GitHub login used for pushes: it goes in docs/local.md; current files only, old commits that hold it are fine.

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
  - GUI test and screenshots: added by To do 4.
- Docs to update when behaviour changes: README.md (headline features, build and run, autostart, known limitations, Credits) and the docs/ pages it links to; docs/research/uia-tray.md when the taskbar's UI Automation tree or the reading code changes.
- Must keep working: `dotnet run --project src/TrayDump` lists every icon on the primary taskbar's tray and in the overflow, with each app icon's program (exit code 0; 1 if a read fails).
- Review checklist: none yet: the built-in code-review skill.

## Status
Now and next: the "Where we are" block at the top of docs/todo.md.

## Decisions
Reasons, who decided and what was rejected: docs/decisions.md.
- C# on .NET 10 with WinForms; no NuGet packages.
- The grid opens with a global hotkey and with a small floating button, on the monitor the mouse is on.
- Refresh: read everything when the grid opens; while it is open, re-read all icons every 2 s. The overflow is read from its hidden window; the flyout is opened only once, when Explorer hasn't created that window yet, never by the 2 s refresh.
- Settings in `$env:APPDATA\tray-anywhere\settings.json`; autostart via the per-user Run registry key.
- Checks: `dotnet build` (warnings as errors) and `dotnet format`; no unit tests at first.
- Layout: src/Tray holds only the tray-reading code and is the only project that uses UI Automation (`<UseWPF>`); src/TrayDump is the console tool; src/TrayAnywhere is the app (To do 3).
- Implicit usings off; a short hand-written .editorconfig.

## Project docs
- README.md: for visitors: what it does, build and run, autostart, known limitations, credits.
- docs/local.md: this PC's paths and copy-ready commands (gitignored).
- docs/todo.md: the to-do list. docs/todo/: one file per to-do. docs/archive/: finished to-dos' files (read only when a to-do names one).
- docs/decisions.md: decisions with reasons.
- docs/research/: one file per topic; the taskbar's UI Automation tree is in uia-tray.md.

## Never in tracked files
- Personal paths (the user folder, drive paths): they go in docs/local.md.
- The GitHub login used for pushes: it goes in docs/local.md; current files only, old commits that hold it are fine.

# To do 15: An installer (small)

## Problem / Goal
"An installer." Today a visitor installs the .NET 10 SDK, gets the code and runs `dotnet publish` (README "Build and run"). Goal: an easier way for visitors to get tray-anywhere running, for the public release.

Conflict with the owner's rule "no third-party software": the usual installer builders (WiX, Inno Setup, NSIS) are third-party, and MSIX needs the Windows SDK's tools and a signing certificate that visitors' PCs trust. For the owner's own PC, `publish/` plus Start with Windows already does the job.

## Read first
- README.md "Build and run" and "Start with Windows".
- docs/decisions.md 2026-11-14 "Start with Windows: ..." (the everyday copy runs from `publish/`; `$env:LOCALAPPDATA\Programs\tray-anywhere` was rejected for the owner's copy).
- docs/todo.md "Before a public release".

## Approach
Suggested, without an installer program:
- A zip of the published folder on a GitHub release. README: unzip anywhere, run TrayAnywhere.exe, then Start with Windows. Uninstall: untick Start with Windows, Exit, delete the folder (settings.json in `$env:APPDATA\tray-anywhere` stays: say so).
- Framework-dependent (small; the visitor installs the .NET 10 Desktop Runtime with one winget line) or self-contained (`dotnet publish -r win-x64 --self-contained`, built in; no runtime needed, much bigger: measure it).
- If a real installer is still wanted: a PowerShell script that copies the folder to `$env:LOCALAPPDATA\Programs\tray-anywhere` and adds a Start menu shortcut.

## Ask the user
- A zip on a GitHub release instead of an installer? Suggested: yes.
- Framework-dependent or self-contained? Suggested: framework-dependent, with the winget line for the runtime in the README.
- The exe is unsigned, so Windows SmartScreen may warn when a visitor first runs it. Sign it? Suggested: no (a certificate costs money); the README says how to get past the warning.

## Test
- Claude: build the zip, unzip it into a temp folder, run TrayAnywhere.exe from there with the GUI test's options, Exit.
- User: download the zip from the release page, unzip, run, tick Start with Windows, sign out and in.

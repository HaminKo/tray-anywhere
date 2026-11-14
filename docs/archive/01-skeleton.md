# To do 1: Skeleton: .NET 10 SDK, project layout, checks running (small)

## Problem / Goal
Nothing is set up: on 2026-11-14 the PC had only the .NET 8 runtime, no SDK. Goal: `dotnet build` and `dotnet format --verify-no-changes` pass on an empty console program that To do 2 fills in.

## Read first
- CLAUDE.md: the hard rules (no NuGet packages, small readable code, tray code isolated).
- docs/decisions.md: C# on .NET 10 with WinForms.
- docs/local.md: paths on this PC.

## Approach
- The user installs the SDK: `winget install Microsoft.DotNet.SDK.10` (may show a UAC prompt). Check with `dotnet --list-sdks`.
- Suggested layout, one solution at the root so a plain `dotnet build` builds everything:
  - `TrayAnywhere.slnx`
  - `Directory.Build.props`: `net10.0-windows`, nullable on, warnings as errors.
  - `src/Tray/`: class library with only the tray-reading code (the part a Windows update may break).
  - `src/TrayDump/`: the spike console program (To do 2), references src/Tray.
  - `src/TrayAnywhere/`: the WinForms app, added in To do 3.
- `.editorconfig` with the default .NET style, so `dotnet format` has rules to check.
- Find the smallest project setting that makes `System.Windows.Automation` (UIAutomationClient) available in a `net10.0-windows` library: it is part of the Windows Desktop framework and may need `<UseWPF>true</UseWPF>` or `<UseWindowsForms>true</UseWindowsForms>`.
- Write the exact commands into CLAUDE.md "Build specifics", docs/local.md and the README's "Build and run".

## Ask the user
- The layout above, with the tray code in its own library? Suggested: yes.
- Implicit usings (fewer `using` lines, but less visible where names come from)? Suggested: off, so every file shows what it uses.

## Test
- Claude: `dotnet build` and `dotnet format --verify-no-changes` pass; `dotnet run --project src/TrayDump` prints a line.
- User: runs the same three commands in the VS Code terminal.

# Decisions

Each entry: `## YYYY-MM-DD: decision`, then the reason, who decided, and what was rejected and why. Newest first.

## 2026-11-14: Checks are dotnet build (warnings as errors) and dotnet format; no unit tests at first
- Reason: the tray reading depends on the live taskbar, so a unit test would mostly test a fake. The tray dump (To do 2) is the real test; a GUI test comes with To do 4.
- Decided by: the owner (Claude's suggestion).
- Rejected: a unit-test project from the start (little to test without the real taskbar).

## 2026-11-14: Settings file in AppData; autostart via the Run registry key
- Reason: `$env:APPDATA\tray-anywhere\settings.json` (System.Text.Json, ships with .NET) is the usual place for per-user settings. The per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run) is the standard autostart, needs no admin and is one registry value.
- Decided by: the owner (Claude's suggestion).
- Rejected: settings next to the exe (breaks when the build output is cleaned); a Startup-folder shortcut (creating a .lnk needs COM code, more lines than one registry value).

## 2026-11-14: Grid opens by hotkey and floating button, on the mouse's monitor; overflow read only on opening
- Reason: the hotkey is for the keyboard, the button for mouse-only use. "The monitor the mouse is on" works for both external monitors without a setting. Reading the overflow means opening its flyout, so polling it every 1-2 s would flash the flyout all the time; visible icons are cheap to read (the whole primary taskbar took ~0.5 s from PowerShell, measured), so they are re-read every 2 s while the grid is open.
- Decided by: the owner (Claude's suggestions).
- Rejected: polling everything every 1-2 s (flyout flicker); a fixed monitor in settings (needs a setting, and there are two external monitors).
- Revisit after To do 2 if the overflow can't be read without visible flicker. Alternative then: turn every icon on in Settings > Personalization > Taskbar > Other system tray icons, so nothing is in the overflow.

## 2026-11-14: C# on .NET 10 with WinForms, no NuGet packages
- Reason: the owner's rule is "only what ships with Windows or .NET". C# on .NET 10 gives modern C#, standard tools (dotnet build, dotnet format) and good VS Code support. WinForms keeps everything in C#, no XAML. .NET 10 is the long-term-support release; .NET 8 support ended in November 2026.
- Decided by: the owner (Claude's suggestion).
- Rejected: PowerShell (slow start, clunky window code, P/Invoke needs embedded C# anyway); the C# 5 compiler that ships with .NET Framework in Windows (nothing to install, but 2012-era C# and weak editor support); WPF (XAML is a second language to read); .NET 11 (just released, no gain for this app).

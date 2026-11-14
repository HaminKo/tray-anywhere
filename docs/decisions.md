# Decisions

Each entry: `## YYYY-MM-DD: decision`, then the reason, who decided, and what was rejected and why. Newest first.

## 2026-11-14: Icon pictures: drawn live for taskbar icons; Windows' stored snapshot, matched by name then order, for overflow icons
- Reason: nothing links a tray icon to its program (Windows' undocumented ITrayNotify, UI Automation and the old Win32 tray all failed, measured). Windows' list of tray icons (NotifyIconSettings) has each program, a snapshot picture and an order that matched the overflow 12 of 12. Matching by name first, then by order for the icons in between, found 13 of 14 app icons, none wrong (measured once; docs/research/uia-tray.md "Spike step C"). Taskbar icons are drawn from the taskbar itself with PrintWindow: the live picture, also under a full-screen window. That fixes two icons that share one entry (two PowerShell scripts) and shows status changes, for about 50 to 75 ms more per read.
- Decided by: the owner (Claude's suggestions), To do 2 step C.
- Rejected: a generic picture whenever the name gives no match (about half the overflow would be generic, and icons with no name couldn't be told apart); the exe's own icon (wrong for Windows' icons and for scripts); opening the flyout to draw the overflow icons (a flash at every read).
- Moves the taskbar part of "Live tray icon pictures" out of Later; overflow icons stay snapshots, which can be older than the real icon.

## 2026-11-14: Read the overflow from its hidden window; refresh it with the visible icons every 2 s
- Reason: the overflow flyout's window exists while closed, and its buttons can be read through UI Automation from the window handle without opening it: no flicker, 16 to 28 ms per read. Its list followed added and removed icons and tooltip changes within a fraction of a second while the flyout stayed closed (measured with a test icon; docs/research/uia-tray.md "Spike step B"). So the reason for reading the overflow only on opening (the flyout would flash) is gone. Replaces the refresh part of "Grid opens by hotkey and floating button..." below.
- Decided by: the owner (Claude's suggestion), To do 2 step B.
- Rejected: opening the flyout to read it (visible flicker, slower); turning every icon on in Settings so nothing is in the overflow (changes the owner's settings and loses the Pinned/Overflow split); still reading the overflow only on opening (the cost is ~20 ms per refresh, and the grid would show stale overflow icons).
- After an Explorer start the window doesn't exist until the flyout has been opened once (owner's test, then a probe). So when it is missing, the code opens and closes the flyout once by invoking the arrow (`ReadOverflowItems(openIfMissing: true)`): a flash of about 0.2 s, and the keyboard focus moves to the taskbar (giving it back from another program failed, measured). It happens once per Explorer start (sign-in, Explorer restart). Callers that read every few seconds pass false, so a failure can't make the flyout flash every 2 s. The arrow is found by its name "Show Hidden Icons" (English Windows only): nothing else tells it apart.
- Fallback decided by: the owner (Claude's suggestion). Rejected: telling the owner to open the arrow once after each sign-in (no flash, but a manual step every day); turning every icon on in Settings (loses the Pinned/Overflow split).

## 2026-11-14: Find the taskbar window with Win32 FindWindow, then use UI Automation
- Reason: asking UI Automation to search the desktop's top-level windows for `Shell_TrayWnd` took 97 to 371 ms per read (35 windows open); `FindWindow` + `AutomationElement.FromHandle` took 1 to 5 ms (measured, details in docs/research/uia-tray.md "Spike step A"). A warm read of the tray dropped from ~130 ms to ~20 to 30 ms, which matters for the 2 s refresh. Win32 P/Invoke is allowed by the no-third-party rule.
- Decided by: Claude during To do 2, step A, from the measurement; the owner can overrule.
- Rejected: UI Automation only (`RootElement.FindFirst`): one fewer concept to read, but 4 to 10 times slower per read.

## 2026-11-14: One solution; the tray code in its own library
- Reason: the dump tool (To do 2) and the app (To do 3) both need the tray-reading code; a class library is the standard way to share it, and it keeps the code a Windows update may break in one place. One TrayAnywhere.slnx at the root, so a plain `dotnet build` builds everything; shared settings (net10.0-windows, nullable on, warnings as errors) in Directory.Build.props.
- Decided by: the owner (Claude's suggestion).
- Rejected: one project for everything (the dump is a console program and the app a windowed one; a windowed exe needs extra code to print to the terminal); linking the same source files into both projects (less standard, and easy to miss a file).

## 2026-11-14: Implicit usings off; a short hand-written .editorconfig
- Reason: with implicit usings off, every file lists the namespaces it uses, so a reader sees where names come from. The .editorconfig (~20 lines) sets indentation, System usings first, file-scoped namespaces and braces always; `dotnet format --verify-no-changes` fails on each of these (tested with a probe file).
- Decided by: the owner (Claude's suggestion).
- Rejected: implicit usings on (fewer lines, but names appear from nowhere); `dotnet new editorconfig` (389 lines of defaults, measured, that nobody would read).

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
- Refresh part replaced (2026-11-14, To do 2 step B): the overflow can be read without opening the flyout, so it is re-read with the visible icons every 2 s. See "Read the overflow from its hidden window" above. The hotkey, button and monitor parts stand.

## 2026-11-14: C# on .NET 10 with WinForms, no NuGet packages
- Reason: the owner's rule is "only what ships with Windows or .NET". C# on .NET 10 gives modern C#, standard tools (dotnet build, dotnet format) and good VS Code support. WinForms keeps everything in C#, no XAML. .NET 10 is the long-term-support release; .NET 8 support ended in November 2026.
- Decided by: the owner (Claude's suggestion).
- Rejected: PowerShell (slow start, clunky window code, P/Invoke needs embedded C# anyway); the C# 5 compiler that ships with .NET Framework in Windows (nothing to install, but 2012-era C# and weak editor support); WPF (XAML is a second language to read); .NET 11 (just released, no gain for this app).

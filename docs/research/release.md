# Release packaging (To do 15)

What a release zip holds, and why it is framework-dependent. Measured 2026-11-18 on this PC (SDK 10.0.404, runtime 10.0.15) unless marked as an estimate.

## Framework-dependent publish
- `dotnet publish src/TrayAnywhere -o <folder>`: 7 files (TrayAnywhere.exe, TrayAnywhere.dll, Tray.dll, their two .pdb files, TrayAnywhere.deps.json, TrayAnywhere.runtimeconfig.json), 297 KB. Zipped with PowerShell's `Compress-Archive`: 136 KB (measured, before the version was set).
- `dotnet publish` builds Release by default (.NET 8 and later; obj\Release is there).
- With `<Version>0.1.0</Version>`, the zip made by docs/releasing.md is 136,428 bytes, and the exe's product version reads `0.1.0+` and the commit's hash (the SDK adds the commit; measured on the unzipped copy).
- It needs the .NET 10 Desktop Runtime on the PC. winget id: `Microsoft.DotNet.DesktopRuntime.10` (checked with `winget show`, version 10.0.15).
- Not tested here (the SDK brings the runtime): what a PC without the runtime shows. .NET's apphost is known to show a dialog that offers the download for a windowed program.

## Self-contained and single-file: can't be built here
- Any publish with a runtime identifier fails with NU1100 ("Unable to resolve 'Microsoft.NETCore.App.Runtime.win-x64 (= 10.0.15)'", also WindowsDesktop and AspNetCore runtime packs): `-r win-x64 --self-contained`, the same with `-p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`, and also framework-dependent single-file (`-p:PublishSingleFile=true -p:RuntimeIdentifier=win-x64 -p:SelfContained=false`).
- Cause: those builds need Microsoft's runtime packs, which come as packages from nuget.org, and this PC's NuGet config has no package sources (`dotnet nuget list source`: "No sources found"). The SDK's own packs folder holds only the reference packs and the win-x64 app host.
- Size of a self-contained build, estimated from the installed runtime folders it would copy: Microsoft.NETCore.App 78.6 MB (188 files) + Microsoft.WindowsDesktop.App 97.3 MB (78 files), so about 175 MB unzipped. WinForms doesn't support trimming (error NETSDK1175), so it can't be made much smaller.
- A self-contained app carries its own runtime, which Windows and winget don't update: each .NET security fix would need a new release.

## Unsigned exe
- SmartScreen: an unsigned exe downloaded from the internet gets "Windows protected your PC"; More info, then Run anyway, runs it.
- Smart App Control (Windows Security > App & browser control), when on, blocks unsigned apps it has no reputation for, and Windows has no way to allow one app; Microsoft suggests turning it off or asking the developer to sign. Source: [What is Smart App Control? (Microsoft Support)](https://support.microsoft.com/en-us/topic/what-is-smart-app-control-285ea03d-fa88-4d56-882e-6698afdb7003). Not seen on this PC.

## An install script beside the zip (not built)
- Windows opens a downloaded .ps1 in Notepad on double-click; running it needs "Run with PowerShell" or an execution-policy change. It would only copy the folder and add a Start menu shortcut.

## Personal details in the repo (searched 2026-11-21, before the first public release)
All measured with git on 42 commits, every branch:
- Current tracked files (`git grep -i` for the user folder's name and the GitHub login; a drive-path pattern such as `C:\`): none. Two hits are made-up examples: `C:\another copy\TrayAnywhere.exe` in src/GuiTest/Program.cs and a `c:\users\...` comment in src/TrayAnywhere/Autostart.cs.
- History (`git log --all -p --format=''` for the changed lines, `-i -G` for the commits, `--name-only` for file names, the commit messages): none. docs/local.md was never tracked; no binary file was ever tracked.
- Outside the "Never in tracked files" list: every commit's author name is the GitHub login, and its author email is the owner's personal address. The repo was private (`gh repo view`: PRIVATE).
- From memory of GitHub's docs, not re-read: anyone can read a public commit's author email (adding `.patch` to the commit's URL shows it); GitHub gives each account a private address of the form `ID+login@users.noreply.github.com` for commits (GitHub Docs, "Setting your commit email address").
- What was done about it: docs/decisions.md, 2026-11-21 ("The public repo starts with one clean commit"), replaced 2026-11-23 ("The repo goes public as it is"): the email stays in the public history.

## The repo page and the first release (2026-11-23)
- Description (set by the owner with `gh repo edit --description`): "Shows Windows 11's system tray icons on your other monitors, in a popup you can click." It says "system tray", the name people search for; Windows' own "notification area" is rarely used.
- Topics: windows-11, system-tray, multi-monitor, taskbar, tray-icon, csharp, winforms. Picked by how many GitHub repos used each name (measured 2026-11-23 with `gh api "search/repositories?q=topic:NAME"`): windows-11 10,208 against windows11 1,158; system-tray 2,676 against systray 168 and notification-area 28; multi-monitor 735 against multiple-monitors 41 and multimonitor 41; taskbar 1,089 against windows-taskbar 42; tray-icon 465; winforms 9,814; csharp 119,051; dotnet 75,630 (left out).
- Before going public (measured): GitHub held only `main` and no tags, so nothing else became visible; GitHub reads LICENSE as MIT. The repo was made public with `gh repo edit --visibility public --accept-visibility-change-consequences`, after the owner looked it over while private.
- 0.1.0: the zip 157,799 bytes, 7 files; the draft checked by the owner (downloaded and run), then published with `gh release edit v0.1.0 --draft=false`; the tag v0.1.0 is on the commit "Shorten How to use". Signed out, the release page answers HTTP 200.

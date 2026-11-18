# Making a release

For the maintainer. A release is a zip of the framework-dependent app (the files `dotnet publish` makes, nothing else) on a GitHub release. Visitors unzip it and run TrayAnywhere.exe (README "Install"). Why this way: docs/decisions.md, 2026-11-18; measurements: docs/research/release.md.

All commands run in PowerShell in the project folder.

1. Run the checks in CLAUDE.md "Build specifics" on the commit to release.
2. Raise `<Version>` in src/TrayAnywhere/TrayAnywhere.csproj (0.1.0, 0.2.0, ...), commit and push.
3. Build the zip in `release\` (gitignored). It is published there, not to `publish\`, which is locked while the everyday copy runs from it:

   ```powershell
   $version = dotnet msbuild src/TrayAnywhere -getProperty:Version
   Remove-Item -Recurse -Force release -ErrorAction SilentlyContinue
   dotnet publish src/TrayAnywhere -o release\tray-anywhere
   Compress-Archive -Path release\tray-anywhere\* -DestinationPath "release\tray-anywhere-$version.zip"
   ```

4. Check the zip: unzip it into a temp folder and start it there with the GUI test's options (its own lock, settings file and registry key, so it leaves the everyday copy's alone). "Runs" means it started and stayed up for 5 s; the script then closes it:

   ```powershell
   $check = "$env:TEMP\tray-anywhere-release-check"
   Remove-Item -Recurse -Force $check -ErrorAction SilentlyContinue
   Expand-Archive "release\tray-anywhere-$version.zip" $check
   $app = Start-Process "$check\TrayAnywhere.exe" -ArgumentList "--gui-test", "--settings", "$check\settings.json", "--registry-key", "Software\tray-anywhere-gui" -PassThru
   Start-Sleep 5
   if ($app.HasExited) { "Failed: exit code $($app.ExitCode)" } else { "Runs"; Stop-Process -Id $app.Id }
   ```

5. Upload it as a draft release (only the repo's owner sees a draft), and open its page to download the zip and try it as a visitor would:

   ```powershell
   gh release create "v$version" "release\tray-anywhere-$version.zip" --draft --title "tray-anywhere $version" --notes "Unzip into a folder you keep and run TrayAnywhere.exe. Needs the .NET 10 Desktop Runtime: see the README."
   gh release view "v$version" --web
   ```

6. Publish it: on its page, Edit, then Publish release. GitHub then makes the tag `v$version` on main's newest pushed commit at that moment, so push nothing new in between.

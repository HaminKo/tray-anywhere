using System;
using Microsoft.Win32;

namespace TrayAnywhere;

// "Start with Windows": a value named tray-anywhere under the per-user Run key, which
// Windows runs at each sign-in. It holds the path of the copy that set it, so the app
// should run from a folder that stays put (the README's publish folder), not from a
// build folder that the next build overwrites.
// Task Manager's Startup apps (and Settings > Apps > Startup) turn it off without
// touching the Run value: they write a value of the same name under
// Explorer\StartupApproved\Run (docs/research/startup-apps.md). The tick reads that too.
internal sealed class Autostart(string registryKey)
{
    // The key, under HKEY_CURRENT_USER, that holds Windows' Run and StartupApproved keys:
    // no admin rights needed. The GUI test gives the app a key of its own instead
    // (--registry-key), with the same keys below it, which Windows never reads.
    public const string WindowsKey = @"Software\Microsoft\Windows\CurrentVersion";
    private const string ValueName = "tray-anywhere";

    private string RunKey => registryKey + @"\Run";
    private string StartupAppsKey => registryKey + @"\Explorer\StartupApproved\Run";

    // This copy's exe, in quotes: Windows would cut a path with a space in it at the space.
    private static string Command => $"\"{Environment.ProcessPath}\"";

    // True when Windows starts this copy at sign-in: the Run value is this copy's exe and
    // Startup apps hasn't turned it off. A value that points at another copy (another
    // folder) counts as off; turning it on then points it here. Case doesn't matter, as in
    // Windows' paths: the same exe started as c:\users\... is this copy.
    public bool IsOn
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            using var startupApps = Registry.CurrentUser.OpenSubKey(StartupAppsKey);
            return string.Equals(run?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase)
                && !IsTurnedOff(startupApps?.GetValue(ValueName));
        }
    }

    // Startup apps' value: 12 bytes, the first one even when on (2), odd when turned off
    // (1 on this PC, 3 in older write-ups), then when. No value counts as on.
    private static bool IsTurnedOff(object? value) => value is byte[] bytes && bytes.Length > 0 && bytes[0] % 2 == 1;

    // Writes or deletes the Run value, then deletes Startup apps' value either way: a tick
    // wins over a "turned off" in Task Manager, and no tick leaves nothing behind.
    // Throws if the registry refuses the change.
    public void Set(bool on)
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on)
            {
                run.SetValue(ValueName, Command);
            }
            else
            {
                run.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        using var startupApps = Registry.CurrentUser.OpenSubKey(StartupAppsKey, writable: true);
        startupApps?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

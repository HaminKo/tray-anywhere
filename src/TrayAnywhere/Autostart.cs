using System;
using Microsoft.Win32;

namespace TrayAnywhere;

// "Start with Windows": a value named tray-anywhere under the per-user Run key, which
// Windows runs at each sign-in. It holds the path of the copy that set it, so the app
// should run from a folder that stays put (the README's publish folder), not from a
// build folder that the next build overwrites.
internal sealed class Autostart(string runKey)
{
    // The Run key Windows reads, under HKEY_CURRENT_USER: no admin rights needed. The GUI
    // test gives the app a key of its own instead (--run-key), which Windows never reads.
    public const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "tray-anywhere";

    // This copy's exe, in quotes: Windows would cut a path with a space in it at the space.
    private static string Command => $"\"{Environment.ProcessPath}\"";

    // True when Windows starts this copy at sign-in. A value that points at another copy
    // (another folder) counts as off; turning it on then points it here. Case doesn't
    // matter, as in Windows' paths: the same exe started as c:\users\... is this copy.
    public bool IsOn
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Writes or deletes the value. Throws if the registry refuses the change.
    public void Set(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKey);
        if (on)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

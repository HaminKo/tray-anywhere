using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tray;

// One entry of Windows' own list of tray icons: one per program and icon ID that has ever
// shown a tray icon, kept after the program ends. Two icons of one program with the same
// ID (two PowerShell scripts) share one entry.
// IsPromoted: shown on the taskbar rather than in the overflow.
// Snapshot: a PNG picture of the icon, possibly older than the real one.
public sealed record IconSetting(string ExePath, string InitialTooltip, bool IsPromoted, byte[]? Snapshot);

// Reads that list from HKCU\Control Panel\NotifyIconSettings. Undocumented: what this file
// relies on was measured on build 26300 (docs/research/uia-tray.md "Spike step C"), so a
// Windows update that changes it is fixed here.
public static class IconSettings
{
    private const string KeyPath = @"Control Panel\NotifyIconSettings";

    // Reads every entry, in the order of the UIOrderList value: the order the tray shows the
    // icons in (the overflow matched it, measured). Throws InvalidOperationException that
    // names the part it could not find.
    public static List<IconSetting> ReadAll()
    {
        using var root = Registry.CurrentUser.OpenSubKey(KeyPath)
            ?? throw new InvalidOperationException($@"Found no registry key HKCU\{KeyPath}.");
        var order = root.GetValue("UIOrderList") as byte[]
            ?? throw new InvalidOperationException($@"Found no UIOrderList value in HKCU\{KeyPath}.");

        // UIOrderList holds one 8-byte number per entry: the name of the entry's subkey.
        var entries = new List<IconSetting>();
        for (var i = 0; i + 8 <= order.Length; i += 8)
        {
            var subkeyName = BitConverter.ToUInt64(order, i).ToString(CultureInfo.InvariantCulture);
            using var entry = root.OpenSubKey(subkeyName);
            if (entry == null)
            {
                continue;
            }
            entries.Add(new IconSetting(
                ResolveKnownFolder(entry.GetValue("ExecutablePath") as string ?? ""),
                entry.GetValue("InitialTooltip") as string ?? "",
                entry.GetValue("IsPromoted") is int promoted && promoted == 1,
                entry.GetValue("IconSnapshot") as byte[]));
        }
        return entries;
    }

    // ExecutablePath is a plain path, or a known folder's ID followed by the rest, e.g.
    // "{6D809377-6AF0-444B-8957-A3773F02200E}\Microsoft OneDrive\OneDrive.exe" for
    // Program Files. Returns the path unchanged if the ID can't be resolved.
    private static string ResolveKnownFolder(string path)
    {
        var end = path.IndexOf('}');
        if (!path.StartsWith('{') || end < 0 || !Guid.TryParse(path.Substring(1, end - 1), out var folderId))
        {
            return path;
        }
        if (SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out var folder) != 0)
        {
            return path;
        }
        return folder + path.Substring(end + 1);
    }

    // Returns 0 on success. The string comes back in memory that the marshaller frees.
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out string path);
}

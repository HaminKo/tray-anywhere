using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Tray;

// Finds the program behind each app icon, and Windows' stored picture of it, by matching
// the icons to Windows' list of tray icons (IconSettings). Nothing links an icon to its
// entry exactly (measured: docs/research/uia-tray.md "Spike step C"), so this is a guess:
// 1. Candidates: entries whose program runs now and that sit where the icon is (on the
//    taskbar or in the overflow), in Windows' order, which is the tray's order.
// 2. An icon whose name points to a candidate takes the first such candidate after the
//    previous match.
// 3. Icons without such a name, between two matches, take the candidates between those
//    two in order; extra icons stay without a program. An extra candidate there (a
//    program that runs without an icon) does no harm after the last icon, but before it
//    shifts the icons after it onto the wrong programs. Seen on this PC only at the end.
public static class IconOwners
{
    // A candidate entry, with what its icon's name may contain.
    private sealed record Candidate(IconSetting Setting, string FileName, string Description);

    // Returns the items with ExePath, OwnerFoundBy and Picture filled in where an owner was
    // found; a Picture the item already has (drawn from the taskbar) is kept. Windows'
    // own buttons (kind System) are left as they are.
    // Throws InvalidOperationException if Windows' list of tray icons can't be read.
    public static List<TrayItem> Find(List<TrayItem> items)
    {
        var settings = IconSettings.ReadAll();
        var running = RunningPrograms();
        var result = new List<TrayItem>(items);
        foreach (var kind in new[] { TrayItemKind.Pinned, TrayItemKind.Overflow })
        {
            var onTaskbar = kind == TrayItemKind.Pinned;
            var candidates = settings
                .Where(s => s.IsPromoted == onTaskbar && running.Contains(s.ExePath))
                .Select(s => new Candidate(s, Path.GetFileNameWithoutExtension(s.ExePath), Description(s.ExePath)))
                .ToList();
            var icons = Enumerable.Range(0, result.Count).Where(i => result[i].Kind == kind).ToList();
            MatchInOrder(result, icons, candidates);
        }
        return result;
    }

    // Steps 2 and 3 above, for one part of the tray. icons: positions in items, in tray order.
    private static void MatchInOrder(List<TrayItem> items, List<int> icons, List<Candidate> candidates)
    {
        var next = 0;                    // the first candidate after the previous match
        var waiting = new List<int>();   // icons without a matching name since then
        foreach (var i in icons)
        {
            var named = candidates.FindIndex(next, c => NamePointsTo(items[i].Name, c));
            if (named < 0)
            {
                waiting.Add(i);
                continue;
            }
            PairInOrder(items, waiting, candidates.GetRange(next, named - next));
            waiting.Clear();
            items[i] = WithOwner(items[i], candidates[named], OwnerFoundBy.Name);
            next = named + 1;
        }
        PairInOrder(items, waiting, candidates.GetRange(next, candidates.Count - next));
    }

    // The first waiting icon gets the first candidate, and so on, as far as both go.
    private static void PairInOrder(List<TrayItem> items, List<int> waiting, List<Candidate> candidates)
    {
        for (var k = 0; k < waiting.Count && k < candidates.Count; k++)
        {
            items[waiting[k]] = WithOwner(items[waiting[k]], candidates[k], OwnerFoundBy.Order);
        }
    }

    private static TrayItem WithOwner(TrayItem item, Candidate owner, OwnerFoundBy foundBy) =>
        item with { ExePath = owner.Setting.ExePath, OwnerFoundBy = foundBy, Picture = item.Picture ?? owner.Setting.Snapshot };

    // Whether an icon's name points to this candidate: the name starts with the entry's first
    // tooltip ("New quick note"), or holds the program's file name ("Steam") or its
    // description ("Google Drive" for GoogleDriveFS.exe). Ignores case.
    private static bool NamePointsTo(string name, Candidate candidate)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return false;
        }
        var firstTooltip = candidate.Setting.InitialTooltip.Trim();
        return (firstTooltip.Length > 0 && name.StartsWith(firstTooltip, StringComparison.OrdinalIgnoreCase))
            || (candidate.FileName.Length > 0 && name.Contains(candidate.FileName, StringComparison.OrdinalIgnoreCase))
            || (candidate.Description.Length > 0 && name.Contains(candidate.Description, StringComparison.OrdinalIgnoreCase));
    }

    // The exe's description from its version info ("Google Drive"), or "" if there is none.
    // A running program's exe can be gone: an update may replace it while it runs.
    private static string Description(string exePath) =>
        File.Exists(exePath) ? FileVersionInfo.GetVersionInfo(exePath).FileDescription ?? "" : "";

    // The full paths of the programs that run now. Asks each process with limited rights,
    // which Windows also grants for programs that run as administrator (Task Manager often
    // does); .NET's Process.MainModule needs more rights and fails there.
    private static HashSet<string> RunningPrograms()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
                if (handle == IntPtr.Zero)
                {
                    // Windows' protected processes; they have no tray icons.
                    continue;
                }
                var path = new StringBuilder(1024);
                var length = path.Capacity;
                if (QueryFullProcessImageName(handle, 0, path, ref length))
                {
                    paths.Add(path.ToString());
                }
                CloseHandle(handle);
            }
        }
        return paths;
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int length);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tray;

namespace TrayAnywhere;

// What the grid shows: the app icons on the taskbar and in the overflow, and what went
// wrong in the read, if anything (one line per failed part).
internal sealed record TrayState(List<TrayItem> Pinned, List<TrayItem> Overflow, string? Problem)
{
    public static readonly TrayState Empty = new([], [], null);
}

// Reads the tray on a background thread: a full read takes about 150 ms (measured in
// To do 2), too long for the UI thread. One read at a time: two at once could both open
// the flyout.
internal sealed class TrayReader
{
    // The last read started, under way or done. Each read waits for the one before it and
    // starts from its result, so reads never overlap.
    private Task<TrayState> latest = Task.FromResult(TrayState.Empty);
    // Whether `latest` may open the flyout.
    private bool latestMayOpenFlyout;

    // Starts a read, or returns the one under way when that one does what this caller
    // asks. Call it only from the UI thread, so no lock is needed.
    // openIfMissing: open the flyout once if Explorer hasn't created the overflow window
    // yet (see Taskbar.ReadOverflowItems).
    public Task<TrayState> ReadAsync(bool openIfMissing)
    {
        // A read under way is fresh enough, unless this caller needs the flyout opened
        // when its window is missing and that read won't do it.
        if (!latest.IsCompleted && (latestMayOpenFlyout || !openIfMissing))
        {
            return latest;
        }
        var previous = latest;
        // Read never throws, so awaiting the previous read never throws either.
        latest = Task.Run(async () => Read(openIfMissing, await previous));
        latestMayOpenFlyout = openIfMissing;
        return latest;
    }

    // The taskbar, the overflow and the programs behind the icons are read one by one,
    // so one failing doesn't lose the others (as in TrayDump). A part that fails keeps
    // its icons from the last read.
    private static TrayState Read(bool openIfMissing, TrayState last)
    {
        var problems = new List<string>();
        var visible = TryRead("Taskbar", Taskbar.ReadVisibleItems, problems);
        var overflow = TryRead("Overflow", () => Taskbar.ReadOverflowItems(openIfMissing, out _), problems);

        var found = (visible ?? []).Concat(overflow ?? []).ToList();
        // Programs and overflow pictures: without them the icons still show, with a
        // generic picture.
        var items = TryRead("Programs", () => IconOwners.Find(found), problems) ?? found;

        var pinned = visible == null ? last.Pinned : items.Where(item => item.Kind == TrayItemKind.Pinned).ToList();
        var hidden = overflow == null ? last.Overflow : items.Where(item => item.Kind == TrayItemKind.Overflow).ToList();
        return new TrayState(pinned, hidden, problems.Count == 0 ? null : string.Join("\n", problems));
    }

    // Runs one part of the read. If it fails, notes why and returns null. Catches every
    // exception type: when an icon goes away or Explorer restarts during a read, UI
    // Automation throws several kinds (ElementNotAvailable, COM errors, timeouts), the
    // next read tries again, and an unhandled one would end the app.
    private static List<TrayItem>? TryRead(string part, Func<List<TrayItem>> read, List<string> problems)
    {
        try
        {
            return read();
        }
        catch (Exception e)
        {
            problems.Add($"{part} not read: {e.Message}");
            return null;
        }
    }
}

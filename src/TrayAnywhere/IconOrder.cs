using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tray;

namespace TrayAnywhere;

// The order of the grid's icons that the user set by dragging them (To do 14), kept in
// settings.json as a list of keys per section. Only the grid's order: the real tray keeps
// Windows' order. Each read of the tray is sorted by the saved keys before it shows.
internal static class IconOrder
{
    // What tells an icon apart from one read to the next: its program's file name
    // ("steam.exe"), not its full path, which changes when an app updates itself into a
    // new folder (Discord's app-1.0.x). Without a program, its name: that can change with
    // the tooltip, and the icon then counts as new (1 of 16 icons on the owner's PC,
    // measured in To do 14).
    public static string Key(TrayItem item) =>
        item.ExePath != null ? Path.GetFileName(item.ExePath) : item.Name.Trim();

    // The icons with a saved key first, in the saved order; the others after them, in
    // tray order. Icons with the same key (one program's two icons) keep their tray order
    // among themselves: OrderBy keeps the order of equal ones.
    public static List<TrayItem> Sort(List<TrayItem> items, List<string> saved) =>
        items.OrderBy(item => PlaceOf(Key(item), saved)).ToList();

    // The keys to save after a drag: the section's icons in their new order, and the saved
    // keys of icons that aren't there now at their old places, so a program that was
    // closed during the drag gets its place back when it starts again.
    public static List<string> Merge(List<TrayItem> items, List<string> saved)
    {
        var keys = items.Select(Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < saved.Count; i++)
        {
            if (!keys.Contains(saved[i], StringComparer.OrdinalIgnoreCase))
            {
                keys.Insert(Math.Min(i, keys.Count), saved[i]);
            }
        }
        return keys;
    }

    // A key's place in the saved order; after all of them if it isn't there. Ignores case:
    // Windows writes a program's path in either case ("ONENOTEM.EXE").
    private static int PlaceOf(string key, List<string> saved)
    {
        var place = saved.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        return place < 0 ? int.MaxValue : place;
    }
}

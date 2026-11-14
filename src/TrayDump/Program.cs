using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Tray;

// TrayDump: prints the tray icons that src/Tray reads from the live taskbar, one per
// line, and how long the reading took.

List<TrayItem> items;
long firstRead, secondRead;
try
{
    var clock = Stopwatch.StartNew();
    items = Taskbar.ReadVisibleItems();
    firstRead = clock.ElapsedMilliseconds;

    // Read again: the cost of a refresh once UI Automation has started up.
    clock.Restart();
    Taskbar.ReadVisibleItems();
    secondRead = clock.ElapsedMilliseconds;
}
catch (InvalidOperationException e)
{
    // Exit code 1, so a script that runs this as a check sees the failure.
    Console.WriteLine(e.Message);
    return 1;
}

for (var i = 0; i < items.Count; i++)
{
    var item = items[i];
    var kind = "[" + item.Kind.ToString().ToLowerInvariant() + "]";
    var place = $"{item.Bounds.X},{item.Bounds.Y}";
    var size = $"{item.Bounds.Width}x{item.Bounds.Height}";
    Console.WriteLine($"{i + 1,3}  {kind,-10} {place,-10} {size,-7}  {OneLine(item.Name)}");
}
Console.WriteLine();
Console.WriteLine($"{items.Count} items. First read {firstRead} ms (includes start-up), second read {secondRead} ms.");
return 0;

// The tooltip text on one line: its lines joined with " / ".
static string OneLine(string name)
{
    // Format characters (Unicode category Cf), such as the left-to-right marks in the
    // clock's date, are invisible but print as "?" in some consoles.
    var visible = string.Concat(name.Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.Format));
    var lines = visible.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return lines.Length == 0 ? "(no name)" : string.Join(" / ", lines);
}

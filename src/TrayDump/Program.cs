using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Tray;

// TrayDump: prints the tray icons that src/Tray reads from the live taskbar and from
// the overflow, one per line, and how long the reading took.

var clock = Stopwatch.StartNew();
List<TrayItem> visible;
long firstRead, secondRead;
try
{
    visible = Taskbar.ReadVisibleItems();
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

// Read from the overflow's hidden window; the flyout is opened once only if Explorer
// hasn't created that window yet. Read on its own, so if it fails, the taskbar's icons
// are still listed.
var overflow = new List<TrayItem>();
var openedFlyout = false;
string? overflowError = null;
clock.Restart();
try
{
    overflow = Taskbar.ReadOverflowItems(openIfMissing: true, out openedFlyout);
}
catch (InvalidOperationException e)
{
    overflowError = e.Message;
}
var overflowRead = clock.ElapsedMilliseconds;

// One numbering for both lists, so each icon has its own number.
var items = visible.Concat(overflow).ToList();
for (var i = 0; i < items.Count; i++)
{
    var item = items[i];
    var kind = "[" + item.Kind.ToString().ToLowerInvariant() + "]";
    // Overflow items have no position while the flyout is closed.
    var place = item.Bounds.IsEmpty ? "-" : $"{item.Bounds.X},{item.Bounds.Y}";
    var size = item.Bounds.IsEmpty ? "-" : $"{item.Bounds.Width}x{item.Bounds.Height}";
    Console.WriteLine($"{i + 1,3}  {kind,-10} {place,-10} {size,-7}  {OneLine(item.Name)}");
}
Console.WriteLine();
Console.WriteLine($"{visible.Count} on the taskbar, {overflow.Count} in the overflow.");
Console.WriteLine($"Taskbar: first read {firstRead} ms (includes start-up), second read {secondRead} ms. Overflow: {overflowRead} ms.");
if (openedFlyout)
{
    Console.WriteLine("Opened the overflow flyout once: Explorer hadn't created its window yet.");
}
if (overflowError != null)
{
    Console.WriteLine("Overflow not read: " + overflowError);
    return 1;
}
return 0;

// The tooltip text on one line: its lines joined with " / ".
static string OneLine(string name)
{
    // Format characters (Unicode category Cf), such as the left-to-right marks in the
    // clock's date, are invisible but print as "?" in some consoles.
    var printable = string.Concat(name.Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.Format));
    var lines = printable.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return lines.Length == 0 ? "(no name)" : string.Join(" / ", lines);
}

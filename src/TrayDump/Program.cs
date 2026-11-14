using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Tray;

// TrayDump: prints the tray icons that src/Tray reads from the live taskbar and from
// the overflow, one per line, with the program behind each app icon and how long the
// reading took. "TrayDump pictures" also saves each icon's picture as a PNG file.

var savePictures = args.Length == 1 && args[0] == "pictures";
if (args.Length > 0 && !savePictures)
{
    Console.WriteLine("Usage: TrayDump [pictures]");
    return 2;
}

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

// One numbering for both lists, so each icon has its own number. The programs are found
// on their own too: if Windows' list of tray icons can't be read, the icons are still listed.
var items = visible.Concat(overflow).ToList();
string? ownersError = null;
clock.Restart();
try
{
    items = IconOwners.Find(items);
}
catch (InvalidOperationException e)
{
    ownersError = e.Message;
}
var ownersRead = clock.ElapsedMilliseconds;

for (var i = 0; i < items.Count; i++)
{
    var item = items[i];
    var kind = "[" + item.Kind.ToString().ToLowerInvariant() + "]";
    // Overflow items have no position while the flyout is closed.
    var place = item.Bounds.IsEmpty ? "-" : $"{item.Bounds.X},{item.Bounds.Y}";
    var size = item.Bounds.IsEmpty ? "-" : $"{item.Bounds.Width}x{item.Bounds.Height}";
    Console.WriteLine($"{i + 1,3}  {kind,-10} {place,-10} {size,-7}  {Owner(item),-32} {OneLine(item.Name)}");
}
Console.WriteLine();
var appIcons = items.Where(item => item.Kind != TrayItemKind.System).ToList();
Console.WriteLine($"{visible.Count} on the taskbar, {overflow.Count} in the overflow.");
Console.WriteLine(
    $"Programs of the {appIcons.Count} app icons: {appIcons.Count(item => item.OwnerFoundBy == OwnerFoundBy.Name)} by name, "
    + $"{appIcons.Count(item => item.OwnerFoundBy == OwnerFoundBy.Order)} by order, "
    + $"{appIcons.Count(item => item.OwnerFoundBy == OwnerFoundBy.None)} not found. "
    + $"Pictures: {appIcons.Count(item => item.Picture != null)}.");
Console.WriteLine(
    $"Taskbar: first read {firstRead} ms (includes start-up), second read {secondRead} ms. "
    + $"Overflow: {overflowRead} ms. Programs: {ownersRead} ms.");
if (openedFlyout)
{
    Console.WriteLine("Opened the overflow flyout once: Explorer hadn't created its window yet.");
}
if (savePictures)
{
    SavePictures(items);
}
if (overflowError != null)
{
    Console.WriteLine("Overflow not read: " + overflowError);
}
if (ownersError != null)
{
    Console.WriteLine("Programs not found: " + ownersError);
}
return overflowError != null || ownersError != null ? 1 : 0;

// The program behind an app icon and how it was found, e.g. "steam.exe (name)";
// "?" if not found. Empty for Windows' own buttons.
static string Owner(TrayItem item)
{
    if (item.Kind == TrayItemKind.System)
    {
        return "";
    }
    if (item.ExePath == null)
    {
        return "?";
    }
    return $"{Path.GetFileName(item.ExePath)} ({item.OwnerFoundBy.ToString().ToLowerInvariant()})";
}

// The tooltip text on one line: its lines joined with " / ".
static string OneLine(string name)
{
    // Format characters (Unicode category Cf), such as the left-to-right marks in the
    // clock's date, are invisible but print as "?" in some consoles.
    var printable = string.Concat(name.Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.Format));
    var lines = printable.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return lines.Length == 0 ? "(no name)" : string.Join(" / ", lines);
}

// Saves each item's picture as NN.png (NN: its number in the list) in a folder of its own
// under the temp folder, after removing the PNG files an earlier run left there.
static void SavePictures(List<TrayItem> items)
{
    var folder = Path.Combine(Path.GetTempPath(), "tray-anywhere-pictures");
    Directory.CreateDirectory(folder);
    foreach (var old in Directory.GetFiles(folder, "*.png"))
    {
        File.Delete(old);
    }
    var saved = 0;
    for (var i = 0; i < items.Count; i++)
    {
        if (items[i].Picture is byte[] png)
        {
            File.WriteAllBytes(Path.Combine(folder, $"{i + 1:00}.png"), png);
            saved++;
        }
    }
    Console.WriteLine($"Saved {saved} pictures to {folder}");
}

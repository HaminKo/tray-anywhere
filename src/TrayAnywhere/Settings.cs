using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Tray;

namespace TrayAnywhere;

// A place on a monitor: a window's top-left corner, in physical pixels from the top-left
// corner of the monitor. From the monitor's corner, so moving the monitors around in
// Windows' Display settings keeps the places.
internal sealed record Offset(int X, int Y);

// The settings file (%APPDATA%\tray-anywhere\settings.json): where the user dragged each
// floating button and the grid, per monitor, the grid's icon order, which monitors have a
// button and how each looks, and the hotkey. Each change saves it at once.
internal sealed class Settings
{
    // By the monitor's name without "\\.\" (e.g. DISPLAY5). Windows can give a monitor a
    // new name after it is plugged in again; its places are then forgotten.
    public Dictionary<string, Offset> ButtonPlaces { get; set; } = new();
    public Dictionary<string, Offset> GridPlaces { get; set; } = new();
    // The grid's icon order in each section, as IconOrder's keys. Empty: the tray's order.
    public List<string> PinnedOrder { get; set; } = [];
    public List<string> OverflowOrder { get; set; } = [];
    // The secondary monitors whose floating button the user turned off, by name as above.
    // Empty: a button on every secondary monitor.
    public List<string> ButtonsOff { get; set; } = [];
    // Whether the main monitor has a floating button too; off by default, as its taskbar
    // has the real tray. Kept for whichever monitor is the main one, not by name.
    public bool MainButtonOn { get; set; }
    // Each monitor's floating button: its color, size and always on top, by name as above.
    // A monitor not listed has the defaults (ButtonLook).
    public Dictionary<string, ButtonLook> ButtonLooks { get; set; } = new();
    // The keys that open and close the grid. Not in the file (or null): the default,
    // Alt+`, which isn't written, so the file holds only what the user changed.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HotkeyKeys? Hotkey { get; set; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tray-anywhere", "settings.json");

    // Indented, so the file is easy to read. A missing list (null) is a broken file, not
    // an empty list. Colors and sizes by name ("Blue", "Large"); a number or an unknown
    // name in their place is a broken file too.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    // The file these settings were read from and are saved to.
    private string path = "";
    // False when the file exists but couldn't be read: the app then never writes over it,
    // so a hand edit with a mistake in it isn't lost.
    private bool mayWrite = true;

    // Reads the settings, or the defaults if there is no file yet. problem: why an
    // existing file couldn't be read, or null.
    public static Settings Load(string path, out string? problem)
    {
        problem = null;
        Settings? settings = null;
        try
        {
            if (File.Exists(path))
            {
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Options);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            problem = e.Message;
        }
        // A file holding only "null" reads as null: the defaults.
        settings ??= new Settings();
        settings.path = path;
        settings.mayWrite = problem == null;
        return settings;
    }

    public Point? ButtonPlace(Screen monitor) => PlaceIn(ButtonPlaces, monitor);

    public Point? GridPlace(Screen monitor) => PlaceIn(GridPlaces, monitor);

    public void SetButtonPlace(Screen monitor, Point place) => SetPlace(ButtonPlaces, monitor, place);

    public void SetGridPlace(Screen monitor, Point place) => SetPlace(GridPlaces, monitor, place);

    // The saved icon order of a section (Pinned or Overflow).
    public List<string> OrderOf(TrayItemKind section) => section == TrayItemKind.Pinned ? PinnedOrder : OverflowOrder;

    public void SetOrder(TrayItemKind section, List<string> keys)
    {
        if (section == TrayItemKind.Pinned)
        {
            PinnedOrder = keys;
        }
        else
        {
            OverflowOrder = keys;
        }
        Save();
    }

    // "Show the floating button" in the settings window.
    public bool ButtonOn(Screen monitor) => monitor.Primary ? MainButtonOn : !ButtonsOff.Contains(Key(monitor));

    public void SetButtonOn(Screen monitor, bool on)
    {
        if (monitor.Primary)
        {
            MainButtonOn = on;
        }
        else
        {
            // Removed first, so the monitor is never listed twice.
            ButtonsOff.Remove(Key(monitor));
            if (!on)
            {
                ButtonsOff.Add(Key(monitor));
            }
        }
        Save();
    }

    // The color, size and always on top of the monitor's button in the settings window. A
    // look written as null in the file counts as the defaults.
    public ButtonLook LookOf(Screen monitor) => ButtonLooks.GetValueOrDefault(Key(monitor)) ?? new ButtonLook();

    public void SetLook(Screen monitor, ButtonLook look)
    {
        ButtonLooks[Key(monitor)] = look;
        Save();
    }

    // The hotkey in the settings window.
    public void SetHotkey(HotkeyKeys keys)
    {
        Hotkey = keys == HotkeyKeys.Default ? null : keys;
        Save();
    }

    // True if icons were dragged into an order of their own in either section. A method,
    // not a property: a public property would be written into the file.
    public bool HasOrder() => PinnedOrder.Count > 0 || OverflowOrder.Count > 0;

    // Back to the tray's order in both sections.
    public void ResetOrder()
    {
        PinnedOrder = [];
        OverflowOrder = [];
        Save();
    }

    // The saved place on this monitor, in screen pixels, or null if there is none. A place
    // written as null in the file also counts as none.
    private static Point? PlaceIn(Dictionary<string, Offset> places, Screen monitor) =>
        places.GetValueOrDefault(Key(monitor)) is Offset offset
            ? new Point(monitor.Bounds.X + offset.X, monitor.Bounds.Y + offset.Y)
            : null;

    private void SetPlace(Dictionary<string, Offset> places, Screen monitor, Point place)
    {
        places[Key(monitor)] = new Offset(place.X - monitor.Bounds.X, place.Y - monitor.Bounds.Y);
        Save();
    }

    // "\\.\DISPLAY5" becomes "DISPLAY5": the backslashes would be doubled in the file.
    private static string Key(Screen monitor) => monitor.DeviceName.TrimStart('\\', '.');

    private void Save()
    {
        if (!mayWrite)
        {
            return;
        }
        try
        {
            new FileInfo(path).Directory?.Create();
            // Written to a new file, then put in place of the old one, so a crash or a
            // full disk halfway through leaves the old file whole.
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only this change is lost; the app goes on and tries again at the next one.
        }
    }
}

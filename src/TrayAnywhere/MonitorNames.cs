using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TrayAnywhere;

// A monitor and the name the settings window lists it by.
internal sealed record NamedMonitor(Screen Monitor, string Name);

// Names for the monitors by their place, as Windows only names them like DISPLAY5:
// "Main monitor" first, then the others from left to right. Each name has the monitor's
// size in pixels, and "(this one)" for the monitor the settings window is on, e.g.
// "Right monitor, 3840 × 2160 (this one)". Windows' model names ("DELL U2720Q") would take
// many display-config calls, and two monitors of one model would still need their place.
internal static class MonitorNames
{
    // All monitors in the window's order, named. here: the monitor the window is on.
    public static List<NamedMonitor> InOrder(Screen here)
    {
        var all = Screen.AllScreens;
        // By the left edge, then the top, for two monitors one above the other.
        var others = all.Where(m => !m.Primary).OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
        var list = new List<NamedMonitor>();
        foreach (var main in all.Where(m => m.Primary))
        {
            list.Add(new NamedMonitor(main, Name("Main monitor", main, here)));
        }
        for (var i = 0; i < others.Count; i++)
        {
            list.Add(new NamedMonitor(others[i], Name(PlaceName(i, others.Count), others[i], here)));
        }
        return list;
    }

    // The name of the external monitor at this place from the left (0: leftmost), out of
    // this many external monitors.
    private static string PlaceName(int index, int count) => count switch
    {
        1 => "External monitor",
        2 => index == 0 ? "Left monitor" : "Right monitor",
        _ => $"Monitor {index + 1} from the left",
    };

    // The place's name with the monitor's size, in physical pixels as the app works in.
    private static string Name(string place, Screen monitor, Screen here)
    {
        var thisOne = monitor.DeviceName == here.DeviceName ? " (this one)" : "";
        return $"{place}, {monitor.Bounds.Width} × {monitor.Bounds.Height}{thisOne}";
    }
}

using System.Windows.Automation;

namespace Tray;

// Reads the Windows taskbar through UI Automation. To do 2 fills this in.
public static class Taskbar
{
    // True if UI Automation finds the primary taskbar's window (class Shell_TrayWnd)
    // among the desktop's top-level windows.
    public static bool PrimaryTaskbarFound()
    {
        var isTaskbar = new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd");
        return AutomationElement.RootElement.FindFirst(TreeScope.Children, isTaskbar) != null;
    }
}

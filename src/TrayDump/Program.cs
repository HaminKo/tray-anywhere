using System;
using Tray;

// TrayDump will list the tray icons as text (To do 2).
// For now it only shows that UI Automation can reach the taskbar.
if (!Taskbar.PrimaryTaskbarFound())
{
    // Exit code 1, so a script that runs this as a check sees the failure.
    Console.WriteLine("UI Automation answered, but found no primary taskbar (Shell_TrayWnd).");
    return 1;
}

Console.WriteLine("UI Automation works: found the primary taskbar.");
return 0;

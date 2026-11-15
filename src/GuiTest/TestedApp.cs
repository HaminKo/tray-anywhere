using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;
using static GuiTest.Win32;

namespace GuiTest;

// The app under test: the copy of TrayAnywhere.exe next to this program (GuiTest.csproj
// puts it there), started with the test's own settings file and Run key. Finds the
// app's windows by the names it gives them for the test.
internal sealed class TestedApp : IDisposable
{
    // The test's registry key under HKEY_CURRENT_USER, deleted at the start and end of
    // each run. The app writes "Start with Windows" to its Run key instead of Windows'
    // (src/TrayAnywhere/Autostart.cs), which this test never changes.
    public const string TestKey = @"Software\tray-anywhere-gui";
    public const string TestRunKey = TestKey + @"\Run";
    public const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly Process process;
    // The window in front as the app starts: the user's (VS Code, a terminal).
    private readonly IntPtr userWindow = GetForegroundWindow();

    // settingsPath: the file the app reads its places from, and saves a dragged one to.
    // --gui-test: the app runs beside the user's copy, with a lock of its own (LockName)
    // and without the real hotkey, which stays the user's copy's.
    public TestedApp(string settingsPath)
    {
        process = Process.Start(Exe, ["--gui-test", "--settings", settingsPath, "--run-key", TestRunKey]);
    }

    // The one-copy locks (src/TrayAnywhere/Program.cs): the test's copy, the user's copy.
    public const string LockName = @"Local\tray-anywhere-gui";
    public const string UserLockName = @"Local\tray-anywhere";

    // True while a copy of the app holds this lock.
    public static bool IsRunning(string lockName)
    {
        if (!Mutex.TryOpenExisting(lockName, out var running))
        {
            return false;
        }
        running.Dispose();
        return true;
    }

    // The app's exe: what "Start with Windows" writes to the Run key.
    public static string Exe => Path.Combine(AppContext.BaseDirectory, "TrayAnywhere.exe");

    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;

    // The app's value in this Run key (named tray-anywhere), or null if there is none.
    public static string? RunValue(string runKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        return key?.GetValue("tray-anywhere") as string;
    }

    public List<AppWindow> Buttons() => TopWindows(new PropertyCondition(AutomationElement.AutomationIdProperty, "FloatingButton"));

    // The open grid, or null.
    public AppWindow? Grid() => TopWindows(new PropertyCondition(AutomationElement.AutomationIdProperty, "Grid")).FirstOrDefault();

    // The grid's tiles in their order: the Pinned ones, then the Overflow ones.
    public static List<AppWindow> Tiles(AppWindow grid) =>
        Find(grid.Element, TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

    // An item with this name in a right-click menu the app has open, or null.
    public AutomationElement? MenuItem(string name)
    {
        foreach (var menu in TopWindows(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Menu)))
        {
            var item = menu.Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
            if (item != null)
            {
                return item;
            }
        }
        return null;
    }

    // Sends the app's hidden hotkey window the message Windows sends it for Alt+`. The
    // real keys would reach the user's copy, or the window in front. The user's copy has
    // a hotkey window of the same title: each is looked at until the one of this copy.
    public void PressHotkey()
    {
        var window = IntPtr.Zero;
        while (true)
        {
            window = FindWindowEx(MessageOnlyParent, window, null, "tray-anywhere hotkey");
            if (window == IntPtr.Zero)
            {
                throw new CheckFailed("the app's hotkey window isn't there");
            }
            GetWindowThreadProcessId(window, out var processId);
            if (processId == process.Id)
            {
                break;
            }
        }
        // 1: the hotkey's number (Hotkey.cs).
        PostMessage(window, WmHotkey, new IntPtr(1), IntPtr.Zero);
    }

    // Gives the front back to the user's window if one of the app's took it. The grid
    // brings itself to the front when it opens, when Windows lets it, and keys the user
    // types would then go to it, where Space or Enter clicks a real tray icon's tile
    // (docs/research/gui-testing.md). Windows may refuse; then the user is told.
    public void GiveFrontBack()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        if (processId == process.Id && !SetForegroundWindow(userWindow))
        {
            Console.WriteLine("note  the grid has the keyboard: don't type until the test ends");
        }
    }

    // The app's top-level windows that meet the condition.
    private List<AppWindow> TopWindows(Condition condition) => Find(
        AutomationElement.RootElement,
        TreeScope.Children,
        new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id), condition));

    private static List<AppWindow> Find(AutomationElement parent, TreeScope scope, Condition condition) =>
        parent.FindAll(scope, condition).Cast<AutomationElement>().Select(element => new AppWindow(element)).ToList();

    // Ends the app if it is still running: after a failed check, or when the test or the
    // screenshots are done with it. The app has nothing to save on exit (each drag saves
    // settings.json at once).
    public void Dispose()
    {
        if (!process.HasExited)
        {
            process.Kill();
            process.WaitForExit();
        }
        process.Dispose();
    }
}

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace GuiTest;

// A check that failed; it stops the test.
internal sealed class CheckFailed(string message) : Exception(message);

// The test's checks. Each passed one prints a line saying what works; the first that
// fails stops the test with what was expected and what was found.
internal static class Checks
{
    // DoesNotReturnIf: after Check(x != null, ...) the compiler knows x isn't null.
    public static void Check([DoesNotReturnIf(false)] bool passed, string what, string found)
    {
        if (!passed)
        {
            throw new CheckFailed($"{what}: {found}");
        }
        Console.WriteLine($"ok    {what}");
    }

    public static void Skipped(string what) => Console.WriteLine($"skip  {what}");

    // Waits until the condition holds, for at most this many seconds: the app does what
    // it is told on its own thread, a little later. found: what there is instead, for the
    // message if it never holds.
    public static void WaitUntil(string what, Func<bool> condition, double seconds = 5, Func<string>? found = null)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (condition())
                {
                    Console.WriteLine($"ok    {what}");
                    return;
                }
            }
            catch (ElementNotAvailableException)
            {
                // A window went away while being read (a grid closing, tiles laid out
                // again at a refresh): look again.
            }
            if (watch.Elapsed.TotalSeconds > seconds)
            {
                throw new CheckFailed($"{what}: not within {seconds} s{(found == null ? "" : $"; {found()}")}");
            }
            // Lets this thread's window messages through: the test's tray icon gets its
            // clicks that way.
            Application.DoEvents();
            Thread.Sleep(50);
        }
    }

    // Waits until find returns something, and returns that.
    public static T WaitFor<T>(string what, Func<T?> find, double seconds = 5) where T : class
    {
        T? found = null;
        WaitUntil(what, () => (found = find()) != null, seconds);
        return found!;
    }
}

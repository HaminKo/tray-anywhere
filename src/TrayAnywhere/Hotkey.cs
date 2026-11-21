using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// A global hotkey: Windows sends WM_HOTKEY to this hidden window when the keys are
// pressed, whichever program has the focus.
internal sealed class Hotkey : NativeWindow, IDisposable
{
    // Holding the keys down sends one press, not a stream of them.
    private const uint NoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;
    // The hotkey's number for this window; it has only one.
    private const int HotkeyId = 1;
    // A message-only window: never shown, only receives messages.
    private static readonly IntPtr MessageOnlyParent = new(-3);

    // False for the GUI test's copy, which leaves the keys to the user's copy: it keeps
    // and shows them, but never takes them.
    private readonly bool use;
    private bool registered;

    // The window's title: never shown, but the GUI test finds the window by it and sends
    // it WM_HOTKEY in place of the real keys. keys: the keys to take, as set.
    public Hotkey(HotkeyKeys keys, bool use)
    {
        Current = keys;
        this.use = use;
        CreateHandle(new CreateParams { Parent = MessageOnlyParent, Caption = "tray-anywhere hotkey" });
        Resume();
    }

    public event Action? Pressed;

    // The keys as set, taken or not.
    public HotkeyKeys Current { get; private set; }

    // True when the app wants the keys and another program already has them.
    public bool Taken => use && !registered;

    // Lets go of the keys, so that while the settings window waits for new keys, pressing
    // these reaches it too.
    public void Pause()
    {
        if (registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            registered = false;
        }
    }

    // Takes the keys again after Pause; if they were taken, tries once more.
    public void Resume()
    {
        if (use && !registered)
        {
            registered = RegisterHotKey(Handle, HotkeyId, (uint)Current.Modifiers | NoRepeat, (uint)Current.Key);
        }
    }

    // Changes to these keys if no other program has them. Else the old keys stay, and it
    // returns false. One id at a time: Windows would keep the old keys along with new ones
    // registered with the same id.
    public bool TryChange(HotkeyKeys keys)
    {
        var old = Current;
        Pause();
        Current = keys;
        Resume();
        if (Taken)
        {
            Current = old;
            Resume();
            return false;
        }
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            Pressed?.Invoke();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Pause();
        DestroyHandle();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}

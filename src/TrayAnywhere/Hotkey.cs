using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// A global hotkey: Windows sends WM_HOTKEY to this hidden window when the keys are
// pressed, whichever program has the focus.
internal sealed class Hotkey : NativeWindow, IDisposable
{
    // Modifier keys for Register, combined with |.
    public const uint Alt = 0x1;
    public const uint Control = 0x2;

    // Holding the keys down sends one press, not a stream of them.
    private const uint NoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;
    // The hotkey's number for this window; it has only one.
    private const int HotkeyId = 1;
    // A message-only window: never shown, only receives messages.
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private bool registered;

    public Hotkey() => CreateHandle(new CreateParams { Parent = MessageOnlyParent });

    public event Action? Pressed;

    // Returns false if another program already uses these keys.
    public bool Register(uint modifiers, Keys key)
    {
        registered = RegisterHotKey(Handle, HotkeyId, modifiers | NoRepeat, (uint)key);
        return registered;
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
        if (registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            registered = false;
        }
        DestroyHandle();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}

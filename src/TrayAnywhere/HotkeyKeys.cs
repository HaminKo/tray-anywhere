using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayAnywhere;

// The modifier keys of a hotkey, combined with |. The values are RegisterHotKey's.
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

// The keys that open and close the grid: modifiers and one other key. In settings.json by
// name, e.g. { "Modifiers": "Alt, Ctrl", "Key": "Space" }; an unknown name there makes it a
// broken file, as for the button colors.
internal sealed record HotkeyKeys(HotkeyModifiers Modifiers, Keys Key)
{
    // Alt and the key left of 1 (Oemtilde: ` and ~ on a US keyboard): one hand, two
    // neighbouring keys (owner's choice, docs/decisions.md).
    public static readonly HotkeyKeys Default = new(HotkeyModifiers.Alt, Keys.Oemtilde);

    // MapVirtualKey's "the key's character" (MAPVK_VK_TO_CHAR); the top bit marks a dead key.
    private const uint ToCharacter = 2;
    private const uint DeadKey = 0x8000;

    // The keys of a key press as WinForms gives it (the key and Ctrl, Alt, Shift), with the
    // Windows key, which WinForms leaves out, as Windows had it at this press. Null for a
    // modifier key alone: the key that comes with it is still to come.
    public static HotkeyKeys? FromKeyPress(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return null;
        }
        var modifiers = HotkeyModifiers.None;
        if (keyData.HasFlag(Keys.Control))
        {
            modifiers |= HotkeyModifiers.Ctrl;
        }
        if (keyData.HasFlag(Keys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }
        if (keyData.HasFlag(Keys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }
        // The top bit: the key is down.
        if (GetKeyState((int)Keys.LWin) < 0 || GetKeyState((int)Keys.RWin) < 0)
        {
            modifiers |= HotkeyModifiers.Win;
        }
        return new HotkeyKeys(modifiers, key);
    }

    // F1 to F24 on their own or with any modifier; any other key only with Ctrl, Alt or
    // Win, else typing it anywhere would open the grid (owner's choice). Shift alone isn't
    // enough: Shift+A is typing a capital A.
    public bool IsAllowed() =>
        Key is >= Keys.F1 and <= Keys.F24 ||
        (Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0;

    // As the keyboard shows the keys: "Alt+`", "Ctrl+Alt+Space".
    public string Name()
    {
        var name = "";
        foreach (var modifier in new[] { HotkeyModifiers.Win, HotkeyModifiers.Ctrl, HotkeyModifiers.Alt, HotkeyModifiers.Shift })
        {
            if (Modifiers.HasFlag(modifier))
            {
                name += modifier + "+";
            }
        }
        return name + KeyName(Key);
    }

    // A letter, digit or sign key by the character on it in the keyboard's layout ("`"
    // rather than Oemtilde). Other keys, and the number pad's (whose characters are the
    // top row's), by WinForms' name, which Windows' own key names (GetKeyNameText) don't
    // beat: they call the arrow keys "Num 4" and have none for F13 to F24 (measured,
    // docs/research/hotkey.md).
    private static string KeyName(Keys key)
    {
        var character = MapVirtualKey((uint)key, ToCharacter) & ~DeadKey;
        var numberPad = key is >= Keys.NumPad0 and <= Keys.Divide;
        if (character > ' ' && !numberPad)
        {
            return ((char)character).ToString();
        }
        // Keys has two names for each of these; either would be less plain.
        return key switch
        {
            Keys.Enter => "Enter",
            Keys.Back => "Backspace",
            Keys.PageUp => "Page Up",
            Keys.PageDown => "Page Down",
            _ => key.ToString(),
        };
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);
}

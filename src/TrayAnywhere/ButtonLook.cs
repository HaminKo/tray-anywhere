using System;
using System.Drawing;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace TrayAnywhere;

// The colors a floating button can have, picked in the settings window. The arrow on it
// is light in each, and dark only on a light accent color (ButtonColors.Arrow).
internal enum ButtonColor
{
    Dark,
    // Windows' accent color (Settings > Personalization > Colors).
    Accent,
    Blue,
    Green,
    Orange,
    Red,
    Purple,
}

// The sizes a floating button can have: each one's side at 100 % scaling, in pixels.
internal enum ButtonSize
{
    Small = 32,
    Medium = 40,
    Large = 48,
}

// One monitor's floating button as the settings window sets it: its color, its size, and
// whether it stays on top. A value the settings file doesn't have gets the default here,
// as the button was before these settings.
internal sealed record ButtonLook
{
    public ButtonColor Color { get; init; } = ButtonColor.Dark;
    public ButtonSize Size { get; init; } = ButtonSize.Small;
    // Off: a normal window, which any window over it hides. The hotkey still works there.
    public bool OnTop { get; init; } = true;
}

// What each button color looks like, for the button and for its square in the settings window.
internal static class ButtonColors
{
    // Blue, Green, Orange, Red and Purple from Windows 11's own accent colors, dark
    // enough for the light arrow; Dark as the grid.
    public static Color Background(ButtonColor color) => color switch
    {
        ButtonColor.Accent => AccentColor(),
        ButtonColor.Blue => Color.FromArgb(0, 120, 212),
        ButtonColor.Green => Color.FromArgb(16, 124, 16),
        ButtonColor.Orange => Color.FromArgb(202, 80, 16),
        ButtonColor.Red => Color.FromArgb(209, 52, 56),
        ButtonColor.Purple => Color.FromArgb(135, 100, 184),
        _ => Color.FromArgb(32, 32, 32),
    };

    // The background under the mouse: 15 % of the way to white, in each color.
    public static Color Hover(Color color)
    {
        static int Lighter(int value) => value + (int)Math.Round((255 - value) * 0.15);
        return Color.FromArgb(Lighter(color.R), Lighter(color.G), Lighter(color.B));
    }

    // The arrow on a background: light, unless it would stand out less than 3:1 there
    // (WCAG's least contrast for a control's graphics), as on a yellow or light green
    // accent; then dark, as the Dark button. Every fixed color passes, so only the accent
    // can get the dark arrow. By the button's own color, not the lighter one under the
    // mouse, so the arrow doesn't change when the mouse comes over it.
    public static Color Arrow(Color background)
    {
        var lightArrow = Color.FromArgb(220, 220, 220);
        var contrast = (Luminance(lightArrow) + 0.05) / (Luminance(background) + 0.05);
        return contrast >= 3 ? lightArrow : Background(ButtonColor.Dark);
    }

    // How bright a color looks, from 0 (black) to 1 (white), as WCAG defines it: each
    // channel turned into linear light first, so a pure green counts as much brighter
    // than a pure blue (decisions.md: a plain weighted sum of R, G, B can't tell a mint
    // accent from the fixed Purple).
    private static double Luminance(Color color)
    {
        static double Linear(int value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    // The name in the settings window (its tooltip, and for screen readers and the GUI test).
    public static string Name(ButtonColor color) => color == ButtonColor.Accent ? "Windows' accent color" : color.ToString();

    // Windows' accent color as Settings keeps it: AccentColor in DWM's key, with red in the
    // lowest byte (measured, To do 17 step D). Without it: Blue, Windows' default accent.
    private static Color AccentColor()
    {
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr)
            {
                return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
        }
        catch (Exception e) when (e is SecurityException or IOException)
        {
            // Not readable: the default below.
        }
        return Background(ButtonColor.Blue);
    }
}

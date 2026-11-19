using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GuiTest;

// A tray icon of the test's own, so the test can check that the grid shows a new icon,
// passes left and right clicks on to it and drops it when it goes, without clicking any
// of the user's real icons. Windows puts a new program's icon in the overflow.
internal sealed class TestIcon : IDisposable
{
    // Its tooltip; the tray button and the grid's tile get it as their name.
    public const string Name = "tray-anywhere GUI test";

    private readonly NotifyIcon icon = new() { Icon = SystemIcons.Application, Text = Name, Visible = true };

    public TestIcon() => icon.MouseClick += (_, e) =>
    {
        Clicks.Add(e.Button);
        // As Steam does on a right click: a menu, in the wrong place; a new one each time.
        if (e.Button == MouseButtons.Right && !MenuPlace.IsEmpty)
        {
            Menu?.Dispose();
            Menu = new SteamLikeMenu(MenuPlace);
        }
    };

    // The mouse buttons of the clicks it got, in order. WinForms adds them on this thread
    // while the test waits (Checks.WaitUntil lets its messages through).
    public List<MouseButtons> Clicks { get; } = [];

    // Where a right click opens a window like Steam's tray menu (SteamLikeMenu); empty:
    // none.
    public Rectangle MenuPlace { get; set; }

    // That window, from the last right click.
    public SteamLikeMenu? Menu { get; private set; }

    // Closes the window like Steam's menu, if one is open.
    public void CloseMenu()
    {
        Menu?.Dispose();
        Menu = null;
    }

    // A balloon from the icon, as old tray programs show one; Windows shows it as a
    // notification popup (a toast).
    public void ShowBalloon(string text) => icon.ShowBalloonTip(5000, Name, text, ToolTipIcon.None);

    // Takes the icon out of the tray, and closes its window like Steam's menu.
    public void Dispose()
    {
        Menu?.Dispose();
        icon.Dispose();
    }
}

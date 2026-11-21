using System;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace TrayAnywhere;

// The settings window, opened by "Settings..." in the right-click menu: the settings of
// the whole app. A change applies at once and is saved, so there is no OK or Cancel; its
// X or Escape closes it. "Floating button on this monitor" stays in the menu, where "this
// monitor" needs no name (Windows only names monitors like DISPLAY5). Plain WinForms
// controls, coloured dark like the grid.
internal sealed class SettingsForm : Form
{
    // Sizes at 100 % scaling, in pixels: the text as in the grid, and the space around it.
    private const int TextHeight = 12;
    private const int EdgeSpace = 12;
    // The narrowest the inside may be, so the title fits (at 150 % it was cut off with the
    // inside only as wide as Start with Windows; seen in the screenshots).
    private const int MinWidth = 220;

    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);

    private readonly Autostart autostart;
    // The monitor's free area; the window opens in its middle.
    private readonly Rectangle area;
    // The controls in rows: a name on the left, its control on the right, or one control
    // across both.
    private readonly TableLayoutPanel layout = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
    private readonly CheckBox startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    // The text's font at the window's scaling (ScaleTo); null until the window exists.
    private Font? textFont;
    // True while ShowSettings sets the ticks, so that isn't taken for a change by the user.
    private bool showing;

    public SettingsForm(Screen monitor, Autostart autostart)
    {
        Monitor = monitor;
        this.autostart = autostart;
        area = monitor.WorkingArea;

        Text = "tray-anywhere settings";
        // Its AutomationId in UI Automation, by which the GUI test finds it.
        Name = "Settings";
        // A title bar with only an X, and no icon: the app has none of its own.
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        // Like the grid: "Settings..." in the menu brings it back when it is behind others.
        ShowInTaskbar = false;
        // Sizes in this monitor's physical pixels (ScaleTo), as in the app's other windows.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Background;
        ForeColor = TextColor;

        // CheckedChanged, not Click: UI Automation's Toggle (screen readers, the GUI test)
        // turns the tick over without a Click (measured).
        startWithWindows.CheckedChanged += (_, _) =>
        {
            if (!showing)
            {
                SetStartWithWindows();
            }
        };
        layout.Controls.Add(startWithWindows, 0, 0);
        layout.SetColumnSpan(startWithWindows, 2);
        Controls.Add(layout);

        // Made in the middle of its monitor; OnLoad places it there with its real size.
        StartPosition = FormStartPosition.Manual;
        Location = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
    }

    // The monitor the window was opened on.
    public Screen Monitor { get; }

    // Shows the settings as they are now. Start with Windows is read from the registry,
    // not kept: Task Manager's Startup apps may have turned it off since.
    public void ShowSettings()
    {
        showing = true;
        startWithWindows.Checked = autostart.IsOn;
        showing = false;
    }

    // As in GridForm: on the 100 % monitor the first move changes the window's scaling
    // (docs/research/winforms-dpi.md), so move it, then size the text for the monitor,
    // then place it in the middle with its new size.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Location = Middle();
        ScaleTo(DeviceDpi);
        Location = Middle();
        ShowSettings();
    }

    // Coming back to the window, the user may have changed Start with Windows elsewhere.
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ShowSettings();
    }

    // The text and the space around it at this scaling (96: 100 %). The controls size
    // themselves to their text; the window is set to their size here, not with the form's
    // AutoSize, which changed it only after OnLoad had placed it (9 px off the middle,
    // measured once at 150 %).
    private void ScaleTo(int dpi)
    {
        var old = textFont;
        textFont = new Font("Segoe UI", TextHeight * dpi / 96f, GraphicsUnit.Pixel);
        // The controls take the window's font.
        Font = textFont;
        old?.Dispose();
        var space = (int)Math.Round(EdgeSpace * dpi / 96f);
        layout.Location = new Point(space, space);
        var content = layout.PreferredSize;
        var minWidth = (int)Math.Round(MinWidth * dpi / 96f);
        ClientSize = new Size(Math.Max(content.Width + 2 * space, minWidth), content.Height + 2 * space);
    }

    // The window's top-left corner when it is in the middle of its monitor's free area.
    private Point Middle() => TopWindow.KeepInside(area,
        new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2), Size);

    private void SetStartWithWindows()
    {
        try
        {
            autostart.Set(startWithWindows.Checked);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException)
        {
            // Shown once this handler is done: a message box waits in a loop of its own, in
            // which "Settings..." on another monitor could close this window under the handler.
            BeginInvoke(() => MessageBox.Show(this, $"tray-anywhere couldn't change Start with Windows:\n{e.Message}", "tray-anywhere"));
        }
        // The tick as the registry has it now, also when the change failed.
        ShowSettings();
    }

    // Kept on its monitor while dragged, like the grid: it is laid out for that monitor's
    // scaling only. "Settings..." on another monitor opens it there.
    protected override void WndProc(ref Message m)
    {
        if (!TopWindow.KeepOnMonitor(ref m, area))
        {
            base.WndProc(ref m);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Use(Handle);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            textFont?.Dispose();
        }
    }
}

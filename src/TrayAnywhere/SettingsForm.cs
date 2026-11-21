using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace TrayAnywhere;

// The settings window, opened by "Settings..." in the right-click menu: the monitors drawn
// as an outline, as in Windows' Settings > System > Display (names: MonitorNames), and
// the settings of the picked monitor's floating button; then Start with Windows for the
// whole app. A change applies at once and is saved, so there is no OK or Cancel; its X or
// Escape closes it. Plain WinForms controls, coloured dark like the grid.
internal sealed class SettingsForm : Form
{
    // Sizes at 100 % scaling, in pixels: the text as in the grid, and the space around it.
    private const int TextHeight = 12;
    private const int EdgeSpace = 12;
    // The narrowest the inside may be, so the title fits (at 150 % it was cut off with the
    // inside only as wide as Start with Windows; seen in the screenshots).
    private const int MinWidth = 220;
    // The outline of the monitors at most this wide and this high (monitors stacked one
    // above the other would make it tall), and the space each box keeps inside its
    // monitor's place, so neighbours don't touch.
    private const int OutlineWidth = 240;
    private const int OutlineMaxHeight = 180;
    private const int BoxInset = 2;

    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);
    // The monitors' boxes grey, the picked one blue (Windows' default accent color), each a
    // little lighter under the mouse.
    private static readonly Color BoxColor = Color.FromArgb(60, 60, 60);
    private static readonly Color BoxHover = Color.FromArgb(80, 80, 80);
    private static readonly Color PickedColor = Color.FromArgb(0, 120, 215);
    private static readonly Color PickedHover = Color.FromArgb(30, 140, 230);

    private readonly Autostart autostart;
    // Whether a monitor has a floating button, and what switching it does (App).
    private readonly Func<Screen, bool> buttonOn;
    private readonly Action<Screen, bool> setButtonOn;
    // The monitor's free area; the window opens in its middle.
    private readonly Rectangle area;
    // The controls in rows: a name on the left, its control on the right, or one control
    // across both.
    private readonly TableLayoutPanel layout = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
    private readonly CheckBox startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly Label buttonHeading = new() { Text = "Floating button settings for:", AutoSize = true };
    // The outline: one box per monitor at its place on the desktop. Each box is a radio
    // button that looks like a button, so they still are one group (arrow keys, screen
    // readers, the GUI test). In a panel of their own: WinForms makes the radio buttons of
    // one parent a group, so no later control can join theirs.
    private readonly Panel outline = new();
    // Each box with its monitor, for placing them at the window's scaling (ScaleTo).
    private readonly List<(RadioButton Box, Screen Monitor)> boxes = new();
    // The picked monitor's full name, under the outline. Its Name is its AutomationId, by
    // which the GUI test finds it.
    private readonly Label pickedName = new() { Name = "PickedMonitor" };
    private readonly CheckBox showButton = new() { Text = "Show the floating button", AutoSize = true };
    // The monitor picked in the outline: the settings below it are for its button.
    private Screen picked;
    // The text's font at the window's scaling (ScaleTo); null until the window exists.
    private Font? textFont;
    // True while ShowSettings sets the ticks, so that isn't taken for a change by the user.
    private bool showing;

    // monitor: the one the window opens on; the outline starts with it picked.
    public SettingsForm(Screen monitor, Autostart autostart, Func<Screen, bool> buttonOn, Action<Screen, bool> setButtonOn)
    {
        Monitor = monitor;
        this.autostart = autostart;
        this.buttonOn = buttonOn;
        this.setButtonOn = setButtonOn;
        area = monitor.WorkingArea;
        picked = monitor;

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
        foreach (var named in MonitorNames.InOrder(monitor))
        {
            var box = MakeBox(named, named.Monitor.DeviceName == monitor.DeviceName);
            if (box.Checked)
            {
                pickedName.Text = named.Name;
            }
            // Raised for the box losing its dot too; only the one getting it acts.
            box.CheckedChanged += (_, _) =>
            {
                SetHoverColor(box);
                if (box.Checked)
                {
                    picked = named.Monitor;
                    pickedName.Text = named.Name;
                    ShowSettings();
                }
            };
            outline.Controls.Add(box);
            boxes.Add((box, named.Monitor));
        }
        showButton.CheckedChanged += (_, _) =>
        {
            if (!showing)
            {
                setButtonOn(picked, showButton.Checked);
                ShowSettings();
            }
        };
        AddRow(buttonHeading);
        AddRow(outline);
        AddRow(pickedName);
        AddRow(showButton);
        AddRow(startWithWindows);
        Controls.Add(layout);

        // Made in the middle of its monitor; OnLoad places it there with its real size.
        StartPosition = FormStartPosition.Manual;
        Location = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
    }

    // The monitor the window was opened on.
    public Screen Monitor { get; }

    // Shows the settings as they are now, for the picked monitor. Start with Windows is
    // read from the registry, not kept: Task Manager's Startup apps may have turned it off
    // since.
    public void ShowSettings()
    {
        showing = true;
        startWithWindows.Checked = autostart.IsOn;
        showButton.Checked = buttonOn(picked);
        showing = false;
    }

    // A control in a row of its own, across both columns.
    private void AddRow(Control control)
    {
        layout.Controls.Add(control, 0, layout.RowCount);
        layout.SetColumnSpan(control, 2);
        layout.RowCount++;
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
        // A gap above Start with Windows, apart from the button's settings. The other
        // controls keep WinForms' 3 px around them.
        startWithWindows.Margin = new Padding(3, space, 3, 3);
        PlaceBoxes(dpi);
        SizePickedName();
        var content = layout.PreferredSize;
        var minWidth = (int)Math.Round(MinWidth * dpi / 96f);
        ClientSize = new Size(Math.Max(content.Width + 2 * space, minWidth), content.Height + 2 * space);
    }

    // A monitor's box in the outline: its short name, flat, grey, blue when picked.
    private static RadioButton MakeBox(NamedMonitor named, bool isPicked)
    {
        var box = new RadioButton
        {
            Text = named.ShortName,
            // The full name for screen readers and the GUI test.
            AccessibleName = named.Name,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = BoxColor,
            Checked = isPicked,
        };
        box.FlatAppearance.BorderSize = 0;
        box.FlatAppearance.CheckedBackColor = PickedColor;
        SetHoverColor(box);
        return box;
    }

    // WinForms paints a flat box under the mouse in MouseOverBackColor whether it is picked
    // or not, so that color changes with the pick.
    private static void SetHoverColor(RadioButton box)
    {
        var hover = box.Checked ? PickedHover : BoxHover;
        box.FlatAppearance.MouseOverBackColor = hover;
        box.FlatAppearance.MouseDownBackColor = hover;
    }

    // Each monitor's box at its place on the desktop, all at one scale, so the outline is
    // at most OutlineWidth wide and OutlineMaxHeight high. Screen.Bounds are in physical
    // pixels, which gives the same picture as Windows' Display settings (measured on a
    // screenshot of it, To do 17).
    private void PlaceBoxes(int dpi)
    {
        var desktop = boxes[0].Monitor.Bounds;
        foreach (var (_, monitor) in boxes)
        {
            desktop = Rectangle.Union(desktop, monitor.Bounds);
        }
        var scale = Math.Min(OutlineWidth * dpi / 96.0 / desktop.Width, OutlineMaxHeight * dpi / 96.0 / desktop.Height);
        var inset = (int)Math.Round(BoxInset * dpi / 96f);
        // Each edge is scaled on its own, so two monitors that touch get boxes with the
        // same gap between them, whatever the rounding.
        int Scaled(int distance) => (int)Math.Round(distance * scale);
        foreach (var (box, monitor) in boxes)
        {
            var place = monitor.Bounds;
            box.Bounds = Rectangle.FromLTRB(
                Scaled(place.Left - desktop.Left) + inset, Scaled(place.Top - desktop.Top) + inset,
                Scaled(place.Right - desktop.Left) - inset, Scaled(place.Bottom - desktop.Top) - inset);
        }
        outline.Size = new Size(Scaled(desktop.Width), Scaled(desktop.Height));
    }

    // The name under the outline as wide as the longest name, as the label itself measures
    // them, so each pick fits without the window changing its size.
    private void SizePickedName()
    {
        var shown = pickedName.Text;
        var size = Size.Empty;
        foreach (var (box, _) in boxes)
        {
            pickedName.Text = box.AccessibleName;
            var needed = pickedName.PreferredSize;
            size = new Size(Math.Max(size.Width, needed.Width), Math.Max(size.Height, needed.Height));
        }
        pickedName.Text = shown;
        pickedName.Size = size;
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

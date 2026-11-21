using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace TrayAnywhere;

// The settings window, opened by "Settings..." in the right-click menu: the monitors drawn
// as an outline, as in Windows' Settings > System > Display (names: MonitorNames), and
// the settings of the picked monitor's floating button; then Start with Windows and the
// hotkey for the whole app. A change applies at once and is saved, so there is no OK or
// Cancel; its X or Escape closes it. Plain WinForms controls, coloured dark like the grid.
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
    // The color squares: their side, their frame, and the space between them; and the
    // space between the size choices.
    private const int SquareSize = 20;
    private const int SquareFrame = 2;
    private const int SquareGap = 6;
    private const int ChoiceGap = 12;
    // The space left and right of the text in the hotkey's buttons.
    private const int ButtonPadding = 6;
    // WM_SYSCOMMAND's "the menu, from the keyboard" (WndProc); its low 4 bits are Windows' own.
    private const int WmSysCommand = 0x0112;
    private const int ScKeyMenu = 0xF100;

    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);
    // The monitors' boxes grey, the picked one blue (Windows' default accent color), each a
    // little lighter under the mouse.
    private static readonly Color BoxColor = Color.FromArgb(60, 60, 60);
    private static readonly Color BoxHover = Color.FromArgb(80, 80, 80);
    private static readonly Color PickedColor = Color.FromArgb(0, 120, 215);
    private static readonly Color PickedHover = Color.FromArgb(30, 140, 230);
    // The names of the color and size rows while the button is off.
    private static readonly Color GreyedText = Color.FromArgb(120, 120, 120);

    // The line under the hotkey: how its box works; while it waits for new keys; after a
    // key that isn't allowed; after new keys another program has; and while the keys as
    // set are another program's (from the start). Never empty: UI Automation would then
    // give the line the name of the one before it, "Hotkey:" (measured).
    private const string IdleNote = "Click the keys to change them.";
    private const string WaitingNote = "Press the new keys, or Escape to keep these.";
    private const string NotAllowedNote = "Use Ctrl, Alt or Win with that key.";
    private const string RefusedNote = "Another program uses those keys; these stay.";
    private const string TakenNote = "Another program uses these keys: pick others.";

    private readonly Autostart autostart;
    // Whether a monitor has a floating button, and what switching it does (App).
    private readonly Func<Screen, bool> buttonOn;
    private readonly Action<Screen, bool> setButtonOn;
    // A monitor's button's color, size and always on top, and what a change does (App).
    private readonly Func<Screen, ButtonLook> lookOf;
    private readonly Action<Screen, ButtonLook> setLook;
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
    // one parent a group, so no later control can join theirs. Its Name is its
    // AutomationId, by which the GUI test finds the boxes.
    private readonly Panel outline = new() { Name = "Monitors" };
    // Each box with its monitor, for placing them at the window's scaling (ScaleTo).
    private readonly List<(RadioButton Box, Screen Monitor)> boxes = new();
    // The picked monitor's full name, under the outline. Its Name is its AutomationId, by
    // which the GUI test finds it.
    private readonly Label pickedName = new() { Name = "PickedMonitor" };
    private readonly CheckBox showButton = new() { Text = "Show the floating button", AutoSize = true };
    // The picked monitor's button: its color as a row of squares in each color (radio
    // buttons that look like buttons, as the boxes), its size, and always on top. Each row
    // of radio buttons in a panel of its own, so it is a group of its own.
    private readonly Label colorName = new() { Text = "Color:", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly FlowLayoutPanel colorRow = new() { AutoSize = true, WrapContents = false };
    private readonly List<(RadioButton Square, ButtonColor Color)> squares = new();
    private readonly Label sizeName = new() { Text = "Size:", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly FlowLayoutPanel sizeRow = new() { AutoSize = true, WrapContents = false };
    private readonly List<(RadioButton Choice, ButtonSize Size)> sizes = new();
    private readonly CheckBox onTop = new() { Text = "Always on top", AutoSize = true };
    // The app's hotkey, and what new keys do (App: true if they are taken, false if another
    // program has them and the old ones stay).
    private readonly Hotkey hotkey;
    private readonly Func<HotkeyKeys, bool> setHotkey;
    // The hotkey's row: a box with the keys, which a click makes wait for new ones, and
    // Default; under it a line on how it went. The box's and the line's Names are their
    // AutomationIds, by which the GUI test finds them.
    private readonly Label hotkeyName = new() { Text = "Hotkey:", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly FlowLayoutPanel hotkeyRow = new() { AutoSize = true, WrapContents = false };
    private readonly Button hotkeyBox = new() { Name = "Hotkey", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button defaultHotkey = new() { Text = "Default", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label hotkeyNote = new() { Name = "HotkeyNote", Text = IdleNote };
    // The colors' names, on the squares.
    private readonly ToolTip toolTip = new();
    // The monitor picked in the outline: the settings below it are for its button.
    private Screen picked;
    // The text's font at the window's scaling (ScaleTo); null until the window exists.
    private Font? textFont;
    // True while ShowSettings sets the ticks, so that isn't taken for a change by the user.
    private bool showing;
    // True while the hotkey box waits for new keys: every key press goes to it then.
    private bool waiting;

    // monitor: the one the window opens on; the outline starts with it picked.
    public SettingsForm(Screen monitor, Autostart autostart, Func<Screen, bool> buttonOn, Action<Screen, bool> setButtonOn,
        Func<Screen, ButtonLook> lookOf, Action<Screen, ButtonLook> setLook, Hotkey hotkey, Func<HotkeyKeys, bool> setHotkey)
    {
        Monitor = monitor;
        this.autostart = autostart;
        this.buttonOn = buttonOn;
        this.setButtonOn = setButtonOn;
        this.lookOf = lookOf;
        this.setLook = setLook;
        this.hotkey = hotkey;
        this.setHotkey = setHotkey;
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
        // Each change of the look is saved with the picked monitor's other two settings.
        foreach (var color in Enum.GetValues<ButtonColor>())
        {
            var square = MakeSquare(color);
            // As for the boxes: only the square getting the dot acts.
            square.CheckedChanged += (_, _) =>
            {
                SetFrame(square);
                if (square.Checked && !showing)
                {
                    setLook(picked, lookOf(picked) with { Color = color });
                }
            };
            colorRow.Controls.Add(square);
            squares.Add((square, color));
        }
        foreach (var size in Enum.GetValues<ButtonSize>())
        {
            var choice = new RadioButton { Text = size.ToString(), AutoSize = true };
            choice.CheckedChanged += (_, _) =>
            {
                if (choice.Checked && !showing)
                {
                    setLook(picked, lookOf(picked) with { Size = size });
                }
            };
            sizeRow.Controls.Add(choice);
            sizes.Add((choice, size));
        }
        onTop.CheckedChanged += (_, _) =>
        {
            if (!showing)
            {
                setLook(picked, lookOf(picked) with { OnTop = onTop.Checked });
            }
        };
        AddRow(buttonHeading);
        AddRow(outline);
        AddRow(pickedName);
        AddRow(showButton);
        AddRow(colorName, colorRow);
        AddRow(sizeName, sizeRow);
        // A second click while it waits changes nothing; Escape ends the wait.
        hotkeyBox.Click += (_, _) =>
        {
            if (!waiting)
            {
                StartWaiting();
            }
        };
        defaultHotkey.Click += (_, _) => EndWaiting(setHotkey(HotkeyKeys.Default) ? IdleNote : RefusedNote);
        // The keys now, so ScaleTo makes room for them.
        hotkeyBox.Text = hotkey.Current.Name();
        MakeFlat(hotkeyBox, picked: false);
        MakeFlat(defaultHotkey, picked: false);
        hotkeyRow.Controls.Add(hotkeyBox);
        hotkeyRow.Controls.Add(defaultHotkey);
        AddRow(onTop);
        AddRow(startWithWindows);
        AddRow(hotkeyName, hotkeyRow);
        AddRow(hotkeyNote);
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
        var look = lookOf(picked);
        foreach (var (square, color) in squares)
        {
            square.Checked = color == look.Color;
        }
        foreach (var (choice, size) in sizes)
        {
            choice.Checked = size == look.Size;
        }
        onTop.Checked = look.OnTop;
        // Greyed out while the button is off; kept for when it is on again. The rows' names
        // stay enabled, in a grey picked for the dark window.
        colorRow.Enabled = sizeRow.Enabled = onTop.Enabled = showButton.Checked;
        colorName.ForeColor = sizeName.ForeColor = showButton.Checked ? TextColor : GreyedText;
        var keys = hotkey.Current.Name();
        if (hotkeyBox.Text != keys)
        {
            // Longer keys may need a wider window, still on its monitor.
            hotkeyBox.Text = keys;
            FitWindow(DeviceDpi);
            Location = TopWindow.KeepInside(area, Location, Size);
        }
        defaultHotkey.Enabled = hotkey.Current != HotkeyKeys.Default;
        if (hotkey.Taken && !waiting)
        {
            hotkeyNote.Text = TakenNote;
        }
        showing = false;
    }

    // A click on the hotkey box: from now on each key press goes to it (ProcessCmdKey).
    private void StartWaiting()
    {
        waiting = true;
        // Let go of the keys, so pressing them reaches this window too.
        hotkey.Pause();
        MakeFlat(hotkeyBox, picked: true);
        hotkeyNote.Text = WaitingNote;
    }

    // A key pressed while the hotkey box waits. Escape keeps the keys as they were; a
    // modifier key alone waits for the key that comes with it; a key that isn't allowed
    // says so and waits on.
    private void TakeKeys(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            EndWaiting(IdleNote);
            return;
        }
        var keys = HotkeyKeys.FromKeyPress(keyData);
        if (keys == null)
        {
            return;
        }
        if (!keys.IsAllowed())
        {
            hotkeyNote.Text = NotAllowedNote;
            return;
        }
        EndWaiting(setHotkey(keys) ? IdleNote : RefusedNote);
    }

    // The end of a wait for new keys, and after Default (which ends a wait too): the keys
    // are the app's again (new ones setHotkey took, or else the old ones), and the line
    // under them says how it went.
    private void EndWaiting(string note)
    {
        waiting = false;
        hotkey.Resume();
        MakeFlat(hotkeyBox, picked: false);
        hotkeyNote.Text = note;
        ShowSettings();
    }

    // A control in a row of its own, across both columns.
    private void AddRow(Control control)
    {
        layout.Controls.Add(control, 0, layout.RowCount);
        layout.SetColumnSpan(control, 2);
        layout.RowCount++;
    }

    // A name on the left and its control on the right.
    private void AddRow(Label name, Control control)
    {
        layout.Controls.Add(name, 0, layout.RowCount);
        layout.Controls.Add(control, 1, layout.RowCount);
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

    // Keys pressed in another window don't come here, so a wait for new keys ends when
    // the window is left, and the old keys are the app's again.
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (waiting)
        {
            EndWaiting(IdleNote);
        }
    }

    // Closed while the hotkey box waits (by its X, or by "Settings..." on another monitor):
    // the old keys are the app's again.
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (waiting)
        {
            hotkey.Resume();
        }
        base.OnFormClosed(e);
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
        int Px(int size) => (int)Math.Round(size * dpi / 96f);
        foreach (var (square, _) in squares)
        {
            square.Size = new Size(Px(SquareSize), Px(SquareSize));
            square.Margin = new Padding(0, 0, Px(SquareGap), 0);
            square.FlatAppearance.BorderSize = Px(SquareFrame);
        }
        foreach (var (choice, _) in sizes)
        {
            choice.Margin = new Padding(0, 0, Px(ChoiceGap), 0);
        }
        // Room around the text of the hotkey's flat buttons, which have no frame.
        hotkeyBox.Padding = defaultHotkey.Padding = new Padding(Px(ButtonPadding), 0, Px(ButtonPadding), 0);
        hotkeyBox.Margin = new Padding(0, 0, Px(ChoiceGap), 0);
        defaultHotkey.Margin = Padding.Empty;
        PlaceBoxes(dpi);
        SizeForLongest(pickedName, boxes.ConvertAll(b => b.Box.AccessibleName ?? ""));
        SizeForLongest(hotkeyNote, [IdleNote, WaitingNote, NotAllowedNote, RefusedNote, TakenNote]);
        FitWindow(dpi);
    }

    // The window as big as its controls, with the space around them, and never narrower
    // than MinWidth.
    private void FitWindow(int dpi)
    {
        var space = (int)Math.Round(EdgeSpace * dpi / 96f);
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

    // A color's square: flat, in the button's color, lighter under the mouse as the button
    // is; its name in a tooltip.
    private RadioButton MakeSquare(ButtonColor color)
    {
        var background = ButtonColors.Background(color);
        var hover = ButtonColors.Hover(background);
        var square = new RadioButton
        {
            AccessibleName = ButtonColors.Name(color),
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = background,
        };
        square.FlatAppearance.CheckedBackColor = background;
        square.FlatAppearance.MouseOverBackColor = hover;
        square.FlatAppearance.MouseDownBackColor = hover;
        toolTip.SetToolTip(square, ButtonColors.Name(color));
        SetFrame(square);
        return square;
    }

    // The picked color's square framed light, the others grey, so the dark one shows on
    // the dark window too.
    private static void SetFrame(RadioButton square) =>
        square.FlatAppearance.BorderColor = square.Checked ? TextColor : BoxColor;

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

    // A label as big as the longest of the texts it shows (the monitors' names under the
    // outline, the hotkey's notes), as the label itself measures them, so each fits
    // without the window changing its size.
    private static void SizeForLongest(Label label, List<string> texts)
    {
        var shown = label.Text;
        var size = Size.Empty;
        foreach (var text in texts)
        {
            label.Text = text;
            var needed = label.PreferredSize;
            size = new Size(Math.Max(size.Width, needed.Width), Math.Max(size.Height, needed.Height));
        }
        label.Text = shown;
        label.Size = size;
    }

    // A button flat like the monitors' boxes: grey, or blue while picked (the hotkey box
    // while it waits), each a little lighter under the mouse.
    private static void MakeFlat(Button button, bool picked)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = picked ? PickedColor : BoxColor;
        var hover = picked ? PickedHover : BoxHover;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = hover;
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
    // Also: no menu from the keyboard. Letting go of Alt or F10 alone asks for the window's
    // menu (SC_KEYMENU with no key), and Windows' menu would then get the next keys, so a
    // hotkey box waiting for them wouldn't (code review). The window has no menu bar, and
    // Alt+Space still opens its system menu (it comes with Space as its key).
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmSysCommand && (m.WParam.ToInt64() & 0xFFF0) == ScKeyMenu && m.LParam == IntPtr.Zero)
        {
            return;
        }
        if (!TopWindow.KeepOnMonitor(ref m, area))
        {
            base.WndProc(ref m);
        }
    }

    // Every key press comes here first, before the control with the focus gets it: with
    // Alt too (Windows' "system keys"), so Alt+` reaches the hotkey box.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (waiting)
        {
            TakeKeys(keyData);
            return true;
        }
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
            toolTip.Dispose();
        }
    }
}

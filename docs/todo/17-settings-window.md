# To do 17: A settings window (medium)

## Problem / Goal
"Move later items up": the settings window, until now under "Later, only if the user asks" with the note "once there are about four settings (To do 16 found one)".

Today the settings are all in the right-click menu of the floating buttons and the grid: Start with Windows, Floating button on this monitor (per secondary monitor), Reset icon order (an action, not a setting). settings.json also keeps where the buttons and grids were dragged and the icon order, set by dragging. Everything else is fixed in code: the hotkey (Alt+`), the refresh times, the icon size.

The 2026-11-19 decision (To do 16, the owner) says a window pays off only with about four settings, or one that must be typed (such as a hotkey). So the build session starts with the list of settings it would hold.

## Read first
- docs/decisions.md 2026-11-19 "No settings window: one menu item turns the floating button off per monitor" (what was rejected: the hotkey, refresh times and icon size as settings), and 2026-11-14 "The grid app: Alt+`, dark, ..." (the hotkey stays fixed until it clashes).
- docs/archive/16-settings-window.md: the same question asked last time, and its answers.
- src/TrayAnywhere/AppMenu.cs (the menu items), Settings.cs (settings.json: `ButtonsOff` and the rest), Autostart.cs, Hotkey.cs, App.cs (`InUseRefresh`, `UnusedRefresh`), GridForm.cs (tile size).

## Approach
- First the list of settings. A new one is a value in settings.json, read where the code has a constant now.
- A plain WinForms dialog, dark like the grid, opened from a "Settings..." item in the right-click menu; a change applies at once and is saved to settings.json. The menu items it replaces leave the menu, so each setting lives in one place.
- If the list stays at two or three on/off settings, the 2026-11-19 decision stands: menu items, and this to-do goes back to Later.

## Ask the user
- Which settings would the window hold, beyond the two in the menu now? Suggested: none new until one is missed; if one is, the hotkey is the likely first (the only one that must be typed).
- Build it now with the two existing settings? Suggested: no; wait until there is a third setting or one that must be typed, as decided on 2026-11-19.
- Keep the menu items as well as the window? Suggested: no, one place per setting; keep Exit and Reset icon order in the menu.

## Test
- Claude: a GUI test step: open the window from the menu (UI Automation), change each setting, check the tested app's own settings file and the effect.
- User: each setting by hand, and again after a restart.

## Answers (owner, 2026-11-20)
- Build the settings window (after Claude's advice against it with two settings: monitor names, scaling of standard controls, two clicks instead of one).
- New settings: "color/highlight to make it stand out more, whether to have it always on top or not, and size". With Start with Windows and Floating button on this monitor that makes five, past the "about four" of the 2026-11-19 decision.
- The rest "go with your suggestions" (2026-11-20):
  - "It" is the floating buttons, one look for all of them. The grid stays dark (the taskbar icons' pictures have the taskbar's dark background) and its icons keep their size (the pictures are copied at the tray's own size; bigger would only stretch them).
  - Color: a list: Dark (today), Windows' accent color (Settings > Personalization > Colors), Blue, Green, Orange, Red, Purple. The arrow stays light; lighter under the mouse as now. Not Windows' color picker (light, and hard for the GUI test).
  - Size: Small (32 px at 100 %, today), Medium (40 px), Large (48 px); the arrow grows with it; a dragged place stays, pushed inside the screen if needed.
  - Always on top: one switch for all buttons, on by default. Off: the button is a normal window, hidden by any window over it (a maximized one too); Alt+` still works there. Hiding the button only while a full-screen program is in front is To do 18 (Someday).
  - Start with Windows moves from the menu into the window. "Floating button on this monitor" stays in the menu (it needs no monitor name there; Windows only gives "DISPLAY5"). The menu: Floating button on this monitor (secondary monitors), Settings..., Reset icon order, Exit.
  - The window: "Settings..." in the menu of a button or the grid opens it in the middle of that monitor; dark like the grid; a change applies at once and is saved (no OK or Cancel); closes with its X or Escape; one at a time ("Settings..." again brings it to the front); not always on top.
- Owner, 2026-11-21, after step A: the settings are per monitor's floating button, not one look for all; the window lists the buttons and the user picks which one the settings below are for, in the window itself (rejected: the window showing only the monitor it is open on). The rest "go with your suggestions":
  - "Floating button on this monitor" (on/off) moves from the right-click menu into the window, beside that monitor's color, size and always on top. Turning a button back on: the popup's menu there, Settings..., tick.
  - Owner, 2026-11-21: a floating button on the main monitor too, as a setting, off by default. In the list as "Main monitor", first; its switch starts unticked. Kept as "the main monitor" (a setting `MainButtonOn`), not by name, so it follows whichever monitor Windows makes the main one.
  - The list starts at the monitor the window opened on.
  - Names (Claude's choice, no new Windows calls): "Main monitor" first, then the external monitors from left to right by their left edge (then top): one is "External monitor", two "Left monitor" and "Right monitor", three or more "Monitor 1 from the left" and so on; each with its size in pixels and "this one" for the window's monitor, e.g. "Right monitor, 3840 × 2160 (this one)". Rejected: DISPLAY5 (means nothing), Windows' model names such as "DELL U2720Q" (about 80 lines of display-config calls, estimated).
  - The list as radio buttons, one per monitor, so all show at once (the GUI test picks one with UI Automation's SelectionItem; act on CheckedChanged, as for Start with Windows).

## Progress
- Step A, done (tested by the owner 2026-11-21): "Settings..." in the right-click menu opens SettingsForm (dark, in the middle of that monitor, one at a time; again there it comes to the front, from another monitor it moves there; X or Escape closes it; closed at a monitor change). It holds Start with Windows, moved from the menu. Changed from the plan after measuring: the window stays on its monitor while dragged (moved to another scaling, WinForms resized its text but not the window; docs/research/winforms-dpi.md); it acts on CheckedChanged (UI Automation's Toggle raises no Click); the GUI test starts a copy of the app for each of its three Start with Windows clicks (no second menu once the window has shown; docs/research/gui-testing.md). Rows go in the window's TableLayoutPanel (two columns: a name, its control); its size is set in ScaleTo from the table's PreferredSize, at least 220 px wide at 100 %.
- Left from the review: the window can open under a grid dragged over the monitor's middle (the grid is always on top); not seen; a fix needs a choice (window on top, or the grid closes).
- Step B, done (tested by the owner 2026-11-21): under Start with Windows, "Floating button settings for:", one radio button per monitor (MonitorNames.cs; in their own FlowLayoutPanel so later controls can't join their group) and "Show the floating button" for the picked one (Claude's wording: in the window "this monitor" could mean the one marked "(this one)"). Settings.cs `MainButtonOn` (off by default); `ButtonOn`/`SetButtonOn` take any monitor; App.MakeButtons asks for the main one too; the window gets the same pair the menu had (`settings.ButtonOn`, App.SetButtonOn). The menu: Settings..., Reset icon order, Exit. "Settings..." again on the same monitor keeps the pick. A grid open on a switched monitor reopens and takes the focus (left so). GUI test ButtonsInSettings: three app copies (from M's button, a restart, from the main monitor's button), radio buttons picked with UI Automation's SelectionItem (docs/research/gui-testing.md).
- Step C (owner, 2026-11-21, after step B's test): the monitors drawn as an outline like Windows' Settings > System > Display instead of the list, and at the top of the window. Measured on the owner's screenshot of Display settings: the boxes are the monitors' `Screen.Bounds` (physical pixels) at one scale (about 10.2 px per box px for all three) and the same offsets, so `Screen.Bounds` reproduces that picture. Owner: "go with your suggestions" (2026-11-21):
  - The outline replaces the radio list (the FlowLayoutPanel): a panel with one RadioButton per monitor, `Appearance = Button`, flat, placed at its `Screen.Bounds` scaled by one factor so the whole outline is about 240 px wide at 100 % (scaled like the rest in ScaleTo), each box inset ~2 px so neighbours don't touch. Dark grey boxes, the picked one blue (0, 120, 215, Windows' default), light text. Still radio buttons: one group, arrow keys, UI Automation's SelectionItem (measure that `Appearance = Button` keeps it; else a posted click).
  - In the boxes: "Main", "Left", "Right" ("External" with one external monitor): MonitorNames' place without " monitor"; with 3 or more, the number from the left (not Windows' numbers, which need the rejected display-config calls; not this PC's case). Each box's `AccessibleName` is the full name, so screen readers and the GUI test keep "Right monitor, 3840 × 2160 (this one)".
  - Under the outline, a label with the picked monitor's full name, changed at each pick.
  - Order: "Floating button settings for:", the outline, the name label, "Show the floating button" (step D adds its settings here); a gap; Start with Windows at the bottom (for the whole app).
  - GUI test: CheckMonitorList keeps the full names (now as a set, since the boxes aren't top to bottom) and checks each box's place: same left-to-right and top-to-bottom order as the monitors, sizes in the monitors' ratio within a few pixels; the label shows the picked name; picking as in step B. Screenshots (the look changes). About 120 lines (estimate).
- Step D (was step C): color, size and always on top for the picked monitor's button. Settings.cs keeps them per monitor name, like ButtonPlaces (a missing one: the defaults); FloatingButton.cs reads them; App.cs makes that monitor's button again at a change, as SetButtonOn does. The GUI test changes each and checks settings.json, the button and a restart (each in its own app copy, as in step A). About 200 lines (estimate).

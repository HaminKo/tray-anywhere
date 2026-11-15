# To do 10: Run the GUI test while the everyday copy is running (medium)

## Problem / Goal
"Let the GUI test run while your everyday tray-anywhere is running, so you don't have to Exit it before each test" (owner's choice, 2026-11-14, after To do 5). Since To do 5 the owner's copy starts with Windows and runs all day, and `dotnet run --project src/GuiTest` stops at once ("tray-anywhere is running"), so every build step needs the owner to right-click a button: Exit, and start the app again afterwards.

What blocks a second copy today: the one-copy mutex `Local\tray-anywhere` (the app's Program.cs, and the test's own check in its Main); the hotkey Alt+`, which a second copy can't register, so it quits with a message (App.HotkeyRegistered). The test already finds the app's windows by process id and posts messages to their handles, so two copies' windows don't mix up there.

## Read first
- docs/decisions.md: "The GUI test: a console program..." and "Start with Windows: ...".
- docs/research/gui-testing.md: the foreground and menu notes (a second copy changes who has the front).
- src/TrayAnywhere/Program.cs (the mutex, the arguments), App.cs (the hotkey); src/GuiTest/Program.cs Main, TestedApp.cs (PressHotkey posts WM_HOTKEY to the app's hidden window).

## Approach
- A test-only option (like `--settings` and `--run-key`) under which the app uses another mutex name and doesn't need the hotkey registered: the test posts WM_HOTKEY to the hidden window itself.
- The test then checks for the test copy's mutex only, never stops or touches the owner's copy.
- Screenshots copy the screen: the owner's button sits in the same corner as the test copy's on a monitor without a saved place. The test's settings file could give the test copy's buttons other places, or screenshots keep needing a closed copy.

## Ask the user
- Screenshots too, or only the test? Suggested: only the test; screenshots are taken for look changes only, and then a closed copy is no burden.
- While the test runs, the owner's grid shows the test's tray icon for a few seconds, and both copies' buttons are on screen. Fine? Suggested: yes.

## Test
- Claude: the GUI test passes with the owner's copy running, and the owner's copy is still running afterwards with its buttons where they were.
- User: keeps working with the everyday copy during a test run; its grid and buttons still work afterwards.

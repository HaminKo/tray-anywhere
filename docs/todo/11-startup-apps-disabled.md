# To do 11: Start with Windows notices a "disabled" from Task Manager's Startup apps (small)

## Problem / Goal
"Start with Windows also notices, and clears, a 'disabled' set in Task Manager's Startup apps; today the tick stays on then" (owner's choice, 2026-11-14, after To do 5; raised by its code review). Example: the owner turns tray-anywhere off once in Task Manager > Startup apps (or Settings > Apps > Startup). Windows keeps the Run value but marks it disabled in a key of its own, HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run, so the app doesn't start at sign-in. The menu still shows the tick, and unticking and ticking again rewrites only the Run value, so the menu can't turn it back on.

Not seen on this PC. The StartupApproved value's format is undocumented (said to be 12 bytes, the first one even for on and odd for off; not checked).

## Read first
- src/TrayAnywhere/Autostart.cs (IsOn, Set); src/TrayAnywhere/AppMenu.cs.
- docs/decisions.md: "Start with Windows: ...".

## Approach
- Measure first: turn the entry off and on in Task Manager and read the StartupApproved value each time; check whether deleting that value makes Windows treat the entry as on again.
- IsOn: off when StartupApproved says disabled. Set(true): also delete the app's StartupApproved value.
- The GUI test's test key would need a StartupApproved key of its own too (a second test-only key, or one option for both).

## Ask the user
- Should ticking in the menu override a "disabled" from Task Manager? Suggested: yes; the last place the owner changed it wins.

## Test
- Claude: with a "disabled" value in the test's StartupApproved key, the menu shows no tick, and ticking clears it.
- User: turns tray-anywhere off in Task Manager's Startup apps, sees no tick in the menu, ticks it, and Task Manager shows it enabled again.

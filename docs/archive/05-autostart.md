# To do 5: Start with Windows, and the README's run and autostart steps (small)

## Problem / Goal
An optional "Start with Windows" setting, and a README that says how to build, run and autostart the app, with its known limitations.

## Read first
- docs/decisions.md: settings file and the Run registry key.
- README.md.

## Approach
- "Start with Windows" in the floating button's right-click menu: writes or removes a value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run pointing at the built exe.
- A publish command that makes one folder to run from (framework-dependent, so it stays small).
- README: build and run, autostart, known limitations; details in docs/guide.md if they grow.

## Ask the user
- Where does the published app live? Suggested: a `publish/` folder in the project (gitignored), so autostart points at a stable path.

## Test
- Claude: the setting adds and removes the registry value.
- User: turns it on, signs out and in, and checks the button appears.

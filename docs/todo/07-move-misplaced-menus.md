# To do 7: Move a tray app's menu that opens away from the mouse next to it (medium)

## Problem / Goal
"After a forwarded click, move a tray app's menu that opens away from the mouse or off the grid's monitor next to the mouse" (owner's go-ahead, 2026-11-14). Example: on the right monitor (150 %, top-left corner 2560,-865), a right click on Steam's tile in the grid opens Steam's menu 1280 px right of and 433 px above the mouse; with the grid in that monitor's right half the menu is entirely off-screen (measured, 3 of 3 clicks). It is Steam's own bug: its menu lands at mouse + monitor's top-left corner x (scale - 1), so it opens at the mouse on the 100 % monitor and on the laptop screen (measured, 2 of 2 on the 100 % monitor).

Also seen: OneDrive opens its window above the real icon on the laptop screen (To do 2), and so did Malwarebytes' tray view once (logged in To do 3 step B). Whether those should move is open (see below).

## Read first
- docs/research/uia-tray.md "Steam's menu didn't open" and "Where Steam's menu opens" (the measurements and the logger used).
- docs/decisions.md "A forwarded click lets any program come to the front".
- src/TrayAnywhere/App.cs `ClickIcon`; src/Tray/TrayClicks.cs.

## Approach
- First a spike of a few minutes: with Steam's menu open on the right monitor, move it with `SetWindowPos` from a script; the owner checks that the hover highlight and a click on a menu item still work. If they don't, stop and keep the known limitation.
- If they do: after a forwarded click, watch for about 1 s for a new visible top-level window of any program (Steam's menu belongs to steamwebhelper, not steam.exe). If it lands on the grid's monitor far from the mouse, or not fully on any screen, move it so its corner is at the mouse, inside the monitor's free area, opening away from the edges as a menu does. Win32 `EnumWindows`, `GetWindowRect`, `SetWindowPos`; the code goes in src/TrayAnywhere (it moves windows, it doesn't read the tray).
- The hard part: telling the click's window apart from unrelated windows that appear in that second (the logger saw other programs' popups come and go). Perhaps only small popup windows, or only windows that appear within ~0.5 s.
- Then the README's line about Steam's menu under "Known limitations" goes.

## Ask the user
- Which windows to move: only ones that land far from the mouse on the grid's monitor or off-screen (Steam's case), or also windows that open on the laptop screen (OneDrive, Malwarebytes)? Suggested: only the first; decide on the second after seeing it work.

## Test
- Claude: build and format checks; the spike's result in docs/research/uia-tray.md.
- User: right-click Steam's tile with the grid at the left and at the right of the right monitor: the menu opens at the mouse and its items work. On the top-left monitor nothing changes; Discord's menu still opens at the mouse; OneDrive's window still opens where it did.

## Progress
Choices (owner, 2026-11-15, Claude's suggestions): move only windows that land far from the mouse on the grid's monitor or not fully on any screen, not ones fully on another monitor (OneDrive, Malwarebytes); any program's window, but only menu-like popups, with the filter designed from the spike's measurements (Steam only, by its helper steamwebhelper.exe, if no clean filter shows); the moved window's corner at the mouse, opening up and to the right, flipped left or down where there is no room; the spike may click Discord's and OneDrive's tiles. The anchor is the mouse if it is over the grid, else the clicked tile (the menu key, the GUI test). Two steps, one chat each.
- [x] Step A (done 2026-11-16): spike, a logger in a temp folder (not committed). Results in docs/research/uia-tray.md "To do 7 step A"; the decision in docs/decisions.md (2026-11-16).
  - Moving Steam's menu works: hover and item clicks keep working (owner). Steam reuses one hidden menu window, shows it at its previous place, moves it 13 to 80 ms later, and moves it back to its wrong place just before hiding it.
  - Steam only: the owner's Session Inbox popups look like menus. Steam's own scaling setting fixes the place but makes Steam small on 150 % monitors: rejected (owner, who called a mover "a bandage" until the cause turned out to be Steam's).
  - WinEvent hooks instead of a 1.5 s wait or 15 ms checks: instant, and a flash at the wrong place only about once per 15 spam clicks (owner's test). No more work on that flash (stopping rule).
- [ ] Step B: build the Steam-only mover (~100 lines app, ~60 lines GUI test). Sonnet would do; the design:
  - src/TrayAnywhere/SteamMenu.cs (new): `Watch(Point anchor)` and `Dispose`. Watch sets two out-of-context WinEvent hooks if not set (`SetWinEventHook` EVENT_OBJECT_SHOW 0x8002 to EVENT_OBJECT_HIDE 0x8003, and EVENT_OBJECT_LOCATIONCHANGE 0x800B; flags WINEVENT_OUTOFCONTEXT 0 | WINEVENT_SKIPOWNPROCESS 2; callbacks arrive on the UI thread's message loop), keeps the anchor, and starts a ~1 s timer that removes the hooks if Steam's menu hasn't shown. Keep the callback delegate in a field (else the GC collects it while Windows still calls it).
  - Callback: only idObject 0 (OBJID_WINDOW) and idChild 0; then class `SDL_app` and title "Menu" (cheap), then process steamwebhelper (looked up only after those match). HIDE: remove the hooks. SHOW or LOCATIONCHANGE while visible: if the anchor is within 50 px of the menu's rectangle, leave it (Steam placed it right: 100 % monitors, the laptop screen; this also stops our own move's event from looping); else `SetWindowPos` (SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE) to: x = anchor.X if the menu fits to its right, else anchor.X - width; y = anchor.Y - height if it fits above, else anchor.Y; then `TopWindow.KeepInside` the anchor monitor's working area.
  - App.ClickIcon: call `Watch` before the click is sent, not after: the menu showed 51 ms after the button release (measured), and the click call takes 17 to 82 ms; a menu shown before the hooks exist and already at its final wrong place gives no later event. For left and right clicks (Steam's menu opens on both).
  - Anchor: the mouse if it is over the grid, else the centre of the clicked tile (menu key, GUI test). GridForm.IconClicked passes it (or the tile's screen rectangle).
  - GUI test (open, ask in step B; suggested): the test can't make a steamwebhelper window, so with `--gui-test` the app also accepts the GUI test's own process; on a right click, the test's icon opens a window of class `SDL_app` titled "Menu" (RegisterClass, shown without activation) at Steam's wrong place (anchor + (1280, -433)); check that the app moves it so a corner is at the tile. Alternative: drop the process check (class and title only) and need no switch.
  - Docs: README's Steam line under "Known limitations" goes; docs/research/uia-tray.md "In the code (To do 7)"; CLAUDE.md "Must keep working": Steam's tray menu opens at the click on every monitor.
  - Owner's test: Steam's menu with the grid in the left and right halves of the right monitor (single clicks and spam; an item click works); on the top-left monitor unchanged; Discord's menu still at the mouse; OneDrive's window where it was; a Session Inbox popup isn't moved.

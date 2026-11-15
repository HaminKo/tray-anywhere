# To do 8: Read the tray less often while the grid isn't used (small)

## Problem / Goal
"Read less often while the grid isn't being used" (owner, 2026-11-14, after To do 3 step B). While the grid is open it reads the whole tray every 2 s, also when nobody looks at it, and it can stay open all day. Measured once in To do 3 step B: about 13 % of one processor thread while open (6.6 % in the app, about 6 % more in Explorer, estimated from the difference), about 0.4 % of the PC. On the laptop that is a small but steady cost on battery.

## Read first
- docs/decisions.md "The grid app" (the refresh bullet and its measurement).
- docs/research/uia-tray.md "CPU of the 2 s refresh".
- src/TrayAnywhere/App.cs: `refreshTimer`, `RefreshGrid`.

## Approach
- Every 2 s while the mouse is over the grid or it has the focus; otherwise, e.g., every 10 s. Back to 2 s, with one read at once, when the mouse enters the grid.
- Measure again afterwards, the same way (a minute with the grid open and unused).

## Ask the user
- How slow while unused? Suggested: 10 s; an icon that comes or goes then shows within 10 s, or at once when the mouse comes to the grid.

## Test
- Claude: build and format checks; the CPU measurement with the grid unused.
- User: the grid open and unused for a while, then the mouse onto it: a tray app started meanwhile shows up at once.

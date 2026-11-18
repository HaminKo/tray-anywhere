# To do 12: Live pictures for overflow icons (medium)

## Problem / Goal
"Live pictures for overflow icons (the exact current image, e.g. a changing badge)." Taskbar icons are drawn live from the taskbar (PrintWindow); overflow icons show Windows' stored snapshot (`IconSnapshot` in NotifyIconSettings), which can be older than the real icon. Example (owner's test, 2026-11-14): Discord's overflow icon has a red badge in the real flyout but none in the grid; Windows Security's snapshot shows a yellow warning while its real icon has a green check.

Already ruled out (docs/research/uia-tray.md "Spike step C", measured): the hidden overflow window has a 0x0 rectangle, so PrintWindow draws nothing; a screen copy shows whatever covers the tray; UI Automation gives no picture; ITrayNotify gives nothing. Opening the flyout and drawing it works, but flashes at every read (rejected, docs/decisions.md 2026-11-14).

## Read first
- docs/research/uia-tray.md "Spike step B" (the overflow window) and "Spike step C" (dead ends, the open flyout captured, PrintWindow on the taskbar).
- docs/decisions.md 2026-11-14 "Icon pictures: drawn live for taskbar icons; ...".
- src/Tray/Taskbar.cs: `ReadVisibleItems` (the live crop), `ReadOverflowItems`. src/Tray/IconOwners.cs and IconSettings.cs (where overflow items get their snapshot).

## Approach
A research step first, with a stop rule.
- Measure how stale the snapshot is: does Windows rewrite `IconSnapshot` when an icon changes its picture? A test icon that changes picture every few seconds, the value read before and after. If Windows rewrites it within seconds, the grid is already close to live and only the docs change.
- Then try ways that don't show the flyout, each in a probe: e.g. PrintWindow while Explorer's overflow window is shown off-screen or fully transparent. That changes Explorer's own window: only with the owner's OK, and check afterwards that the real flyout still opens normally.
- Stop rule: if nothing works without a flash or lasting changes to Explorer's windows, write the dead ends into uia-tray.md and close the to-do as not possible.

## Ask the user
- Is a flash acceptable if it's rare, e.g. once per grid opening? Suggested: no (the 2026-11-14 decision stands).
- May a probe show Explorer's overflow window off-screen or transparent for a moment? Suggested: yes, in a probe only, with the flyout checked afterwards.

## Test
- Claude: a test icon in the overflow that changes picture; TrayDump `-- pictures` saves the new picture within one read, and the GUI test's grid shows it within one refresh.
- User: an app with a badge in the overflow (Discord's red dot): the grid shows the badge.

## Progress
- Step A (2026-11-18), research: no way found; closed as not possible by the stop rule. Windows writes the snapshot only when it first sees the icon; ITrayNotify sends nothing; Explorer's overflow window shown off-screen draws black (only the arrow's opening draws it); cloaking it is refused. Details: docs/research/uia-tray.md "To do 12"; decision: docs/decisions.md 2026-11-18. README's known limitations now name the missing badges.

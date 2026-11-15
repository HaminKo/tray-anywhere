## Where we are
- Now: To do 10 (run the GUI test beside the everyday copy)
- Next: none
- Updated: 2026-11-15

## To do
Next number: 12. Numbers are permanent: never renumber or reuse one; to move a to-do, move its line.

### Now
- To do 10: Run the GUI test while the everyday copy is running (medium; user 2026-11-14): docs/todo/10-test-beside-running-copy.md

### Bugs me

### Someday
- To do 6: When no icons are hidden, read an empty overflow instead of failing (small; user 2026-11-14): docs/todo/06-no-hidden-icons.md
- To do 7: Move a tray app's menu that opens away from the mouse next to it (medium; user 2026-11-14): docs/todo/07-move-misplaced-menus.md
- To do 9: An icon matched by order can take the picture of a program without a tray icon (medium; user 2026-11-14): docs/todo/09-wrong-order-match.md
- To do 11: Start with Windows notices a "disabled" from Task Manager's Startup apps (small; user 2026-11-14): docs/todo/11-startup-apps-disabled.md

## Later, only if the user asks
- Live pictures for overflow icons (the exact current image, e.g. a changing badge). v1 draws taskbar icons live and uses Windows' stored snapshot for overflow icons (To do 2 step C).
- Notification balloons and toasts from tray apps.
- Drag to reorder icons.
- An installer.
- A settings window (v1 uses a small right-click menu on the floating button, plus the settings file).

## Writing a to-do
- Number: the "Next number" (then raise that line by one). The line goes under Someday, unless the user says it bugs them or wants it soon: then under Bugs me where the user says (at most 5 there, so ask which one drops back to Someday), and "Next:" and "Updated:" in "Where we are" change with it.
- Title with size (small / medium / large) and the goal in the user's words.
- Problem / Goal: what is wrong or wanted, with an example. For a bug or research to-do, also what the user has seen (it exists, where, what works) and what is already ruled out.
- Read first: files and functions to look at, outside references.
- Approach: the suggested design, what to reuse.
- Ask the user: open questions, each with a suggested answer.
- Test: what Claude checks, and what the user tests by hand.

Every to-do is its own file, docs/todo/NN-name.md (two-digit number, short name), starting "# To do N: title (size)" and holding the parts above (later also its Progress list). This list holds one line per to-do: `- To do N: title (size; user YYYY-MM-DD): docs/todo/NN-name.md`. When a to-do is finished, finish-step moves its file unchanged to docs/archive/.

## Before a public release
- Pick a license (LICENSE file; ask the user, with a suggestion).
- Search all tracked files for the "Never in tracked files" list in CLAUDE.md. If old commits hold any of it, publish a new repo with one clean commit instead of rewriting history.
- A fresh subagent that hasn't seen the project checks README.md and the docs it links against the code, and only reports; the user decides the fixes.
- The README follows the "README" rule in ~/.claude/CLAUDE.md: short plain sentences, details in docs/, no section longer than about a screen.
- A screenshot of the grid in the README.
- Start CHANGELOG.md (Keep a Changelog: Unreleased first, then versions newest first) with the first version.

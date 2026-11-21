## Where we are
- Now: To do 17, step B (the monitor list, with the button's on/off)
- Next: none
- Updated: 2026-11-21

## To do
Next number: 19. Numbers are permanent: never renumber or reuse one; to move a to-do, move its line.

### Now
- To do 17: A settings window (medium; user 2026-11-20): docs/todo/17-settings-window.md

### Bugs me

### Someday
- To do 18: Hide the floating button while a full-screen program is in front (small; Claude 2026-11-20): docs/todo/18-hide-button-full-screen.md

## Later, only if the user asks

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
- Publish the first GitHub release (0.1.0) by docs/releasing.md, after the license and the README screenshot.

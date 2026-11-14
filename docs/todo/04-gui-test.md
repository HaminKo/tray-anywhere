# To do 4: GUI test and screenshots (medium)

## Problem / Goal
Once the grid window exists (To do 3), Claude needs a way to see it: a test that starts the app, opens the grid and checks what it shows, plus screenshots Claude can read.

## Read first
- The gui-test skill ("Setting it up").
- src/TrayAnywhere from To do 3.

## Approach
- Follow the gui-test skill's setup for a desktop window. Drive the app with UI Automation, which is already in the project, so nothing new is needed.
- Screenshot the grid on a secondary monitor into a gitignored folder.
- Add the commands to CLAUDE.md "Build specifics" and docs/local.md.

## Ask the user
- Which monitor for test screenshots? Suggested: whichever the mouse is on, as in the app.

## Test
- Claude: the test passes and a screenshot shows the grid with icons.
- User: runs the test command once.

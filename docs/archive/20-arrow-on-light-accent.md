# To do 20: A readable arrow on a floating button in a light accent color (small)

## Problem / Goal
"Floating button: a dark arrow, or Windows' darker accent shade, when the accent color is light." A button set to "Windows' accent color" (To do 17 step D) keeps the light arrow, (220, 220, 220). With a light accent, such as yellow (255, 185, 0) or a light grey, the arrow is hard to see, and harder under the mouse, where the button gets 15 % lighter. The fixed colors were picked dark enough for the light arrow; the accent is whatever the user has. Found in To do 17 step D's code review (owner: add it as Someday, 2026-11-21); not seen on this PC, whose accent is #0078D4.

## Read first
- src/TrayAnywhere/ButtonLook.cs (`ButtonColors.Background`, `AccentColor`, `Hover`), FloatingButton.cs (`ArrowColor`, `OnPaint`, `SetColors`), SettingsForm.cs (`MakeSquare`: the accent's square).
- docs/research/gui-testing.md 2026-11-21 "the button's color, size and always on top": the registry values for the accent (`DWM\AccentColor`, `Explorer\Accent\AccentPalette`, `StartColorMenu`).
- docs/decisions.md 2026-11-20 "A settings window ..." (the owner: "The arrow stays light").

## Approach
- Either a dark arrow when the background is light (by its brightness, e.g. 0.299 R + 0.587 G + 0.114 B over about 160; the threshold to try on the screenshots), or the accent's darker shade (AccentPalette entry 4, as `StartColorMenu`; #0067C0 here), the one Windows uses on dark surfaces.
- One rule for the button and the accent's square in the settings window.

## Ask the user
- Dark arrow or darker accent? Suggested: the darker accent shade: the arrow stays light like the rest of the app, and it is what Windows' own dark-mode surfaces show; but then "Windows' accent color" is a shade darker than the one in Settings.
- Only for the accent, or for every color? Suggested: only the accent; the fixed colors are dark enough.

## Test
- Claude: a GUI test can't set Windows' accent (it writes only its own registry key), so the rule is checked by the screenshots with a light color forced in a test-only way, or by hand. Suggested: by hand.
- User: pick a yellow accent in Settings > Personalization > Colors, with the button set to Windows' accent color: the arrow is easy to see, also under the mouse. Then set the accent back.

# AccessCalibrate UI v2: design spec

This replaces the visual design in `Docs/DESIGN_SPEC.md`. The flow, the checks and the rules engine stay the same, except for the additions listed under "New behaviour".

**Source of truth:** the HTML files in `Docs/design-v2/screens/<theme>/`. Open them in a browser. Each one plays its motion on load, and **R** replays it. `Docs/design-v2/png/` has a still of the final state of every screen (1280×720, downscaled from the 2× captures). If this document and an HTML file disagree, the HTML file wins. Read exact sizes, colours and timings from the HTML.

Reference frame: **1280 × 720**. Scale the whole layout proportionally to the screen.

---

## 1. Screens (14 per theme, 3 themes)

| # | File | What it is |
|---|---|---|
| 01 | welcome | Title, 5 steps, "Playing with <device> · Change", Start / Not now |
| 02 | change-device | Modal device picker over the current check |
| 03 | reaction-ready | Reaction, waiting for the signal |
| 04 | reaction-signal | Reaction, signal on |
| 05 | hold-ready | Hold, before the press (controller drawing) |
| 06 | hold-active | Hold, while holding (progress ring) |
| 07 | presses-ready | Repeated presses, before the first press |
| 08 | presses-active | Repeated presses, counting (tick ring) |
| 09 | steadiness-ready | Steadiness, bring the dot into the circle |
| 10 | steadiness-active | Steadiness, measuring |
| 11 | button-reach | Button reach, list + controller |
| 12 | suggestions | List + detail panel + Advanced slider |
| 13 | no-changes | Shown instead of 12 when there are no suggestions |
| 14 | summary | Results table after Apply |

Themes: `dark` (Galaxy, default), `light`, `high-contrast`. They replace the old Glass Dark, Glass Light and High Contrast presets, and they keep the same `UseTheme` API.

## 2. Layout of a check screen (03–11)

- **Top bar:** 64 px. "Control setup" on the left, the step indicator in the centre, and "<device> · Change" on the right.
- **Bottom bar:** 60 px, with a 1 px top line. Hints go on the left and "Buttons on screen match your device" on the right.
- **Three columns between the bars, each centred vertically:**
  - left x 40–330: title, instruction, line, "What we measure", tip;
  - centre x 370–910: the device drawing or measurement, the status line and a sub line;
  - right x 950–1240: "Your options" (Skip this check, Back, Skip the whole setup) and a note.
- **Step indicator:** done steps are filled 30 px circles. The current step is a pill with its number and name, a soft glow and a pulsing dot. Upcoming steps are outlined circles.

## 3. Tokens

| Token | dark | light | high-contrast |
|---|---|---|---|
| background | galaxy image `galaxy.jpg` + overlay rgba(3,5,14,0.38), slow drift 70 s | gradient #F6F7F9 → #E8EAEE | #000000 |
| ink (text) | #EEF2FF | #11141C | #FFFFFF |
| muted text | ink 64 % | ink 66 % | #FFFFFF |
| line | rgba(170,195,255,0.16) | ink 16 % | white 70 % |
| surface (cards) | rgba(12,20,46,0.62) | white 94 % | #000 + white border |
| accent (selection, current step, signal) | #F2EEE6 | #11141C | #FFD60A |
| text on accent / on primary button | #07142A | #FFFFFF | #000000 |

Accent is a theme variable. Studios can override it.

**Type:** Manrope (already bundled). Scale used: 12 / 14 / 15 / 16 / 17 / 18 / 19 / 22 / 32–38 (titles) / 46–48 (Welcome, No changes). Radii: 4 px (rows), 6 px (buttons, panels), 50 % (glyphs, step circles). No uppercase labels, no letter-spaced eyebrows.

**Text rules:**
- Body copy is at most 2–3 lines.
- No line holds a single word or two short words. Where a text is centred and then aligned left during the intro, line breaks are fixed by hand, so it is split into one element per line. Keep the exact breaks from the HTML.

**Glyphs:** controller glyphs are 30 px circles. Keys are 30 px rounded boxes. ≡ (Menu) means skip, and holding ≡ means skip the whole setup.

## 4. Motion

All motion respects reduce motion: with it on, show the final state with no animation. Nothing flashes.

**"Fog-in"** (used everywhere): opacity 0 → 1, blur 14 px → 0, translateY 6–8 px → 0, ease-out. UI Toolkit has no element blur, so approximate it with opacity plus a 1.06 → 1 scale.

**Check intro** (03, 05, 07, 09, 11; total about 10 s):

| t (s) | What happens |
|---|---|
| 0.0 – 1.3 | Title + instruction fog in, **centred** in the middle of the screen at scale 1.08 |
| 1.3 – 2.7 | Hold, so the player can read |
| 2.7 – 4.2 | The block slides to the left column. **Each line slides from its centred offset to the left edge as it moves** (one element per line, translateX from `(blockWidth − lineWidth)/2` to 0) |
| 4.1 – 5.3 | The divider line draws left → right (scaleX 0 → 1) |
| 4.3 – 7.7 | "What we measure" does the same: fog in centred, hold, slide left under the line, lines re-aligning |
| 7.8 | The tip fogs in at its place |
| 8.9 | The centre (device drawing) fogs in |
| 9.6 | "Your options" label fades in |
| 10.0+ | Option cards **stack**: the first appears, the others drop out from behind it one by one (translateY −n×56 px → 0, overshoot easing cubic-bezier(.34,1.56,.64,1), 0.14 s apart) |
| 10.8 | Note under the options fogs in |

Button reach (11) is the same idea: the title moves left, then the button list stacks in on the left (52 px step), then the controller, then the options stack on the right.

**Ready → active transition** (04, 06, 08, 10; about 2 s): the left and right columns stay. The centre fades out with blur and scales to 0.94 (0.7 s, from 0.4 s). The new centre fogs in (0.9 s, from 0.9 s). The status line follows at 1.3 s and the sub line at 1.5 s.

**Per-screen loops:**
- 04: three rings ripple out of the circle (2.4 s, 0.8 s apart, scale 1 → 1.5, fade out). A dashed ring rotates in 8 s. On press, the circle squeezes to 0.9 and shakes ±5 px for 0.45 s.
- 06: the ring glow breathes (2 s). The dot at the end of the arc pulses in place. The whole ring scales 1 → 1.015 over 3 s.
- 08: one tick lights up per press out of 40. The latest tick is longer.
- 10: the dot follows input smoothly (about 120 ms). The ring is accent-coloured while the dot is inside and dims outside. No trail.
- Current step dot: pulse 1.8 s. The Welcome first dot also has a rotating dashed ring.

**Other screens:**
- 01 Welcome: title (0.2 s) → subtitle (0.9 s) → steps one by one (from 1.7 s, 0.18 s apart) → line draws (2.6 s) → dots (3.4 s) → device line (3.8 s) → buttons (4.2 s).
- 02 Change device: the scrim fades in, then the dialog rises 24 px and sharpens (0.6 s), then the rows drop in with overshoot, 0.12 s apart.
- 12 Suggestions: title → subtitle → suggestion rows stack (66 px step) → detail panel slides in from the right → the Default / With this bars grow left → right → buttons.
- 13 No changes: title → text → the line draws while each step dot pops in (0.4 s apart) with its name → buttons.
- 14 Summary: title → header → rows slide in from the left (0.25 s apart), each "Applied" pops → buttons.

## 5. New behaviour (needs code and tests)

1. **Device picker** (02): opened from "Change" on any screen. Options are Xbox, PlayStation, Keyboard & mouse and Touch, with the detected one marked. Confirming restarts the current check with the new device. Glyphs, the drawing and the rules follow the choice.
2. **Device drawing** in the centre of every ready screen, with the target control highlighted and a callout ("Hold A", "Press Y", "Left stick"). Keyboard & mouse shows a full ANSI keyboard (correct layout) with the key highlighted, or the mouse for Steadiness. PlayStation and Touch drawings are still to be designed: use the Xbox drawing with PlayStation glyphs for now.
3. **Back** (go to the previous check) and **Skip the whole setup** (hold ≡ / Shift+Esc) in every check.
4. **Play the tone** on Reaction plays the signal sound once.
5. **Suggestions detail panel**: Why (measurement + rule), In the game (effect), Default vs With this bars, and an **Advanced slider** to adjust the suggested value (←/→ on the selected row). The adjusted value is what gets applied and stored.
6. **Summary**: one row per check with the value, a small scale showing the value against the rule threshold, and the resulting setting. Copy results and Redo as before.
7. **No changes** state (13) replaces the suggestions screen when the list is empty.

## 6. Accessibility checks to keep or add

- Text contrast ≥ 4.5:1 in all three themes (extend the existing contrast test to the new tokens).
- Reduce motion shows the final states with no motion.
- Nothing flashes, and there are no loops faster than 1 s.
- Every action is reachable by gamepad, keyboard and pointer, with hints in the bottom bar.

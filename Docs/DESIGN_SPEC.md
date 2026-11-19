# AccessCalibrate — UI design spec ("console overlay")

Replaces the placeholder UI (CalibrationView.uxml / CalibrationViewStyle.uss / UIToolkitCalibrationView.cs).
Logic, tests and `ICalibrationView` stay as they are. Reference size: 1280×720 (PanelSettings: Scale With Screen Size, reference 1280×720, match 0.5).

## 1. Direction

A system-style overlay that sits ON TOP of the game, like PS5 / Xbox system menus:
- the game stays visible behind a dark dim layer;
- one large translucent panel, big rounded corners, thin light border;
- monochrome: white text, white "focus" fill for the selected control (dark text on it). No accent color by default: the game's own accent comes in through the theme;
- no icons in cards, no gradients, no emoji. Numbers (01–05) instead of icons;
- controller-glyph hint bar at the bottom (A Select, B Back, Esc Skip);
- "glass" surfaces: translucent fill + 1px light hairline border + a 1px inner highlight on the TOP edge (`inset 0 1px 0`). Do NOT draw gradient borders under translucent fills: the gradient shows through and makes white-on-white text.

## 2. Theme tokens (USS custom properties on `.ac-root`)

Every color, radius and font in the stylesheet must come from these variables, so a developer re-themes by overriding 5–10 values in their own USS.

| Token | Default | Use |
|---|---|---|
| `--ac-overlay` | `rgba(8, 10, 14, 0.55)` | full-screen dim over the game |
| `--ac-panel` | `rgba(20, 22, 30, 0.62)` with blur / `rgba(20, 22, 30, 0.88)` without | main panel. UI Toolkit has no backdrop blur: by default use the 0.88 value; optional blur (see section 6b) allows 0.62 |
| `--ac-glass-border` | `rgba(255, 255, 255, 0.16)` | 1px border of glass elements (panel uses 0.14) |
| `--ac-glass-highlight` | `rgba(255, 255, 255, 0.20)` | 1px top-edge highlight of glass elements (panel 0.22) |
| `--ac-panel-border` | `rgba(255, 255, 255, 0.10)` | 1px panel border, rail divider |
| `--ac-text` | `#FFFFFF` | titles, labels |
| `--ac-text-secondary` | `rgba(255, 255, 255, 0.78)` | body text |
| `--ac-text-muted` | `rgba(255, 255, 255, 0.62)` | eyebrows, counters, hints |
| `--ac-surface` | `rgba(255, 255, 255, 0.07)` | glass: secondary buttons, chips, ready bar (rows: 0.05) |
| `--ac-surface-focus` | `rgba(255, 255, 255, 0.10)` | focused row background (keep it dark: white text must stay ≥ 4.5:1) |
| `--ac-focus-border` | `rgba(255, 255, 255, 0.55)` | focused row 1.5px border |
| `--ac-track` | `rgba(255, 255, 255, 0.16)` | progress tracks, switch off |
| `--ac-focus` | `#FFFFFF` | primary button, selected rail item, switch on, stimulus |
| `--ac-on-focus` | `#0B0C10` | text / knob on `--ac-focus` |
| `--ac-uncertain-border` | `rgba(255, 255, 255, 0.34)` | dashed border of an uncertain row |
| `--ac-radius-panel` | `36px` | panel |
| `--ac-radius-control` | `20px` | buttons, rows |
| `--ac-radius-row` | `16px` | rail items |

Font: **Manrope** (SIL Open Font License 1.1: redistributable; ship the font file + its OFL.txt, mention it in NOTICE). Weights 500 / 600 / 700 / 800. Fallback: Unity's default font. Expose the font as a FontAsset field so a developer can swap it.

### How a developer applies a theme

- A `CalibrationTheme` ScriptableObject (Create → AccessCalibrate → Theme) with: `StyleSheet themeOverrides` (a USS that only overrides `--ac-…` variables), `FontAsset font`, and a `preset` enum.
- 3 built-in presets shipped as USS files: **Glass Dark** (default), **Glass Light**, **High Contrast** (solid black panel, pure white text, yellow focus, no translucency).
- The `CalibrationRunner` component has a `theme` field; empty = Glass Dark. The theme's USS is added AFTER the default stylesheet, so it only changes what it overrides.
- A theme change is live in Play mode (no restart), so a developer can tune it while looking at the screen.
- Players are not locked in: if the OS / game has a "high contrast" setting, the game can switch to the High Contrast preset through `AccessCalibrate.UseTheme(...)`.

Bonus (portfolio): an editor-only check that warns in the Console when a theme's `--ac-text` on `--ac-panel`, or `--ac-on-focus` on `--ac-focus`, is below 4.5:1 contrast.

## 3. Layout primitives

- **Overlay**: full screen, `--ac-overlay`.
- **Panel**: centered, 90% width (max 1400px), 82% height, radius `--ac-radius-panel`, background `--ac-panel`, 1px `--ac-panel-border`.
- **Rail** (intro screens only): left column 360px, padding 40px 24px, right border 1px `--ac-panel-border`.
  - Eyebrow "CONTROL SETUP": 13px, weight 700, letter-spacing ~0.14em, uppercase, `--ac-text-muted`.
  - 5 items, 56px tall, radius `--ac-radius-row`, 19px weight 600, number "01"…"05" in a 26px column.
  - Done: number replaced by a check mark, text `--ac-text-muted`.
  - Current: background `--ac-focus`, text `--ac-on-focus`, weight 700.
  - Upcoming: text 88% white, number `--ac-text-muted`.
  - Bottom note, 15px, `--ac-text-muted`.
- **In-test top bar** (no rail during measurement, to keep focus): chip "01 / 05" (32px tall, radius 10px, `--ac-surface`, 15px 700, tabular numbers) + test name 20px 700. Right: "Try 3 of 6" 15px muted + one 28×6 bar per trial (done = `--ac-focus`, current = 55% white, upcoming = `--ac-track`).
- **Hint bar** (bottom of panel): 15px weight 600, 72% white. Glyphs: round 26px outline badge; key badges ("Esc", "Select") 26px tall, radius 8px, 1.5px outline. Don't repeat a hint that is already shown on a button.
- **Glyphs follow the device in use** (last device that sent input) AND the current bindings: Xbox / generic gamepad: A B X Y; PlayStation: ✕ ○ □ △ (confirm = ✕); Switch Pro: A B X Y in Nintendo positions; keyboard: Enter / Esc / Space. Never show a glyph the player doesn't have.
- **Buttons**: 60–64px tall, radius `--ac-radius-control`, 19–20px.
  - Primary: subtle vertical gradient #FFFFFF → #E4E8F0 (opaque), `--ac-on-focus` text weight 800, a small dark round confirm glyph on the right, 4px soft white halo + shadow.
  - Secondary: glass (`--ac-surface`, `--ac-glass-border`, top highlight), white text weight 600.
  - Focused (keyboard / gamepad): primary look + scale 1.03.

## 4. Screens

1. **Welcome** (new, before test 1): rail (no step selected) + right side: H1 "Set up the controls around how you play" (54px, 800, letter-spacing −0.025em), body "Five quick checks, no score. We'll suggest settings, and you choose which ones to keep." (21px, secondary). Two stacked 420px buttons: "Start" (primary, focused) and "Not now" (secondary, right-aligned muted note "Uses default settings"). Rail note: "About 1 minute. You can redo it anytime from Options."
2. **Check intro** (before each test): rail with current step. Right: eyebrow "Step 2 of 5", H1 = test title, body = instructions, a 220px outlined circle with a big "A" glyph on the right. Bottom: a 72px `--ac-surface` bar "Press any button when you're ready" + muted "Waiting for input". Rail note: "There's no right or wrong result."
3. **Reaction**: top bar + centered stimulus: 232px solid `--ac-focus` circle with "Press" (40px 800, `--ac-on-focus`) inside a 280px 1.5px ring (22% white). Line below: "Any button, as soon as the circle appears". Footer left: speaker icon + "A soft tone plays with each signal". Before the signal: the ring only, no fill. Never flash.
4. **Hold**: top bar + 280px progress ring (10px stroke, track `--ac-track`, fill `--ac-focus`, round caps, starts at 12 o'clock; draw with `generateVisualContent` / Painter2D arc). Center: seconds "2.4" (64px 800, tabular) + "seconds of 5" (17px muted). Below: "Keep holding" (26px 800) + "Let go when it stops feeling comfortable".
5. **Steadiness**: top bar (right side shows "Left stick" or "Mouse"). Left: 360px round arena (4% white fill, 1.5px 14% border, faint crosshair 10%), 90px dashed target circle at the target position, cursor = 22px white dot with a dark 4px halo, plus a trail of the last ~6 samples as 6px dots at 30% white. Right: H1 "Keep the dot in the circle" (40px), body "Hold it as still as is comfortable. It doesn't need to be centred: we only look at how much it moves.", then "Measuring" + "3.1 s left" and a 6px progress bar.
6. **Suggested settings** (confirmation): panel padding 36/56/32; eyebrow "CONTROL SETUP · DONE", H1 "Here's what we suggest" (40px), right-aligned note "Keep what helps. Change it anytime in Options." (28px below the header). Rows 68px, gap 12px, radius 20px, padding 0 24 0 28, glass at 0.05 with 0.12 border (focused row: `--ac-surface-focus`, 1px `--ac-focus-border`, top highlight 0.25, soft shadow). At least 36px between the last row and the buttons; if there are more rows than fit, the list scrolls, the buttons never move. Row: title 19px 700 + reason 15px (68% white, tabular) on the left; switch on the right (60×34 track, 28px knob; on = `--ac-focus` track + `--ac-on-focus` knob, aligned right; off = `--ac-track` + 75% white knob, left). Uncertain: NO colored chip. The row gets a 1px DASHED border (34% white) and a fainter fill (2%); its reason line says it plainly, e.g. "Longest hold 1.4 s · not measured reliably, so it starts off"; switch OFF by default. (Dashed = "not solid": meaning carried by shape and text, not by color.) Buttons: primary label is live: "Apply N settings" / "Apply 1 setting" / "Continue without changes"; then "Redo setup", "Not now". Hint bar: only "Turn on / off" (its glyph); Apply is already on the primary button. Nothing to suggest: H1 "No changes needed", a centred message instead of the list, no hint, and only "Continue without changes" + "Redo setup" ("Not now" is hidden: it would do the same as "Continue without changes").

## 5. Copy fixes

- "2 button(s) not pressed in time" → "2 of 4 buttons not reached in time".
- One-handed reason → "Half of the buttons not reached in time".
- Confirmation reasons use "·" between measurement and rule, e.g. "Median reaction 620 ms · rule: above 500 ms → timing windows ×1.5".
- Keep every string in CalibrationTexts / ProposalTexts.

## 6. Motion and accessibility

- Panel: fade + 12px rise, 200ms ease-out. Focus change: scale 1.03 over 120ms. Progress values animate smoothly.
- Reduced motion ON: no scale, no rise, instant changes.
- Min text 15px at 1280×720. Text contrast ≥ 4.5:1 on the panel (defaults pass).
- Focus is always visible for keyboard and gamepad. Nothing flashes.
- Keep the existing 1 s input grace on the confirmation screen.

## 6b. Optional: real glass blur (bonus)

UI Toolkit can't blur what's behind it. Optional, off by default: a URP render pass (or a blurred, downscaled copy of the game camera into a RenderTexture) used as the panel background, so `--ac-panel` can drop to 0.62. Must have a toggle and fall back to the opaque panel on low-end devices / WebGL.

## 7. Done when

- All screens match this spec at 1280×720 and scale correctly at 1920×1080 and 2560×1440.
- Every color / radius / font in the USS is a `var(--ac-…)` token; README has a "Theming" section with the token table and a 10-line example override.
- Existing tests still pass; the render test saves one PNG per screen (ui-1-welcome … ui-6-confirmation) for review.

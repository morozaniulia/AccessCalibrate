# AccessCalibrate

> **Not a medical tool.** The thresholds are hypothetical starting points and have not been clinically validated.

AccessCalibrate is an open-source Unity package. On first launch it runs a short (~60 s) motor-ability calibration: reaction, comfortable hold, repeated presses, steadiness and button reach. It then proposes accessibility settings (timing windows, hold-to-toggle, mashing alternatives, stick deadzone, aim assist, remapping). Each proposal shows the reason it was made. **The player always has the final say:** every proposal can be switched on or off, and the package never changes a setting by itself.

*Work in progress. The full README (architecture, installation, prior work, results) comes with the final step.*

## Limits

- **Not clinically validated.** All thresholds in `RulesConfig` are hypotheses chosen by the developer and documented next to each value. They are not derived from other players' data and are not compared against any reference population.
- **Display latency is unknown.** The moment a signal appears is recorded when the frame that shows it is *processed* (`Time.realtimeSinceStartupAsDouble`), not when the screen actually lights up. Rendering, the display's refresh and its internal processing add an unknown delay, which makes measured reactions slightly slower than the player really is. Reaction rules therefore only fire above *threshold + `ReactionLatencyMarginMs`* (50 ms by default), so display lag alone does not trigger them.
- **Input timing comes from input events.** Press and release times come from `InputAction.CallbackContext.time`, not from `Update()`, so frame rate does not add to the measurement. The input device's own latency (USB / Bluetooth polling) is not measured.
- **Steadiness is a spread, not a diagnosis.** The steadiness test measures how much the cursor wanders around the player's own average position (2D standard deviation, as a fraction of the circle radius). Holding still slightly off-centre is not counted as unsteadiness. The distance from the centre is stored for information only, and no rule uses it.
- **Short samples.** Each test lasts a few seconds. Fatigue, practice, the time of day, or an unfamiliar controller can change the results. That is why every proposal stays editable, and the calibration can be repeated at any time.

## Demo

The package includes a short 3D level, playable in 1–2 minutes, that shows what each setting does. Import it from **Package Manager → AccessCalibrate → Samples → Demo**, then open `AccessCalibrateDemo.unity`.

| Station | Setting it shows |
|---|---|
| Rolling ball: press to jump | the quick-time window is `0.8 s × timingMultiplier`; `qteMode` Skippable adds a skip button, Off jumps for you |
| Lever | `holdToToggle`: one press instead of holding for 2 s |
| Drawbridge | `mashingMode`: tap about 12 times, hold for 3 s (HoldInstead) or automatic (Auto) |
| Narrow path | `stickDeadzone` is applied to the raw stick, so small movements are ignored |

The calibration runs on the first launch. **Redo calibration** (Start / F1) is always available, and a small panel shows the active settings.

The demo needs URP with a **Universal (3D) renderer** in the pipeline asset's renderer list (it doesn't have to be the default). The demo camera selects it automatically, so a 2D project keeps its 2D renderer as the default.

3D models: [Ultimate Platformer Pack](https://quaternius.com) by Quaternius, CC0 1.0 (public domain). Only the models the demo uses are included.

## Study mode

Study mode compares sessions, for example the same person with the dominant hand, the non-dominant hand and gloves. It is off by default. Turn it on in the demo (bottom-right panel) or in code:

```csharp
StudyMode.Enabled = true;
StudyMode.Label = "non-dominant hand";
```

Each finished calibration then appends one row to a local CSV file at `Application.persistentDataPath/AccessCalibrate/study/study.csv`. A row holds the raw trials, the device and confidence of each test, the proposed profile and the label. The row is written before the player decides, so it records what was *proposed*. The data never leaves the device.

To compare one or more CSV files, run:

```bash
pip install matplotlib
python tools/plot_study.py study.csv other-device.csv -o study.png
```

The script prints a table of medians per label, then draws one small chart per measurement. Each chart shows one dot per session, a line at each label's median, and the rule threshold as a dashed line. Uncertain measurements appear as hollow dots, and skipped ones are left out. Use `--table-only` to skip the chart (then matplotlib isn't needed). To run the script's tests: `cd tools && python -m unittest test_plot_study.py`.

## Theming

The built-in screens follow the "console overlay" design in [`Docs/DESIGN_SPEC.md`](Docs/DESIGN_SPEC.md): a translucent panel over the game, a step rail, a large white focus, and controller glyphs that follow the device in use (Xbox, PlayStation, Switch, keyboard). Every colour, radius and font in the stylesheet is a `--ac-…` variable defined on `.ac-root`.

There are three presets: **Glass Dark** (default), **Glass Light** and **High Contrast** (solid black, white text, yellow focus). To use one, create a theme asset (**Assets → Create → AccessCalibrate → Theme**), pick a preset, optionally add your own USS and font, and assign it to `CalibrationRunner.Theme`. Changes show up live in Play mode. At runtime, for example when the game's own high-contrast option is on, call:

```csharp
AccessCalibrate.UseTheme(ThemePreset.HighContrast);
```

A theme USS only overrides variables, and it is applied after the default stylesheet:

```css
/* MyGameTheme.uss: warm accent, squarer corners */
.ac-root {
    --ac-focus: #FFB347;             /* primary button, current step, switches "on" */
    --ac-on-focus: #1A1206;          /* text on the accent */
    --ac-panel: rgba(28, 20, 12, 0.90);
    --ac-panel-border: rgba(255, 179, 71, 0.30);
    --ac-radius-panel: 12px;
    --ac-radius-control: 8px;
    --ac-radius-row: 6px;
}
```

| Token | Glass Dark default | Used for |
|---|---|---|
| `--ac-overlay` | `rgba(8, 10, 14, 0.55)` | full-screen dim over the game |
| `--ac-panel` | `rgba(20, 22, 30, 0.88)` | main panel |
| `--ac-panel-border` / `--ac-panel-highlight` | white 14% / 22% | panel hairline / top-edge highlight |
| `--ac-divider` | white 10% | rail divider |
| `--ac-glass-border` / `--ac-glass-highlight` | white 16% / 20% | glass controls: border / top edge |
| `--ac-text` | `#FFFFFF` | titles, labels |
| `--ac-text-secondary` | white 78% | body text |
| `--ac-text-muted` | white 62% | eyebrows, counters, notes |
| `--ac-text-hint` / `--ac-text-reason` | white 72% / 68% | hint bar / reason lines |
| `--ac-surface` / `--ac-surface-row` | white 7% / 5% | secondary buttons, chips / setting rows |
| `--ac-surface-focus` / `--ac-focus-border` | white 10% / 55% | focused row background / border |
| `--ac-focus` / `--ac-on-focus` | `#FFFFFF` / `#0B0C10` | primary button, current step, switch on, signal / text on it |
| `--ac-track` | white 16% | progress tracks, switch off |
| `--ac-uncertain-border` | white 34% | dashed outline of an uncertain row |
| `--ac-radius-panel` / `-control` / `-row` | `36px` / `20px` / `16px` | corners |
| `--ac-font-500` … `--ac-font-800` | Manrope Medium … ExtraBold | text weights |

A test checks that every preset keeps text at **4.5:1 contrast or more** over a black, a white and a grey game behind the panel. Another test checks that the stylesheet uses no colour that isn't a token.

Font: [Manrope](https://github.com/googlefonts/manrope), under the SIL Open Font License 1.1 (see `NOTICE` and `Fonts/OFL.txt`). A theme's `font` field replaces it everywhere.

## Translating the texts

Every text the player sees lives in `CalibrationTexts` (static strings). To translate it, assign your own values once at startup, before the calibration runs:

```csharp
CalibrationTexts.ReactionTitle = "Reacción";
CalibrationTexts.ButtonJump    = "Saltar (A / Espacio)";
```

Keep the `{0}` / `{1}` placeholders. If you pass your own buttons to `CalibrationRunner.Buttons`, give them already-translated names.

## Privacy

All data stays on the device. No telemetry, no network access.

## License

Apache License 2.0. See `LICENSE` and `NOTICE`.

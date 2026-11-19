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

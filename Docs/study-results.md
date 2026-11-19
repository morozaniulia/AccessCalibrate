# Study results: first pilot (2026-11-19)

![Study chart](study-results.png)

This is a **small pilot**: one calibration per label, three labels, recorded with study mode on 2026-11-19. It checks that the pipeline works end to end (calibration → CSV → chart) and shows the kind of differences the checks pick up. Nothing here can be generalised: each label has only one session, the file doesn't record who took part, and the thresholds are hypotheses. The CSV itself stays on the device and is not in this repository.

## Medians per label

With one session per label, each median is that session's value.

| Label | Sessions | Reaction (median) | Longest hold | Repeated presses | Steadiness spread | Buttons not reached |
|---|---|---|---|---|---|---|
| normal | 1 | 290 ms | 5.0 s | 6.8 / s | 0.000 | 0 of 4 |
| non-dominant hand | 1 | 296 ms | 5.0 s | 7.2 / s | 0.032 | 0 of 4 |
| feet | 1 | 628 ms | 4.85 s | 0.2 / s | 0.417 | 0 of 4 |

Devices: keyboard for reaction, hold, repeated presses and button reach; mouse for steadiness. All measurements were marked *ok* (none uncertain, none skipped).

## What was proposed

The rules used each device's thresholds (keyboard: reaction above 450 + 50 ms, presses below 4.5 / s; mouse: spread above 0.10). The dashed lines in the chart show the gamepad defaults, not these values.

| Label | Proposed |
|---|---|
| normal | nothing |
| non-dominant hand | nothing |
| feet | timing windows ×1.5 (628 ms > 500 ms); hold instead of pressing rapidly (0.2 / s < 4.5 / s); bigger stick deadzone and medium aim assist (spread 0.417 > 0.10) |

## Observations

- **Feet vs hands.** Reaction roughly doubled (290–296 ms → 628 ms), and only one press was made in the repeated-presses window (0.2 / s). Holding a key was barely affected (4.85 s out of a 5 s maximum).
- **Steadiness with the mouse.** For *normal* the spread is exactly 0.000, so the cursor did not move at all for the 5 s of measuring. This most likely means the mouse was left untouched rather than held very still, and the value shouldn't be read as a steadiness result.
- **Single short hold.** For *normal* the second hold lasted 0.11 s, probably a quick tap. It has no effect because the result is the longest hold (5 s).
- **Anticipation.** The first *feet* reaction trial was a press 840 ms before the signal. The first trial is always dropped, and negative values would be filtered anyway.
- **Wrong setting for the device.** For *feet* the steadiness rule proposed a *stick* deadzone, although steadiness was measured with a mouse. This has been fixed since: a mouse measurement now proposes "Smooth small mouse movements" (`mouseSmoothing`) instead.

## Reproduce

```bash
python tools/plot_study.py <path to study.csv> -o Docs/study-results.png
```

On Windows the file is at `%USERPROFILE%\AppData\LocalLow\<company>\<product>\AccessCalibrate\study\study.csv`.

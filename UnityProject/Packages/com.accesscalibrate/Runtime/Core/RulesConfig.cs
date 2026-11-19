// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;

namespace AccessCalibration.Core
{
    /// <summary>
    /// Thresholds for one input device.
    ///
    /// IMPORTANT: every number here is a HYPOTHETICAL STARTING POINT chosen by the developer.
    /// None of them is clinically validated, and none is derived from other players' data.
    /// </summary>
    [Serializable]
    public sealed class DeviceThresholds
    {
        /// <summary>Median reaction above this (ms, plus the latency margin) → timingMultiplier = SlowTimingMultiplier.</summary>
        public double ReactionSlowMs = 450;
        /// <summary>Median reaction above this (ms, plus the latency margin) → timingMultiplier = VerySlowTimingMultiplier and QTEs skippable.</summary>
        public double ReactionVerySlowMs = 700;
        /// <summary>Longest comfortable hold below this (seconds) → holdToToggle.</summary>
        public double HoldMinSeconds = 2.0;
        /// <summary>Presses per second below this → mashingMode = HoldInstead.</summary>
        public double TapMinPerSecond = 4.0;
        /// <summary>
        /// Spread (2D standard deviation around the player's own average position, as a fraction of
        /// the circle radius) above this → bigger deadzone + aim assist. Hypothesis: 0.15 means the
        /// cursor typically wanders 15% of the radius — clearly visible jitter, well beyond the
        /// small corrections of a steady hand.
        /// </summary>
        public double StabilityMaxSpread = 0.15;

        public DeviceThresholds Clone() => (DeviceThresholds)MemberwiseClone();
    }

    /// <summary>
    /// All the knobs of the rules engine, editable by the game developer.
    /// Rules are deterministic: same Measurements + same RulesConfig → same profile.
    /// </summary>
    [Serializable]
    public sealed class RulesConfig
    {
        /// <summary>
        /// The screen's (and input chain's) latency is unknown, so measured reaction times are a bit
        /// slower than the player really is. Reaction rules only fire when the median is above
        /// threshold + this margin, so display lag alone doesn't trigger them. See README "Limits".
        /// </summary>
        public double ReactionLatencyMarginMs = 50;

        public float SlowTimingMultiplier = 1.5f;
        public float VerySlowTimingMultiplier = 2.0f;

        /// <summary>Deadzone used when no stability rule fires.</summary>
        public float DefaultStickDeadzone = 0.125f;
        /// <summary>Deadzone proposed when stability is above the threshold.</summary>
        public float IncreasedStickDeadzone = 0.25f;

        // Per-device thresholds. Gamepad values come straight from the project spec;
        // the others are reasoned guesses, documented next to each value.

        /// <summary>Gamepad: the reference values from the spec.</summary>
        public DeviceThresholds Gamepad = new DeviceThresholds();

        /// <summary>
        /// Keyboard: same reaction/hold thresholds as gamepad (a key press is comparable to a button
        /// press). Tapping a key is usually a little easier than a face button, so 4.5 presses/s.
        /// No stick → stability is normally skipped; value kept for completeness.
        /// </summary>
        public DeviceThresholds Keyboard = new DeviceThresholds
        {
            TapMinPerSecond = 4.5,
        };

        /// <summary>
        /// Mouse: clicking is quick, so reaction/hold/tap match keyboard. A resting hand on a mouse is
        /// steadier than a thumb holding a stick at partial deflection, so a lower spread (0.10)
        /// already counts as "unsteady".
        /// </summary>
        public DeviceThresholds Mouse = new DeviceThresholds
        {
            TapMinPerSecond = 4.5,
            StabilityMaxSpread = 0.10,
        };

        /// <summary>
        /// Touch: touchscreens add extra latency, so reaction thresholds are 50 ms higher. Tapping a
        /// glass surface has no key travel feedback, so 3.5 presses/s. No stick/mouse → stability
        /// is skipped by the calibration; value kept for completeness.
        /// </summary>
        public DeviceThresholds Touch = new DeviceThresholds
        {
            ReactionSlowMs = 500,
            ReactionVerySlowMs = 750,
            TapMinPerSecond = 3.5,
        };

        public DeviceThresholds For(InputDeviceKind device)
        {
            switch (device)
            {
                case InputDeviceKind.Keyboard: return Keyboard;
                case InputDeviceKind.Mouse: return Mouse;
                case InputDeviceKind.Touch: return Touch;
                default: return Gamepad;
            }
        }
    }
}

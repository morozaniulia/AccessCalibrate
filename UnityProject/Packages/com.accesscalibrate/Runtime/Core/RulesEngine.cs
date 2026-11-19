// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AccessCalibrate.Core
{
    /// <summary>
    /// One proposed setting together with WHY it was proposed — the confirmation screen shows this
    /// to the player (e.g. "reaction median 620 ms → timing windows ×1.5").
    /// </summary>
    public sealed class ProposedChange
    {
        /// <summary>Name of the <see cref="AccessibilityProfile"/> field, e.g. "timingMultiplier".</summary>
        public string Setting;
        /// <summary>The proposed value, formatted for display, e.g. "1.5" or "Skippable".</summary>
        public string ProposedValue;
        /// <summary>Which measurement triggered it, e.g. "reaction median".</summary>
        public string Measurement;
        public double MeasuredValue;
        /// <summary>Unit of <see cref="MeasuredValue"/> and <see cref="Threshold"/>, e.g. "ms", "s", "presses/s".</summary>
        public string Unit;
        /// <summary>The value the measurement was compared against (margin already included).</summary>
        public double Threshold;
        /// <summary>"&gt;", "&lt;" or "≥".</summary>
        public string Comparison;
        /// <summary>The measurement had too few valid trials; the player should double-check this one.</summary>
        public bool IsUncertain;

        /// <summary>Plain technical summary, e.g. "reaction median 620 ms > 500 ms → timingMultiplier = 1.5".</summary>
        public string Summary =>
            $"{Measurement} {Format(MeasuredValue)} {Unit} {Comparison} {Format(Threshold)} {Unit} → {Setting} = {ProposedValue}"
            + (IsUncertain ? " (uncertain)" : "");

        internal static string Format(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        public override string ToString() => Summary;
    }

    /// <summary>What the rules engine returns: the proposed profile and the reason for every change.</summary>
    public sealed class RulesResult
    {
        public AccessibilityProfile Profile;
        public List<ProposedChange> Changes = new List<ProposedChange>();

        public bool Proposes(string setting) => Changes.Any(c => c.Setting == setting);
    }

    /// <summary>
    /// Measurements + RulesConfig → proposed AccessibilityProfile.
    ///
    /// A pure function: deterministic, no hidden state, no data from other players, no
    /// "fit score". Skipped measurements never change anything. The player confirms or edits
    /// every proposal afterwards.
    /// </summary>
    public static class RulesEngine
    {
        public static RulesResult Evaluate(Measurements m, RulesConfig config)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var profile = new AccessibilityProfile { stickDeadzone = config.DefaultStickDeadzone };
            var result = new RulesResult { Profile = profile };

            ApplyReaction(m.Reaction, config, result);
            ApplyHold(m.Hold, config, result);
            ApplyTaps(m.Taps, config, result);
            ApplyStability(m.Stability, config, result);
            ApplyButtons(m.Buttons, result);

            return result;
        }

        static void ApplyReaction(ReactionMeasurement r, RulesConfig config, RulesResult result)
        {
            if (r == null || !r.IsUsable) return;
            var t = config.For(r.Device);
            double verySlow = t.ReactionVerySlowMs + config.ReactionLatencyMarginMs;
            double slow = t.ReactionSlowMs + config.ReactionLatencyMarginMs;

            if (r.MedianMs > verySlow)
            {
                result.Profile.timingMultiplier = config.VerySlowTimingMultiplier;
                result.Profile.qteMode = QteMode.Skippable;
                Add(result, r, "timingMultiplier", ProposedChange.Format(config.VerySlowTimingMultiplier),
                    "reaction median", r.MedianMs, "ms", verySlow, ">");
                Add(result, r, "qteMode", nameof(QteMode.Skippable),
                    "reaction median", r.MedianMs, "ms", verySlow, ">");
            }
            else if (r.MedianMs > slow)
            {
                result.Profile.timingMultiplier = config.SlowTimingMultiplier;
                Add(result, r, "timingMultiplier", ProposedChange.Format(config.SlowTimingMultiplier),
                    "reaction median", r.MedianMs, "ms", slow, ">");
            }
        }

        static void ApplyHold(HoldMeasurement h, RulesConfig config, RulesResult result)
        {
            if (h == null || !h.IsUsable) return;
            var t = config.For(h.Device);
            if (h.MaxHoldSeconds < t.HoldMinSeconds)
            {
                result.Profile.holdToToggle = true;
                Add(result, h, "holdToToggle", "true",
                    "longest comfortable hold", h.MaxHoldSeconds, "s", t.HoldMinSeconds, "<");
            }
        }

        static void ApplyTaps(TapMeasurement tap, RulesConfig config, RulesResult result)
        {
            if (tap == null || !tap.IsUsable) return;
            var t = config.For(tap.Device);
            if (tap.PressesPerSecond < t.TapMinPerSecond)
            {
                result.Profile.mashingMode = MashingMode.HoldInstead;
                Add(result, tap, "mashingMode", nameof(MashingMode.HoldInstead),
                    "repeated presses", tap.PressesPerSecond, "presses/s", t.TapMinPerSecond, "<");
            }
        }

        static void ApplyStability(StabilityMeasurement s, RulesConfig config, RulesResult result)
        {
            if (s == null || !s.IsUsable) return;
            var t = config.For(s.Device);
            if (s.RmsDeviation > t.StabilityMaxRms)
            {
                result.Profile.stickDeadzone = config.IncreasedStickDeadzone;
                result.Profile.aimAssist = AimAssist.Medium;
                Add(result, s, "stickDeadzone", ProposedChange.Format(config.IncreasedStickDeadzone),
                    "stability RMS", s.RmsDeviation, "× radius", t.StabilityMaxRms, ">");
                Add(result, s, "aimAssist", nameof(AimAssist.Medium),
                    "stability RMS", s.RmsDeviation, "× radius", t.StabilityMaxRms, ">");
            }
        }

        static void ApplyButtons(ButtonAccessMeasurement b, RulesResult result)
        {
            if (b == null || !b.IsUsable) return;
            int missed = b.UnpressedButtons.Count;
            int total = b.RequestedButtons.Count;
            if (missed >= 1)
            {
                result.Profile.remapSuggestions = new List<string>(b.UnpressedButtons);
                Add(result, b, "remapSuggestions", string.Join(", ", b.UnpressedButtons),
                    "buttons not pressed", missed, "buttons", 1, "≥");
            }
            if (total > 0 && missed * 2 >= total)
            {
                result.Profile.suggestOneHandedLayout = true;
                Add(result, b, "suggestOneHandedLayout", "true",
                    "buttons not pressed", missed, "buttons", total / 2.0, "≥");
            }
        }

        static void Add(RulesResult result, MeasurementBase source, string setting, string value,
                        string measurement, double measured, string unit, double threshold, string comparison)
        {
            result.Changes.Add(new ProposedChange
            {
                Setting = setting,
                ProposedValue = value,
                Measurement = measurement,
                MeasuredValue = measured,
                Unit = unit,
                Threshold = threshold,
                Comparison = comparison,
                IsUncertain = source.Confidence == Confidence.Uncertain,
            });
        }
    }
}

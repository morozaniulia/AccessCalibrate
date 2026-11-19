// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Globalization;
using AccessCalibration.Core;

namespace AccessCalibration
{
    /// <summary>Turns a <see cref="ProposedChange"/> into player-facing text (from <see cref="CalibrationTexts"/>).</summary>
    public static class ProposalTexts
    {
        /// <summary>What the setting does, in plain words, e.g. "More time for quick moments".</summary>
        public static string Title(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "timingMultiplier": return CalibrationTexts.SettingTimingMultiplier;
                case "qteMode": return CalibrationTexts.SettingQteSkippable;
                case "holdToToggle": return CalibrationTexts.SettingHoldToToggle;
                case "mashingMode": return CalibrationTexts.SettingMashingHoldInstead;
                case "stickDeadzone": return CalibrationTexts.SettingStickDeadzone;
                case "aimAssist": return CalibrationTexts.SettingAimAssist;
                case "remapSuggestions": return CalibrationTexts.SettingRemap;
                case "suggestOneHandedLayout": return CalibrationTexts.SettingOneHanded;
                default: return c.Setting;
            }
        }

        /// <summary>
        /// Why it was suggested: "Median reaction 620 ms · rule: above 500 ms → timing windows ×1.5".
        /// Uncertain: "Longest hold 1.4 s · not measured reliably, so it starts off".
        /// </summary>
        public static string Reason(ProposedChange c)
        {
            string measured = Measurement(c);
            if (c.IsUncertain) return measured + " · " + CalibrationTexts.ConfirmUncertain;

            string rule = Rule(c);
            string effect = Effect(c);
            string text = measured;
            if (!string.IsNullOrEmpty(rule)) text += " · " + rule;
            if (!string.IsNullOrEmpty(effect)) text += " → " + effect;
            return text;
        }

        static string Measurement(ProposedChange c)
        {
            switch (c.Source)
            {
                case MeasurementKind.Reaction: return F(CalibrationTexts.MeasureReaction, c.MeasuredValue);
                case MeasurementKind.Hold: return F(CalibrationTexts.MeasureHold, c.MeasuredValue);
                case MeasurementKind.Taps: return F(CalibrationTexts.MeasureTaps, c.MeasuredValue);
                case MeasurementKind.Stability: return F(CalibrationTexts.MeasureStability, c.MeasuredValue);
                case MeasurementKind.Buttons:
                    return c.Setting == "suggestOneHandedLayout"
                        ? CalibrationTexts.MeasureButtonsHalf
                        : F(CalibrationTexts.MeasureButtonsMissed, c.MeasuredValue, c.Total);
                default: return c.Summary;
            }
        }

        static string Rule(ProposedChange c)
        {
            switch (c.Source)
            {
                case MeasurementKind.Reaction: return F(CalibrationTexts.RuleAboveMs, c.Threshold);
                case MeasurementKind.Hold: return F(CalibrationTexts.RuleBelowSeconds, c.Threshold);
                case MeasurementKind.Taps: return F(CalibrationTexts.RuleBelowPerSecond, c.Threshold);
                case MeasurementKind.Stability: return F(CalibrationTexts.RuleAbove, c.Threshold);
                default: return null;   // buttons: the measurement already says it
            }
        }

        /// <summary>Only where the value matters; on/off settings are fully described by their title.</summary>
        static string Effect(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "timingMultiplier": return F(CalibrationTexts.EffectTiming, c.ProposedValue);
                case "stickDeadzone": return F(CalibrationTexts.EffectDeadzone, c.ProposedValue);
                case "aimAssist": return F(CalibrationTexts.EffectAimAssist, c.ProposedValue);
                case "remapSuggestions": return F(CalibrationTexts.EffectRemap, c.ProposedValue);
                default: return null;
            }
        }

        static string F(string pattern, params object[] args) => string.Format(CultureInfo.InvariantCulture, pattern, args);
    }
}

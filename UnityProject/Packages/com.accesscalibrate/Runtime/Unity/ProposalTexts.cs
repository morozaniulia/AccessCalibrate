// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Globalization;
using AccessCalibration.Core;

namespace AccessCalibration
{
    /// <summary>Turns a <see cref="ProposedChange"/> into player-facing text (from <see cref="CalibrationTexts"/>).</summary>
    public static class ProposalTexts
    {
        /// <summary>What the setting does, in plain words, e.g. "More time for quick moments (timing windows ×1.5)".</summary>
        public static string Title(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "timingMultiplier": return Format(CalibrationTexts.SettingTimingMultiplier, c.ProposedValue);
                case "qteMode": return CalibrationTexts.SettingQteSkippable;
                case "holdToToggle": return CalibrationTexts.SettingHoldToToggle;
                case "mashingMode": return CalibrationTexts.SettingMashingHoldInstead;
                case "stickDeadzone": return Format(CalibrationTexts.SettingStickDeadzone, c.ProposedValue);
                case "aimAssist": return Format(CalibrationTexts.SettingAimAssist, c.ProposedValue);
                case "remapSuggestions": return Format(CalibrationTexts.SettingRemap, c.ProposedValue);
                case "suggestOneHandedLayout": return CalibrationTexts.SettingOneHanded;
                default: return c.Setting + " = " + c.ProposedValue;
            }
        }

        /// <summary>Why it was suggested, e.g. "Median reaction time 620 ms (rule: above 500 ms)".</summary>
        public static string Reason(ProposedChange c)
        {
            switch (c.Source)
            {
                case MeasurementKind.Reaction: return Format(CalibrationTexts.ReasonReaction, c.MeasuredValue, c.Threshold);
                case MeasurementKind.Hold: return Format(CalibrationTexts.ReasonHold, c.MeasuredValue, c.Threshold);
                case MeasurementKind.Taps: return Format(CalibrationTexts.ReasonTaps, c.MeasuredValue, c.Threshold);
                case MeasurementKind.Stability: return Format(CalibrationTexts.ReasonStability, c.MeasuredValue, c.Threshold);
                case MeasurementKind.Buttons:
                    return c.Setting == "suggestOneHandedLayout"
                        ? Format(CalibrationTexts.ReasonButtonsHalf, c.MeasuredValue, c.Threshold * 2)  // threshold = half of the buttons
                        : Format(CalibrationTexts.ReasonButtonsMissed, c.MeasuredValue);
                default: return c.Summary;
            }
        }

        static string Format(string pattern, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, pattern, args);
    }
}

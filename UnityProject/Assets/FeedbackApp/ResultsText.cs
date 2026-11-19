// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AccessCalibration.Core;

namespace AccessCalibration.FeedbackApp
{
    /// <summary>
    /// The plain-text block "Copy results" puts on the clipboard, and the lines the summary screen
    /// shows. Only what the calibration measured and suggested: no names, no machine or browser
    /// details beyond the platform, nothing sent anywhere.
    /// </summary>
    public static class ResultsText
    {
        public const string Version = "0.1.0";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public struct Line
        {
            public string Name;
            public string Value;     // "650 ms median, 5 trials" or "skipped"
            public string Device;    // "keyboard", or "" when skipped
        }

        /// <summary>
        /// The five measurements, in calibration order. <paramref name="buttonNames"/>: list the
        /// buttons not reached (the copied text) or only count them (the summary screen).
        /// </summary>
        public static List<Line> Measurements(Measurements m, bool buttonNames = true)
        {
            return new List<Line>
            {
                Make("Reaction", m.Reaction, () => string.Format(Inv, "{0:0} ms median, {1} trials", m.Reaction.MedianMs, m.Reaction.ValidTrialCount)),
                Make("Hold", m.Hold, () => string.Format(Inv, "{0:0.0} s longest", m.Hold.MaxHoldSeconds)),
                Make("Repeated presses", m.Taps, () => string.Format(Inv, "{0:0.0} presses / s", m.Taps.PressesPerSecond)),
                Make("Steadiness", m.Stability, () => string.Format(Inv, "spread {0:0.00} of the circle", m.Stability.Spread)),
                Make("Button reach", m.Buttons, () =>
                {
                    var b = m.Buttons;
                    string text = string.Format(Inv, "{0} of {1} not reached", b.UnpressedButtons.Count, b.RequestedButtons.Count);
                    return buttonNames && b.UnpressedButtons.Count > 0 ? text + ": " + string.Join(", ", b.UnpressedButtons) : text;
                }),
            };
        }

        static Line Make(string name, MeasurementBase m, System.Func<string> value)
        {
            if (m == null || m.Confidence == Confidence.Skipped)
                return new Line { Name = name, Value = "skipped", Device = "" };
            string v = value();
            if (m.Confidence == Confidence.Uncertain) v += ", uncertain";
            return new Line { Name = name, Value = v, Device = DeviceName(m.Device) };
        }

        public static string DeviceName(InputDeviceKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>"keyboard, mouse": the devices the non-skipped checks were done with.</summary>
        public static string Devices(Measurements m)
        {
            var used = new MeasurementBase[] { m.Reaction, m.Hold, m.Taps, m.Stability, m.Buttons }
                .Where(x => x != null && x.Confidence != Confidence.Skipped)
                .Select(x => DeviceName(x.Device))
                .Distinct()
                .ToList();
            return used.Count > 0 ? string.Join(", ", used) : "none";
        }

        /// <summary>
        /// The clipboard block. <paramref name="review"/> may be null (calibration closed on the
        /// Welcome screen); <paramref name="applied"/> is false after "Not now".
        /// </summary>
        public static string Build(Measurements m, ProposalReview review, bool applied, string platform)
        {
            m = m ?? new Measurements();
            var sb = new StringBuilder();
            sb.Append("AccessCalibrate ").Append(Version).Append(" results\n");
            sb.Append("Platform: ").Append(platform).Append('\n');
            sb.Append("Devices: ").Append(Devices(m)).Append('\n');
            sb.Append('\n');
            sb.Append("Measurements\n");
            foreach (var l in Measurements(m))
            {
                sb.Append("- ").Append(l.Name).Append(": ").Append(l.Value);
                if (l.Device.Length > 0) sb.Append(" (").Append(l.Device).Append(')');
                sb.Append('\n');
            }
            sb.Append('\n');
            int count = review?.Items.Count ?? 0;
            sb.Append("Suggestions");
            sb.Append(count == 0 ? ": none" : applied ? " (applied)" : " (not applied: Not now)");
            sb.Append('\n');
            if (review != null)
            {
                foreach (var item in review.Items)
                {
                    var c = item.Change;
                    sb.Append(item.Accepted ? "- [on]  " : "- [off] ")
                      .Append(ProposalTexts.Title(c))
                      .Append(" (").Append(c.Setting).Append(" = ").Append(c.ProposedValue).Append(')');
                    if (c.IsUncertain) sb.Append(", uncertain");
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }
    }
}

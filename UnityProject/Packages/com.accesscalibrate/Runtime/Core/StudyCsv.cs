// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AccessCalibration.Core
{
    /// <summary>
    /// Study mode format: one CSV row per calibration with the raw measurements, the proposed
    /// profile and a free-text label typed by the person running the session
    /// (e.g. "normal", "non-dominant hand", "gloves"). RFC 4180 quoting, invariant culture
    /// (a dot as decimal separator on every machine), lists inside a cell joined with ';'.
    /// </summary>
    public static class StudyCsv
    {
        public static readonly string[] Columns =
        {
            "timestamp_utc", "label",
            "reaction_device", "reaction_confidence", "reaction_median_ms", "reaction_valid_trials", "reaction_raw_ms",
            "hold_device", "hold_confidence", "hold_max_s", "hold_raw_s",
            "taps_device", "taps_confidence", "taps_count", "taps_duration_s", "taps_per_s",
            "stability_device", "stability_confidence", "stability_spread", "stability_mean_offset",
            "buttons_device", "buttons_confidence", "buttons_requested", "buttons_unpressed",
            "proposed_timingMultiplier", "proposed_qteMode", "proposed_holdToToggle", "proposed_mashingMode",
            "proposed_stickDeadzone", "proposed_aimAssist", "proposed_remapSuggestions",
            "proposed_suggestOneHandedLayout", "proposed_mouseSmoothing", "profile_version",
        };

        public static string Header => string.Join(",", Columns);

        /// <summary>One CSV line (no line break). Skipped measurements leave their values empty.</summary>
        public static string Row(DateTime utc, string label, Measurements m, AccessibilityProfile proposed)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (proposed == null) throw new ArgumentNullException(nameof(proposed));

            var cells = new List<string>
            {
                utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                // Line breaks in a label become spaces, so every row stays one physical line.
                (label ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim(),
            };

            var r = m.Reaction;
            bool ok = r.IsUsable;
            cells.AddRange(new[]
            {
                r.Device.ToString(), r.Confidence.ToString(),
                ok ? Num(r.MedianMs) : "", ok ? r.ValidTrialCount.ToString(CultureInfo.InvariantCulture) : "",
                List(r.RawTrialsMs),
            });

            var h = m.Hold;
            ok = h.IsUsable;
            cells.AddRange(new[] { h.Device.ToString(), h.Confidence.ToString(), ok ? Num(h.MaxHoldSeconds) : "", List(h.RawHoldsSeconds) });

            var t = m.Taps;
            ok = t.IsUsable;
            cells.AddRange(new[]
            {
                t.Device.ToString(), t.Confidence.ToString(),
                ok ? t.PressCount.ToString(CultureInfo.InvariantCulture) : "", ok ? Num(t.DurationSeconds) : "",
                ok ? Num(t.PressesPerSecond) : "",
            });

            var s = m.Stability;
            ok = s.IsUsable;
            cells.AddRange(new[] { s.Device.ToString(), s.Confidence.ToString(), ok ? Num(s.Spread) : "", ok ? Num(s.MeanOffset) : "" });

            var b = m.Buttons;
            ok = b.IsUsable;
            cells.AddRange(new[]
            {
                b.Device.ToString(), b.Confidence.ToString(),
                string.Join(";", b.RequestedButtons), ok ? string.Join(";", b.UnpressedButtons) : "",
            });

            cells.AddRange(new[]
            {
                Num(proposed.timingMultiplier), proposed.qteMode.ToString(), Bool(proposed.holdToToggle),
                proposed.mashingMode.ToString(), Num(proposed.stickDeadzone), proposed.aimAssist.ToString(),
                string.Join(";", proposed.remapSuggestions), Bool(proposed.suggestOneHandedLayout), Bool(proposed.mouseSmoothing),
                proposed.version.ToString(CultureInfo.InvariantCulture),
            });

            return string.Join(",", cells.Select(Quote));
        }

        static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        static string Bool(bool v) => v ? "true" : "false";
        static string List(IEnumerable<double> values) => string.Join(";", values.Select(Num));

        /// <summary>RFC 4180: quote a cell that contains a comma, quote, or line break; double inner quotes.</summary>
        public static string Quote(string cell)
        {
            if (cell == null) return "";
            bool needs = cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            return needs ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
        }

        /// <summary>Parses one CSV line (RFC 4180, no embedded line breaks needed by readers of this file).</summary>
        public static List<string> ParseLine(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            cells.Add(cell.ToString());
            return cells;
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AccessCalibrate.Core
{
    public enum QteMode { Normal, Skippable, Off }
    public enum MashingMode { Normal, HoldInstead, Auto }
    public enum AimAssist { Off, Low, Medium, High }

    /// <summary>
    /// The final settings the game reads. The player can change every field;
    /// the calibration only proposes values.
    /// </summary>
    [Serializable]
    public sealed class AccessibilityProfile
    {
        public const int CurrentVersion = 1;

        public float timingMultiplier = 1f;
        public QteMode qteMode = QteMode.Normal;
        public bool holdToToggle = false;
        public MashingMode mashingMode = MashingMode.Normal;
        public float stickDeadzone = 0.125f;
        public AimAssist aimAssist = AimAssist.Off;
        public List<string> remapSuggestions = new List<string>();
        public bool suggestOneHandedLayout = false;
        public int version = CurrentVersion;

        public AccessibilityProfile Clone()
        {
            var copy = (AccessibilityProfile)MemberwiseClone();
            copy.remapSuggestions = new List<string>(remapSuggestions);
            return copy;
        }

        public override bool Equals(object obj)
        {
            return obj is AccessibilityProfile o
                && timingMultiplier.Equals(o.timingMultiplier)
                && qteMode == o.qteMode
                && holdToToggle == o.holdToToggle
                && mashingMode == o.mashingMode
                && stickDeadzone.Equals(o.stickDeadzone)
                && aimAssist == o.aimAssist
                && remapSuggestions.SequenceEqual(o.remapSuggestions)
                && suggestOneHandedLayout == o.suggestOneHandedLayout
                && version == o.version;
        }

        public override int GetHashCode() => ToJson().GetHashCode();

        // ---------------------------------------------------------------- JSON
        // Hand-written so Core stays free of UnityEngine (JsonUtility) and third-party libraries.

        public string ToJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(version.ToString(inv)).Append(",\n");
            sb.Append("  \"timingMultiplier\": ").Append(timingMultiplier.ToString("R", inv)).Append(",\n");
            sb.Append("  \"qteMode\": \"").Append(qteMode).Append("\",\n");
            sb.Append("  \"holdToToggle\": ").Append(holdToToggle ? "true" : "false").Append(",\n");
            sb.Append("  \"mashingMode\": \"").Append(mashingMode).Append("\",\n");
            sb.Append("  \"stickDeadzone\": ").Append(stickDeadzone.ToString("R", inv)).Append(",\n");
            sb.Append("  \"aimAssist\": \"").Append(aimAssist).Append("\",\n");
            sb.Append("  \"remapSuggestions\": [")
              .Append(string.Join(", ", remapSuggestions.Select(MiniJson.Quote)))
              .Append("],\n");
            sb.Append("  \"suggestOneHandedLayout\": ").Append(suggestOneHandedLayout ? "true" : "false").Append("\n");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Reads a profile from JSON. Missing fields keep their defaults, unknown fields are ignored.
        /// Throws <see cref="FormatException"/> on malformed JSON.
        /// </summary>
        public static AccessibilityProfile FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> obj))
                throw new FormatException("Profile JSON must be an object.");

            var p = new AccessibilityProfile();
            if (obj.TryGetValue("version", out var v) && v is double ver) p.version = (int)ver;
            if (obj.TryGetValue("timingMultiplier", out v) && v is double tm) p.timingMultiplier = (float)tm;
            if (obj.TryGetValue("qteMode", out v) && v is string qm) p.qteMode = ParseEnum(qm, p.qteMode);
            if (obj.TryGetValue("holdToToggle", out v) && v is bool ht) p.holdToToggle = ht;
            if (obj.TryGetValue("mashingMode", out v) && v is string mm) p.mashingMode = ParseEnum(mm, p.mashingMode);
            if (obj.TryGetValue("stickDeadzone", out v) && v is double dz) p.stickDeadzone = (float)dz;
            if (obj.TryGetValue("aimAssist", out v) && v is string aa) p.aimAssist = ParseEnum(aa, p.aimAssist);
            if (obj.TryGetValue("remapSuggestions", out v) && v is List<object> list)
                p.remapSuggestions = list.OfType<string>().ToList();
            if (obj.TryGetValue("suggestOneHandedLayout", out v) && v is bool oh) p.suggestOneHandedLayout = oh;
            return p;
        }

        static T ParseEnum<T>(string s, T fallback) where T : struct
        {
            return Enum.TryParse(s, ignoreCase: true, out T result) && Enum.IsDefined(typeof(T), result)
                ? result
                : fallback;
        }
    }
}

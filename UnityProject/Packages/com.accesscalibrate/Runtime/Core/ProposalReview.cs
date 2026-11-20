// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessCalibration.Core
{
    /// <summary>
    /// The player's review of the proposals: one on/off switch per proposed setting.
    ///
    /// Proposals based on a reliable measurement start ON; proposals marked uncertain start OFF
    /// (the player opts in). Nothing is applied until <see cref="BuildProfile"/> is called with
    /// the player's choices — the player always has the final say.
    /// </summary>
    public sealed class ProposalReview
    {
        public sealed class Item
        {
            public ProposedChange Change;
            public bool Accepted;
            /// <summary>The value that will be applied: the proposed one, unless the player adjusted it.</summary>
            public string Value;

            /// <summary>Settings with a value the player can adjust (timing windows, aim assist).</summary>
            public bool IsAdjustable => Steps(Change.Setting) != null;

            /// <summary>Move the value one step (−1 / +1) along <see cref="Steps"/>. False at either end.</summary>
            public bool Adjust(int delta)
            {
                var steps = Steps(Change.Setting);
                if (steps == null) return false;
                int i = Array.IndexOf(steps, Value);
                if (i < 0) i = Array.IndexOf(steps, Change.ProposedValue);
                int j = Math.Max(0, Math.Min(steps.Length - 1, i + Math.Sign(delta)));
                if (j == i) return false;
                Value = steps[j];
                return true;
            }
        }

        /// <summary>The values a setting can be adjusted to, in order (null: not adjustable).</summary>
        public static string[] Steps(string setting)
        {
            switch (setting)
            {
                case "timingMultiplier": return new[] { "1", "1.25", "1.5", "1.75", "2", "2.25", "2.5" };
                case "aimAssist": return new[] { nameof(AimAssist.Off), nameof(AimAssist.Low), nameof(AimAssist.Medium), nameof(AimAssist.High) };
                default: return null;
            }
        }

        public RulesResult Result { get; }
        public IReadOnlyList<Item> Items { get; }

        public ProposalReview(RulesResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Items = result.Changes
                .Select(c => new Item { Change = c, Accepted = !c.IsUncertain, Value = c.ProposedValue })
                .ToList();
        }

        /// <summary>
        /// The profile to save: <paramref name="baseProfile"/> (normally the defaults) with only the
        /// accepted proposals applied. Switched-off proposals keep the base value.
        /// </summary>
        public AccessibilityProfile BuildProfile(AccessibilityProfile baseProfile)
        {
            if (baseProfile == null) throw new ArgumentNullException(nameof(baseProfile));
            var p = baseProfile.Clone();
            var proposed = Result.Profile;
            foreach (var item in Items.Where(i => i.Accepted))
            {
                switch (item.Change.Setting)
                {
                    case "timingMultiplier":
                        p.timingMultiplier = float.TryParse(item.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : proposed.timingMultiplier;
                        break;
                    case "qteMode": p.qteMode = proposed.qteMode; break;
                    case "holdToToggle": p.holdToToggle = proposed.holdToToggle; break;
                    case "mashingMode": p.mashingMode = proposed.mashingMode; break;
                    case "stickDeadzone": p.stickDeadzone = proposed.stickDeadzone; break;
                    case "aimAssist":
                        p.aimAssist = Enum.TryParse<AimAssist>(item.Value, out var aim) ? aim : proposed.aimAssist;
                        break;
                    case "remapSuggestions": p.remapSuggestions = new List<string>(proposed.remapSuggestions); break;
                    case "suggestOneHandedLayout": p.suggestOneHandedLayout = proposed.suggestOneHandedLayout; break;
                    case "mouseSmoothing": p.mouseSmoothing = proposed.mouseSmoothing; break;
                    default: throw new InvalidOperationException($"Unknown setting '{item.Change.Setting}'.");
                }
            }
            return p;
        }
    }
}

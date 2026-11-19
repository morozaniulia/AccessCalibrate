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
        }

        public RulesResult Result { get; }
        public IReadOnlyList<Item> Items { get; }

        public ProposalReview(RulesResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Items = result.Changes
                .Select(c => new Item { Change = c, Accepted = !c.IsUncertain })
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
                    case "timingMultiplier": p.timingMultiplier = proposed.timingMultiplier; break;
                    case "qteMode": p.qteMode = proposed.qteMode; break;
                    case "holdToToggle": p.holdToToggle = proposed.holdToToggle; break;
                    case "mashingMode": p.mashingMode = proposed.mashingMode; break;
                    case "stickDeadzone": p.stickDeadzone = proposed.stickDeadzone; break;
                    case "aimAssist": p.aimAssist = proposed.aimAssist; break;
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

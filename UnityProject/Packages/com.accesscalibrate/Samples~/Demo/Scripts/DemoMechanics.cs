// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using UnityEngine;

namespace AccessCalibration.Demo
{
    /// <summary>
    /// How each setting changes the demo's mechanics — the part a game developer would copy.
    /// Pure functions of the profile, so they are easy to read and to test.
    /// </summary>
    public static class DemoMechanics
    {
        /// <summary>Quick-time window without any adjustment, in seconds.</summary>
        public const float BaseQteWindowSeconds = 0.8f;
        /// <summary>How long the lever has to be held.</summary>
        public const float LeverHoldSeconds = 2.0f;
        /// <summary>Presses needed to lower the bridge when mashing.</summary>
        public const int BridgePresses = 12;
        /// <summary>When "hold instead" is on: seconds of holding to lower the bridge.</summary>
        public const float BridgeHoldSeconds = 3.0f;
        /// <summary>Upper end of the stick range, as in the Input System's stick deadzone processor.</summary>
        public const float StickDeadzoneMax = 0.925f;

        /// <summary>timingMultiplier stretches every timing window.</summary>
        public static float QteWindow(AccessibilityProfile p) => BaseQteWindowSeconds * Mathf.Max(0.1f, p.timingMultiplier);

        /// <summary>qteMode Skippable: the player may skip; Off: the game does it for them.</summary>
        public static bool QteCanBeSkipped(AccessibilityProfile p) => p.qteMode == QteMode.Skippable;
        public static bool QteIsAutomatic(AccessibilityProfile p) => p.qteMode == QteMode.Off;

        /// <summary>holdToToggle: one press starts the action, which then completes by itself.</summary>
        public static bool HoldNeedsHolding(AccessibilityProfile p) => !p.holdToToggle;

        /// <summary>mashingMode: Normal = tap repeatedly, HoldInstead = hold, Auto = fills by itself.</summary>
        public static MashingMode Mashing(AccessibilityProfile p) => p.mashingMode;

        /// <summary>
        /// Radial stick deadzone with the profile's stickDeadzone as the lower end: small movements
        /// (drift, tremor) are ignored, the rest is rescaled to 0…1 so full speed stays reachable.
        /// </summary>
        public static Vector2 ApplyDeadzone(Vector2 raw, float deadzone, float max = StickDeadzoneMax)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= deadzone || magnitude <= 0f) return Vector2.zero;
            float scaled = Mathf.Clamp01((magnitude - deadzone) / Mathf.Max(0.0001f, max - deadzone));
            return raw / magnitude * scaled;
        }

        /// <summary>One discreet line per active setting, for the on-screen panel.</summary>
        public static string[] DescribeActiveSettings(AccessibilityProfile p)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"Timing windows ×{p.timingMultiplier:0.##}",
                $"Quick-time events: {p.qteMode}",
                p.holdToToggle ? "Hold actions: press once (toggle)" : "Hold actions: hold",
                p.mashingMode == MashingMode.HoldInstead ? "Rapid presses: hold instead"
                    : p.mashingMode == MashingMode.Auto ? "Rapid presses: automatic" : "Rapid presses: tap",
                $"Stick deadzone: {p.stickDeadzone:0.###}",
                $"Aim assist: {p.aimAssist}",
            };
            if (p.remapSuggestions.Count > 0) lines.Add("Remap suggested: " + string.Join(", ", p.remapSuggestions));
            if (p.suggestOneHandedLayout) lines.Add("One-handed layout suggested");
            return lines.ToArray();
        }
    }
}

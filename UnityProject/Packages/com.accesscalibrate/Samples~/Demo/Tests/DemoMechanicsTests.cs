// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;

namespace AccessCalibration.Demo.Tests
{
    /// <summary>Each setting changes the demo's mechanics the way the confirmation screen promised.</summary>
    public class DemoMechanicsTests
    {
        [TestCase(1.0f, 0.8f)]
        [TestCase(1.5f, 1.2f)]
        [TestCase(2.0f, 1.6f)]
        public void QteWindow_IsBaseTimesTimingMultiplier(float multiplier, float expected)
        {
            var p = new AccessibilityProfile { timingMultiplier = multiplier };
            Assert.AreEqual(expected, DemoMechanics.QteWindow(p), 1e-5);
        }

        [Test]
        public void QteMode_DecidesSkippableOrAutomatic()
        {
            Assert.IsFalse(DemoMechanics.QteCanBeSkipped(new AccessibilityProfile()));
            Assert.IsTrue(DemoMechanics.QteCanBeSkipped(new AccessibilityProfile { qteMode = QteMode.Skippable }));
            Assert.IsTrue(DemoMechanics.QteIsAutomatic(new AccessibilityProfile { qteMode = QteMode.Off }));
        }

        [Test]
        public void HoldToToggle_RemovesTheNeedToHold()
        {
            Assert.IsTrue(DemoMechanics.HoldNeedsHolding(new AccessibilityProfile()));
            Assert.IsFalse(DemoMechanics.HoldNeedsHolding(new AccessibilityProfile { holdToToggle = true }));
        }

        [Test]
        public void Deadzone_IgnoresSmallMovements_AndRescalesTheRest()
        {
            Assert.AreEqual(Vector2.zero, DemoMechanics.ApplyDeadzone(new Vector2(0.2f, 0), 0.25f));
            Assert.AreEqual(Vector2.zero, DemoMechanics.ApplyDeadzone(new Vector2(0.25f, 0), 0.25f));
            // Same small push moves the character with the default deadzone…
            Assert.Greater(DemoMechanics.ApplyDeadzone(new Vector2(0.2f, 0), 0.125f).x, 0f);
            // …and full deflection still reaches full speed with a big deadzone.
            Assert.AreEqual(1f, DemoMechanics.ApplyDeadzone(new Vector2(0, 1f), 0.25f).magnitude, 1e-5);
            // Direction is kept.
            var v = DemoMechanics.ApplyDeadzone(new Vector2(0.5f, 0.5f), 0.25f);
            Assert.AreEqual(v.x, v.y, 1e-6);
        }

        [Test]
        public void ActiveSettingsPanel_ListsEverySetting_AndSuggestionsOnlyWhenPresent()
        {
            var plain = DemoMechanics.DescribeActiveSettings(new AccessibilityProfile());
            Assert.AreEqual(6, plain.Length);

            var p = new AccessibilityProfile
            {
                timingMultiplier = 1.5f, holdToToggle = true, mashingMode = MashingMode.HoldInstead,
                remapSuggestions = { "X" }, suggestOneHandedLayout = true,
            };
            var lines = DemoMechanics.DescribeActiveSettings(p);
            CollectionAssert.Contains(lines, "Timing windows ×1.5");
            CollectionAssert.Contains(lines, "Hold actions: press once (toggle)");
            CollectionAssert.Contains(lines, "Rapid presses: hold instead");
            CollectionAssert.Contains(lines, "Remap suggested: X");
            CollectionAssert.Contains(lines, "One-handed layout suggested");
        }
    }
}

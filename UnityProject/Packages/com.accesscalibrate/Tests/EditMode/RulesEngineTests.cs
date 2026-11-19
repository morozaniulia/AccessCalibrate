// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AccessCalibrate.Core.Tests
{
    public class RulesEngineTests
    {
        RulesConfig _config;

        [SetUp]
        public void SetUp()
        {
            // Margin 0 so the thresholds below match the spec table exactly;
            // the margin itself has its own test.
            _config = new RulesConfig { ReactionLatencyMarginMs = 0 };
        }

        static Measurements AllSkipped() => new Measurements();

        static ReactionMeasurement Reaction(double medianMs, InputDeviceKind device = InputDeviceKind.Gamepad,
                                            Confidence confidence = Confidence.Ok)
            => new ReactionMeasurement { MedianMs = medianMs, Device = device, Confidence = confidence };

        // ------------------------------------------------------------ defaults

        [Test]
        public void AllSkipped_ProposesNothing_AndKeepsDefaults()
        {
            var r = RulesEngine.Evaluate(AllSkipped(), _config);

            Assert.IsEmpty(r.Changes);
            var expected = new AccessibilityProfile { stickDeadzone = _config.DefaultStickDeadzone };
            Assert.AreEqual(expected, r.Profile);
        }

        [Test]
        public void SameInput_SameOutput()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(620);
            var a = RulesEngine.Evaluate(m, _config);
            var b = RulesEngine.Evaluate(m, _config);
            Assert.AreEqual(a.Profile.ToJson(), b.Profile.ToJson());
            CollectionAssert.AreEqual(a.Changes.Select(c => c.Summary), b.Changes.Select(c => c.Summary));
        }

        // ------------------------------------------------------------ reaction

        [TestCase(450, 1.0f)]   // exactly at threshold → no change ("> 450")
        [TestCase(451, 1.5f)]
        [TestCase(700, 1.5f)]   // exactly at the higher threshold → still 1.5
        [TestCase(701, 2.0f)]
        public void Reaction_SetsTimingMultiplier(double medianMs, float expected)
        {
            var m = AllSkipped();
            m.Reaction = Reaction(medianMs);
            Assert.AreEqual(expected, RulesEngine.Evaluate(m, _config).Profile.timingMultiplier);
        }

        [Test]
        public void Reaction_Above700_AlsoMakesQtesSkippable()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(800);
            var r = RulesEngine.Evaluate(m, _config);
            Assert.AreEqual(QteMode.Skippable, r.Profile.qteMode);
            Assert.IsTrue(r.Proposes("qteMode"));
        }

        [Test]
        public void Reaction_Between450And700_LeavesQtesNormal()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(600);
            Assert.AreEqual(QteMode.Normal, RulesEngine.Evaluate(m, _config).Profile.qteMode);
        }

        [Test]
        public void Reaction_LatencyMargin_RaisesTheThreshold()
        {
            _config.ReactionLatencyMarginMs = 50;
            var m = AllSkipped();

            m.Reaction = Reaction(480);   // > 450 but not > 500
            Assert.AreEqual(1.0f, RulesEngine.Evaluate(m, _config).Profile.timingMultiplier);

            m.Reaction = Reaction(510);   // > 500
            var r = RulesEngine.Evaluate(m, _config);
            Assert.AreEqual(1.5f, r.Profile.timingMultiplier);
            Assert.AreEqual(500, r.Changes.Single(c => c.Setting == "timingMultiplier").Threshold);
        }

        [Test]
        public void Reaction_Skipped_IsIgnoredEvenIfValueIsHigh()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(1500, confidence: Confidence.Skipped);
            var r = RulesEngine.Evaluate(m, _config);
            Assert.AreEqual(1.0f, r.Profile.timingMultiplier);
            Assert.IsEmpty(r.Changes);
        }

        [Test]
        public void Reaction_Uncertain_StillActs_ButIsFlagged()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(600, confidence: Confidence.Uncertain);
            var change = RulesEngine.Evaluate(m, _config).Changes.Single();
            Assert.IsTrue(change.IsUncertain);
            StringAssert.Contains("(uncertain)", change.Summary);
        }

        [Test]
        public void Reaction_UsesThresholdsOfTheDeviceItWasMeasuredWith()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(480, InputDeviceKind.Touch);   // touch threshold is 500
            Assert.AreEqual(1.0f, RulesEngine.Evaluate(m, _config).Profile.timingMultiplier);

            m.Reaction = Reaction(480, InputDeviceKind.Gamepad); // gamepad threshold is 450
            Assert.AreEqual(1.5f, RulesEngine.Evaluate(m, _config).Profile.timingMultiplier);
        }

        [Test]
        public void Reaction_ReasonDescribesMeasurementThresholdAndResult()
        {
            var m = AllSkipped();
            m.Reaction = Reaction(620);
            var c = RulesEngine.Evaluate(m, _config).Changes.Single();
            Assert.AreEqual("reaction median 620 ms > 450 ms → timingMultiplier = 1.5", c.Summary);
        }

        // ------------------------------------------------------------ hold

        [TestCase(1.9, true)]
        [TestCase(2.0, false)]   // "< 2 s" → exactly 2 s is fine
        [TestCase(5.0, false)]
        public void Hold_ShortHold_ProposesHoldToToggle(double maxSeconds, bool expected)
        {
            var m = AllSkipped();
            m.Hold = new HoldMeasurement { MaxHoldSeconds = maxSeconds, Confidence = Confidence.Ok };
            Assert.AreEqual(expected, RulesEngine.Evaluate(m, _config).Profile.holdToToggle);
        }

        [Test]
        public void Hold_Skipped_IsIgnored()
        {
            var m = AllSkipped();
            m.Hold = new HoldMeasurement { MaxHoldSeconds = 0.2, Confidence = Confidence.Skipped };
            Assert.IsFalse(RulesEngine.Evaluate(m, _config).Profile.holdToToggle);
        }

        // ------------------------------------------------------------ taps

        [TestCase(19, 5.0, MashingMode.HoldInstead)]  // 3.8 / s
        [TestCase(20, 5.0, MashingMode.Normal)]       // exactly 4 / s
        [TestCase(30, 5.0, MashingMode.Normal)]
        public void Taps_SlowRate_ProposesHoldInstead(int presses, double seconds, MashingMode expected)
        {
            var m = AllSkipped();
            m.Taps = new TapMeasurement { PressCount = presses, DurationSeconds = seconds, Confidence = Confidence.Ok };
            Assert.AreEqual(expected, RulesEngine.Evaluate(m, _config).Profile.mashingMode);
        }

        [Test]
        public void Taps_Skipped_IsIgnored()
        {
            var m = AllSkipped();
            m.Taps = new TapMeasurement { PressCount = 0, DurationSeconds = 5, Confidence = Confidence.Skipped };
            Assert.AreEqual(MashingMode.Normal, RulesEngine.Evaluate(m, _config).Profile.mashingMode);
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        public void Taps_ZeroOrNegativeDuration_IsIgnoredLikeSkipped(double seconds)
        {
            // A broken measurement (no time elapsed) must not be read as "0 presses per second".
            var m = AllSkipped();
            m.Taps = new TapMeasurement { PressCount = 0, DurationSeconds = seconds, Confidence = Confidence.Ok };
            var r = RulesEngine.Evaluate(m, _config);
            Assert.AreEqual(MashingMode.Normal, r.Profile.mashingMode);
            Assert.IsEmpty(r.Changes);
        }

        // ------------------------------------------------------------ stability

        [Test]
        public void Stability_AboveThreshold_RaisesDeadzoneAndAimAssist()
        {
            var m = AllSkipped();
            m.Stability = new StabilityMeasurement { RmsDeviation = 0.2, Confidence = Confidence.Ok };
            var p = RulesEngine.Evaluate(m, _config).Profile;
            Assert.AreEqual(_config.IncreasedStickDeadzone, p.stickDeadzone);
            Assert.AreEqual(AimAssist.Medium, p.aimAssist);
        }

        [Test]
        public void Stability_AtOrBelowThreshold_KeepsDefaults()
        {
            var m = AllSkipped();
            m.Stability = new StabilityMeasurement { RmsDeviation = 0.15, Confidence = Confidence.Ok };
            var p = RulesEngine.Evaluate(m, _config).Profile;
            Assert.AreEqual(_config.DefaultStickDeadzone, p.stickDeadzone);
            Assert.AreEqual(AimAssist.Off, p.aimAssist);
        }

        [Test]
        public void Stability_MouseIsStricterThanGamepad()
        {
            var m = AllSkipped();
            m.Stability = new StabilityMeasurement { RmsDeviation = 0.12, Confidence = Confidence.Ok, Device = InputDeviceKind.Mouse };
            Assert.AreEqual(AimAssist.Medium, RulesEngine.Evaluate(m, _config).Profile.aimAssist);

            m.Stability.Device = InputDeviceKind.Gamepad;
            Assert.AreEqual(AimAssist.Off, RulesEngine.Evaluate(m, _config).Profile.aimAssist);
        }

        [Test]
        public void Stability_Skipped_IsIgnored()
        {
            var m = AllSkipped();
            m.Stability = new StabilityMeasurement { RmsDeviation = 0.9, Confidence = Confidence.Skipped };
            Assert.AreEqual(AimAssist.Off, RulesEngine.Evaluate(m, _config).Profile.aimAssist);
        }

        // ------------------------------------------------------------ buttons

        static ButtonAccessMeasurement Buttons(string[] requested, string[] unpressed) => new ButtonAccessMeasurement
        {
            RequestedButtons = requested.ToList(),
            UnpressedButtons = unpressed.ToList(),
            Confidence = Confidence.Ok,
        };

        [Test]
        public void Buttons_NoneMissed_ProposesNothing()
        {
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A", "B", "X", "Y" }, new string[0]);
            var r = RulesEngine.Evaluate(m, _config);
            Assert.IsEmpty(r.Profile.remapSuggestions);
            Assert.IsFalse(r.Profile.suggestOneHandedLayout);
        }

        [Test]
        public void Buttons_OneMissed_SuggestsRemapOnly()
        {
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A", "B", "X", "Y" }, new[] { "Y" });
            var p = RulesEngine.Evaluate(m, _config).Profile;
            CollectionAssert.AreEqual(new[] { "Y" }, p.remapSuggestions);
            Assert.IsFalse(p.suggestOneHandedLayout);
        }

        [Test]
        public void Buttons_HalfMissed_AlsoSuggestsOneHandedLayout()
        {
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A", "B", "X", "Y" }, new[] { "X", "Y" });
            var p = RulesEngine.Evaluate(m, _config).Profile;
            CollectionAssert.AreEqual(new[] { "X", "Y" }, p.remapSuggestions);
            Assert.IsTrue(p.suggestOneHandedLayout);
        }

        [Test]
        public void Buttons_OddCount_HalfRoundsUp()
        {
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A", "B", "X" }, new[] { "X" });     // 1 of 3 < half
            Assert.IsFalse(RulesEngine.Evaluate(m, _config).Profile.suggestOneHandedLayout);

            m.Buttons = Buttons(new[] { "A", "B", "X" }, new[] { "B", "X" }); // 2 of 3 ≥ half
            Assert.IsTrue(RulesEngine.Evaluate(m, _config).Profile.suggestOneHandedLayout);
        }

        [Test]
        public void Buttons_OnlyOneRequested_NeverSuggestsOneHandedLayout()
        {
            // With a single button, "half missed" says nothing about one-handed play — remap only.
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A" }, new[] { "A" });
            var p = RulesEngine.Evaluate(m, _config).Profile;
            CollectionAssert.AreEqual(new[] { "A" }, p.remapSuggestions);
            Assert.IsFalse(p.suggestOneHandedLayout);
        }

        [Test]
        public void Buttons_Skipped_IsIgnored()
        {
            var m = AllSkipped();
            m.Buttons = Buttons(new[] { "A", "B" }, new[] { "A", "B" });
            m.Buttons.Confidence = Confidence.Skipped;
            var p = RulesEngine.Evaluate(m, _config).Profile;
            Assert.IsEmpty(p.remapSuggestions);
            Assert.IsFalse(p.suggestOneHandedLayout);
        }

        // ------------------------------------------------------------ transparency

        [Test]
        public void EveryChangedSetting_HasAReason()
        {
            var m = new Measurements
            {
                Reaction = Reaction(800),
                Hold = new HoldMeasurement { MaxHoldSeconds = 1, Confidence = Confidence.Ok },
                Taps = new TapMeasurement { PressCount = 5, DurationSeconds = 5, Confidence = Confidence.Ok },
                Stability = new StabilityMeasurement { RmsDeviation = 0.5, Confidence = Confidence.Ok },
                Buttons = Buttons(new[] { "A", "B" }, new[] { "B" }),
            };
            var r = RulesEngine.Evaluate(m, _config);

            var expected = new HashSet<string>
            {
                "timingMultiplier", "qteMode", "holdToToggle", "mashingMode",
                "stickDeadzone", "aimAssist", "remapSuggestions", "suggestOneHandedLayout",
            };
            CollectionAssert.AreEquivalent(expected, r.Changes.Select(c => c.Setting));
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccessCalibration.Core.Tests
{
    public class AccessibilityProfileTests
    {
        [Test]
        public void Json_RoundTrip_KeepsEveryField()
        {
            var original = new AccessibilityProfile
            {
                timingMultiplier = 1.5f,
                qteMode = QteMode.Skippable,
                holdToToggle = true,
                mashingMode = MashingMode.Auto,
                stickDeadzone = 0.25f,
                aimAssist = AimAssist.High,
                remapSuggestions = new List<string> { "Jump", "Fire \"alt\"", "Ș\\ă" },
                suggestOneHandedLayout = true,
                mouseSmoothing = true,
            };

            var copy = AccessibilityProfile.FromJson(original.ToJson());

            Assert.AreEqual(original, copy);
        }

        [Test]
        public void Json_DefaultProfile_RoundTrips()
        {
            var p = new AccessibilityProfile();
            Assert.AreEqual(p, AccessibilityProfile.FromJson(p.ToJson()));
        }

        [Test]
        public void Json_UsesReadableEnumNames()
        {
            var json = new AccessibilityProfile { qteMode = QteMode.Off }.ToJson();
            StringAssert.Contains("\"qteMode\": \"Off\"", json);
        }

        [Test]
        public void FromJson_MissingFields_KeepDefaults_UnknownFieldsIgnored()
        {
            var p = AccessibilityProfile.FromJson("{ \"holdToToggle\": true, \"somethingNew\": [1, {\"a\": null}] }");
            Assert.IsTrue(p.holdToToggle);
            Assert.AreEqual(1f, p.timingMultiplier);
            Assert.AreEqual(QteMode.Normal, p.qteMode);
        }

        [Test]
        public void FromJson_OlderProfileWithoutMouseSmoothing_DefaultsToOff()
        {
            var p = AccessibilityProfile.FromJson("{ \"version\": 1, \"holdToToggle\": true }");
            Assert.IsFalse(p.mouseSmoothing);
            Assert.IsTrue(p.holdToToggle);
        }

        [Test]
        public void FromJson_UnknownEnumValue_FallsBackToDefault()
        {
            var p = AccessibilityProfile.FromJson("{ \"aimAssist\": \"Ultra\", \"qteMode\": \"7\" }");
            Assert.AreEqual(AimAssist.Off, p.aimAssist);
            Assert.AreEqual(QteMode.Normal, p.qteMode);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{ \"holdToToggle\": tru }")]
        [TestCase("{ \"a\": 1 ")]
        [TestCase("[1, 2]")]
        public void FromJson_Malformed_ThrowsFormatException(string json)
        {
            Assert.Throws<FormatException>(() => AccessibilityProfile.FromJson(json));
        }

        [Test]
        public void Clone_IsIndependentCopy()
        {
            var a = new AccessibilityProfile { remapSuggestions = new List<string> { "A" } };
            var b = a.Clone();
            b.remapSuggestions.Add("B");
            Assert.AreEqual(1, a.remapSuggestions.Count);
        }
    }
}

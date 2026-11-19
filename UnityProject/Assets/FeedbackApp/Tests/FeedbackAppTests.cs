// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.IO;
using System.Linq;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AccessCalibration.FeedbackApp.Tests
{
    public class FeedbackAppTests : InputTestFixture
    {
        GameObject _host;
        CalibrationRunner _runner;
        FeedbackApp _app;
        double _now = 10;

        static Measurements Sample() => new Measurements
        {
            Reaction = new ReactionMeasurement { MedianMs = 650, ValidTrialCount = 5, Confidence = Confidence.Ok, Device = InputDeviceKind.Keyboard },
            Hold = new HoldMeasurement { MaxHoldSeconds = 1.4, Confidence = Confidence.Ok, Device = InputDeviceKind.Keyboard },
            Taps = new TapMeasurement { PressCount = 4, DurationSeconds = 5, Confidence = Confidence.Ok, Device = InputDeviceKind.Keyboard },
            Stability = new StabilityMeasurement { Spread = 0.25, Confidence = Confidence.Ok, Device = InputDeviceKind.Mouse },
            Buttons = new ButtonAccessMeasurement
            {
                RequestedButtons = { "Jump", "Action", "LB", "RB" }, UnpressedButtons = { "RB" },
                Confidence = Confidence.Ok, Device = InputDeviceKind.Keyboard,
            },
        };

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _host = new GameObject("AccessCalibrate Feedback");
            _host.SetActive(false);
            _runner = _host.AddComponent<CalibrationRunner>();
            _runner.Clock = () => _now;
            _runner.ConfirmationInputDelaySeconds = 0;
            _app = _host.AddComponent<FeedbackApp>();
            _host.SetActive(true);
        }

        public override void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OpensStraightIntoTheCalibration()
        {
            yield return null;   // Start
            Assert.IsTrue(_runner.IsCalibrating);
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.IsFalse(_app.SummaryVisible);
            Assert.AreEqual("welcome", _app.State);
            _runner.WelcomeStart();
            Assert.AreEqual("intro:ReactionTest", _app.State);
        }

        [UnityTest]
        public IEnumerator Apply_ShowsTheSummary_CopyAndRedoWork()
        {
            yield return null;
            _runner.ShowProposal(Sample());
            int aim = _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == "aimAssist");
            Assert.GreaterOrEqual(aim, 0);
            _runner.SetAccepted(aim, false);                 // the player switches one off
            _runner.Accept();
            yield return null;

            Assert.IsTrue(_app.SummaryVisible);
            Assert.AreEqual("summary", _app.State);
            var root = _app.SummaryRoot;
            Assert.AreEqual(5, root.Q("fb-measured").childCount, "five measurements");
            Assert.AreEqual(_runner.LastResult.Changes.Count, root.Q<ScrollView>("fb-suggested").contentContainer.childCount);
            Assert.IsTrue(root.Query<Label>().ToList().Any(l => l.text.StartsWith("Applied ")));

            _app.CopyResults();
            Assert.AreEqual(true, _app.LastCopySucceeded);
            string text = GUIUtility.systemCopyBuffer;
            Assert.AreEqual(_app.LastResultsText, text);
            StringAssert.Contains("- Reaction: 650 ms median, 5 trials (keyboard)", text);
            StringAssert.Contains("- Steadiness: spread 0.25 of the circle (mouse)", text);
            StringAssert.Contains("- Button reach: 1 of 4 not reached: RB (keyboard)", text);
            StringAssert.Contains("Devices: keyboard, mouse", text);
            StringAssert.Contains("Suggestions (applied)", text);
            StringAssert.Contains("- [off] Aim assist (aimAssist", text);
            StringAssert.Contains("- [on]  More time for quick moments (timingMultiplier", text);
            StringAssert.DoesNotContain(System.Environment.UserName, text, "no personal data");
            StringAssert.DoesNotContain(System.Environment.MachineName, text, "no personal data");

            _app.Redo();
            Assert.IsFalse(_app.SummaryVisible);
            Assert.IsTrue(_runner.IsWelcoming, "Redo starts a fresh calibration");
        }

        [UnityTest]
        public IEnumerator NotNow_SummarySaysNothingWasApplied()
        {
            yield return null;
            _runner.ShowProposal(Sample());
            _runner.Skip();
            yield return null;
            Assert.IsTrue(_app.SummaryVisible);
            _app.CopyResults();
            StringAssert.Contains("Suggestions (not applied: Not now)", _app.LastResultsText);
        }

        [Test]
        public void ResultsText_SkippedAndUncertainChecks()
        {
            var m = new Measurements
            {
                Reaction = new ReactionMeasurement { MedianMs = 300, ValidTrialCount = 2, Confidence = Confidence.Uncertain, Device = InputDeviceKind.Gamepad },
            };
            string text = ResultsText.Build(m, null, false, "Windows");
            StringAssert.Contains("Platform: Windows", text);
            StringAssert.Contains("- Reaction: 300 ms median, 2 trials, uncertain (gamepad)", text);
            StringAssert.Contains("- Hold: skipped\n", text);
            StringAssert.Contains("Devices: gamepad", text);
            StringAssert.Contains("Suggestions: none", text);
        }
    }
}

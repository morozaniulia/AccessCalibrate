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
            Assert.AreEqual("welcome", _app.State);
            _runner.WelcomeStart();
            Assert.AreEqual("test:ReactionTest", _app.State, "no separate intro: the first check starts at once");
        }

        [UnityTest]
        public IEnumerator Apply_ShowsThePackageSummary_CopyWorks_DoneStartsOver()
        {
            yield return null;
            _runner.ShowProposal(Sample());
            int aim = _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == "aimAssist");
            _runner.SetAccepted(aim, false);                 // the player switches one off
            int timing = _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == "timingMultiplier");
            _runner.Adjust(timing, +1);                       // and adjusts one: ×1.5 → ×1.75
            _runner.Accept();
            yield return null;
            Assert.IsTrue(_runner.IsShowingSummary);
            Assert.AreEqual("summary", _app.State);
            Assert.AreEqual(1.75f, AccessCalibrate.Profile.timingMultiplier, 1e-4, "the adjusted value is what gets applied");
            var view = (UIToolkitCalibrationView)_runner.View;
            Assert.IsTrue(view.Root.Query<Label>().ToList().Any(l => l.text == "Done"), "the summary's main button says Done");

            _runner.CopyResults();
            string text = GUIUtility.systemCopyBuffer;
            Assert.AreEqual(_runner.LastResultsText, text);
            StringAssert.Contains("- Reaction: 650 ms median, 5 trials (keyboard)", text);
            StringAssert.Contains("- Steadiness: spread 0.25 of the circle (mouse)", text);
            StringAssert.Contains("- Button reach: 1 of 4 not reached: RB (keyboard)", text);
            StringAssert.Contains("Devices: keyboard, mouse", text);
            StringAssert.Contains("Suggestions (applied)", text);
            StringAssert.Contains("- [off] Aim assist (aimAssist", text);
            StringAssert.Contains("- [on]  More time for quick moments (timingMultiplier = 1.75)", text);
            StringAssert.DoesNotContain(System.Environment.UserName, text, "no personal data");
            StringAssert.DoesNotContain(System.Environment.MachineName, text, "no personal data");
            Assert.IsTrue(view.Root.Query<Label>().ToList().Any(l => l.text == ScreenTexts.CopyDone), "says it was copied");

            _runner.ContinueFromSummary();   // "Done"
            yield return null;
            Assert.IsTrue(_runner.IsWelcoming, "Done starts a fresh calibration");
        }

        [UnityTest]
        public IEnumerator NotNow_StartsOver()
        {
            yield return null;
            _runner.ShowProposal(Sample());
            _runner.Skip();
            yield return null;
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.AreEqual("welcome", _app.State);
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

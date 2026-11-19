// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    public class ReactionTestTests : CalibrationTestFixture
    {
        ReactionTest _test;

        public override void Setup()
        {
            base.Setup();
            _test = AddTest<ReactionTest>();
            _test.Random = new System.Random(42);
        }

        /// <summary>Waits for the next signal, then presses <paramref name="reactionSeconds"/> after it.</summary>
        void ReactAfter(UnityEngine.InputSystem.Controls.ButtonControl button, double reactionSeconds)
        {
            TickAt(_test, _test.NextStimulusAt);
            Assert.IsTrue(_test.StimulusVisible, "signal should be visible");
            double shownAt = _test.StimulusShownAt;

            // The game only gets round to processing the press a bit later (next frame).
            // That delay must NOT end up in the measurement: only the event time counts.
            Now = shownAt + reactionSeconds + 0.4;
            Tap(button, shownAt + reactionSeconds, shownAt + reactionSeconds + 0.1);
        }

        [Test]
        public void StimulusAt0_PressAt500ms_Measures500ms()
        {
            _test.Begin();
            for (int i = 0; i < 6; i++) ReactAfter(Keyboard.spaceKey, 0.5);

            Assert.IsTrue(_test.IsFinished);
            var m = new Measurements();
            _test.WriteResult(m);

            Assert.AreEqual(500, m.Reaction.MedianMs, 1.0);
            Assert.AreEqual(5, m.Reaction.ValidTrialCount);   // first trial dropped
            Assert.AreEqual(Confidence.Ok, m.Reaction.Confidence);
            Assert.AreEqual(InputDeviceKind.Keyboard, m.Reaction.Device);
            Assert.AreEqual(6, m.Reaction.RawTrialsMs.Count);
        }

        [Test]
        public void UsesMedian_AndRecordsGamepadAsDevice()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            _test.Begin();
            // First trial (dropped) is slow; one trial is an outlier-ish 1.5 s.
            foreach (var s in new[] { 1.2, 0.6, 0.7, 0.65, 1.5, 0.62 })
                ReactAfter(gamepad.buttonSouth, s);

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(650, m.Reaction.MedianMs, 1.0);   // median of 600, 700, 650, 1500, 620
            Assert.AreEqual(InputDeviceKind.Gamepad, m.Reaction.Device);
        }

        [Test]
        public void PressBeforeSignal_IsAnAnticipation_AndIsIgnored()
        {
            _test.Begin();
            ReactAfter(Keyboard.spaceKey, 0.5);                       // trial 1 (dropped anyway)

            // Trial 2: press while still waiting for the signal.
            double early = _test.NextStimulusAt - 0.3;
            Now = early;
            Tap(Keyboard.spaceKey, early, early + 0.1);
            Assert.IsFalse(_test.StimulusVisible);

            for (int i = 0; i < 4; i++) ReactAfter(Keyboard.spaceKey, 0.4);

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(4, m.Reaction.ValidTrialCount);
            Assert.AreEqual(400, m.Reaction.MedianMs, 1.0);
            Assert.Less(m.Reaction.RawTrialsMs[1], 0);               // kept in the raw data, negative
        }

        [Test]
        public void TooFewValidTrials_RetriesOnce_ThenUsesTheSecondRound()
        {
            _test.Begin();
            // Round 1: every press is far too fast (< 120 ms) → no valid trials.
            for (int i = 0; i < 6; i++) ReactAfter(Keyboard.spaceKey, 0.05);
            Assert.IsTrue(_test.IsRunning, "should retry once instead of finishing");

            for (int i = 0; i < 6; i++) ReactAfter(Keyboard.spaceKey, 0.3);
            Assert.IsTrue(_test.IsFinished);

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(300, m.Reaction.MedianMs, 1.0);
            Assert.AreEqual(Confidence.Ok, m.Reaction.Confidence);
            Assert.AreEqual(12, m.Reaction.RawTrialsMs.Count);
        }

        [Test]
        public void NoPressAfterSignal_TimesOut_AsInattention()
        {
            _test.Begin();
            TickAt(_test, _test.NextStimulusAt);
            double shownAt = _test.StimulusShownAt;
            TickAt(_test, shownAt + _test.TrialTimeoutSeconds + 0.1);
            Assert.IsFalse(_test.StimulusVisible);           // moved on to the next trial
        }

        [Test]
        public void Skip_MarksResultSkipped()
        {
            _test.Begin();
            ReactAfter(Keyboard.spaceKey, 0.5);
            _test.Skip();

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(Confidence.Skipped, m.Reaction.Confidence);
        }
    }
}

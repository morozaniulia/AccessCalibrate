// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>
    /// A button pressed on the intro card ("press any button to start") and still held when the
    /// test begins must not spoil the measurement. Input System button actions don't fire for a
    /// button that was already down when they were enabled, so the tests wait for a release first.
    /// </summary>
    public class HeldButtonTests : CalibrationTestFixture
    {
        CalibrationRunner _runner;

        public override void Setup()
        {
            AccessCalibrate.ResetStaticState();
            base.Setup();
            Host.SetActive(false);
            _runner = Host.AddComponent<CalibrationRunner>();
            _runner.UseBuiltInView = false;
            _runner.View = NullCalibrationView.Instance;
            _runner.Clock = () => Now;
            Host.SetActive(true);
        }

        public override void TearDown()
        {
            base.TearDown();
            AccessCalibrate.ResetStaticState();
        }

        /// <summary>Skips to the given test's intro card, then presses Space there and KEEPS IT HELD.</summary>
        void StartTestWithSpaceHeld(int testIndex)
        {
            _runner.StartCalibration();
            for (int i = 0; i < testIndex; i++) _runner.SkipCurrent();
            Now = 10.1;
            Press(Keyboard.spaceKey, time: 10.1);   // the "press any button to start" press…
            _runner.Tick();                          // …starts the test while Space is still down
            Assert.IsTrue(_runner.CurrentTest.IsRunning);
        }

        [Test]
        public void Hold_ButtonHeldFromIntro_IsNotMeasuredAsZero()
        {
            StartTestWithSpaceHeld(1);
            var hold = _runner.Hold;

            TickAt(hold, 17.0);                       // still holding the intro press for 7 s
            Release(Keyboard.spaceKey, time: 17.5);  // finally lets go
            TickAt(hold, 17.6);

            Tap(Keyboard.spaceKey, 18.0, 20.0);      // trial 1: a real 2 s hold
            TickAt(hold, 21.1);                       // pause over → trial 2
            Tap(Keyboard.spaceKey, 21.5, 22.5);      // trial 2: 1 s

            var m = new Measurements();
            hold.WriteResult(m);
            Assert.AreEqual(2, m.Hold.RawHoldsSeconds.Count, "raw: " + string.Join(", ", m.Hold.RawHoldsSeconds));
            Assert.AreEqual(2.0, m.Hold.RawHoldsSeconds[0], 1e-6);
            Assert.AreEqual(1.0, m.Hold.RawHoldsSeconds[1], 1e-6);
            Assert.AreEqual(2.0, m.Hold.MaxHoldSeconds, 1e-6);
        }

        [Test]
        public void Hold_StillHeldAfterThe5sCap_SecondTrialWaitsForARelease()
        {
            StartTestWithSpaceHeld(1);
            var hold = _runner.Hold;
            Release(Keyboard.spaceKey, time: 10.15);
            TickAt(hold, 10.2);

            Press(Keyboard.spaceKey, time: 10.5);    // trial 1: held…
            TickAt(hold, 15.6);                       // …capped at 5 s, and the player keeps holding
            TickAt(hold, 16.7);                       // pause over: trial 2 must wait for a release
            TickAt(hold, 23.0);                       // a long time later, still holding
            Release(Keyboard.spaceKey, time: 23.5);
            TickAt(hold, 23.6);
            Tap(Keyboard.spaceKey, 24.0, 25.5);      // trial 2: 1.5 s

            var m = new Measurements();
            hold.WriteResult(m);
            Assert.AreEqual(2, m.Hold.RawHoldsSeconds.Count, "raw: " + string.Join(", ", m.Hold.RawHoldsSeconds));
            Assert.AreEqual(5.0, m.Hold.RawHoldsSeconds[0], 1e-6);
            Assert.AreEqual(1.5, m.Hold.RawHoldsSeconds[1], 1e-6);
        }

        [Test]
        public void Reaction_ButtonHeldFromIntro_SignalsWaitForARelease()
        {
            StartTestWithSpaceHeld(0);
            var reaction = _runner.Reaction;
            reaction.Random = new System.Random(1);

            TickAt(reaction, 14.0);                   // holding for ~4 s: no trial may run meanwhile
            Release(Keyboard.spaceKey, time: 14.5);
            TickAt(reaction, 14.6);

            for (int i = 0; i < 6; i++)
            {
                TickAt(reaction, reaction.NextStimulusAt);
                Assert.IsTrue(reaction.StimulusVisible);
                double shown = reaction.StimulusShownAt;
                Now = shown + 0.6;
                Tap(Keyboard.spaceKey, shown + 0.4, shown + 0.5);
            }

            var m = new Measurements();
            reaction.WriteResult(m);
            Assert.AreEqual(6, m.Reaction.RawTrialsMs.Count, "raw: " + string.Join(", ", m.Reaction.RawTrialsMs));
            Assert.AreEqual(400, m.Reaction.MedianMs, 1.0);
            Assert.AreEqual(Confidence.Ok, m.Reaction.Confidence);
        }

        [Test]
        public void Taps_ButtonHeldFromIntro_CountsTheTapsAfterRelease()
        {
            StartTestWithSpaceHeld(2);
            var taps = _runner.Taps;
            Release(Keyboard.spaceKey, time: 11.0);
            TickAt(taps, 11.1);
            for (int i = 0; i < 10; i++)
            {
                double t = 11.5 + i * 0.5;
                Tap(Keyboard.spaceKey, t, t + 0.1);
            }
            TickAt(taps, 16.6);

            var m = new Measurements();
            taps.WriteResult(m);
            Assert.AreEqual(10, m.Taps.PressCount);
        }

        [Test]
        public void ButtonReach_SpaceHeldFromIntro_FirstPromptIsNotMarkedAsUnreachable()
        {
            StartTestWithSpaceHeld(4);                // first prompt is "Jump (A / Space)"
            var reach = _runner.ButtonAccess;

            TickAt(reach, 15.5);                      // 5.4 s of holding the intro press
            Release(Keyboard.spaceKey, time: 15.8);
            TickAt(reach, 15.9);

            foreach (var key in new[] { Keyboard.spaceKey, Keyboard.eKey, Keyboard.qKey, Keyboard.rKey })
            {
                Tap(key, Now + 0.2, Now + 0.3);
                TickAt(reach, Now + 0.3);
            }

            var m = new Measurements();
            reach.WriteResult(m);
            Assert.IsEmpty(m.Buttons.UnpressedButtons, "unpressed: " + string.Join(", ", m.Buttons.UnpressedButtons));
        }
    }
}

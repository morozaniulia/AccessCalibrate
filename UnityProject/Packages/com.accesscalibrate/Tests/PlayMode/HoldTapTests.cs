// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    public class HoldTestTests : CalibrationTestFixture
    {
        HoldTest _test;

        public override void Setup()
        {
            base.Setup();
            _test = AddTest<HoldTest>();
        }

        [Test]
        public void HoldDuration_IsReleaseTimeMinusPressTime_AndMaxIsCappedAt5s()
        {
            _test.Begin();

            // Trial 1: held 1.5 s (event times), even though the clock is elsewhere.
            Now = 12.0;
            Tap(Keyboard.spaceKey, 10.2, 11.7);

            TickAt(_test, 12.0);   // pause between trials…
            TickAt(_test, 13.0);   // …over: trial 2 starts

            // Trial 2: pressed and never released → stops at 5 s.
            Press(Keyboard.spaceKey, time: 13.5);
            TickAt(_test, 18.6);
            Assert.IsTrue(_test.IsFinished);
            Release(Keyboard.spaceKey, time: 19.0);   // ignored, test already done

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(2, m.Hold.RawHoldsSeconds.Count);
            Assert.AreEqual(1.5, m.Hold.RawHoldsSeconds[0], 1e-6);
            Assert.AreEqual(5.0, m.Hold.RawHoldsSeconds[1], 1e-6);
            Assert.AreEqual(5.0, m.Hold.MaxHoldSeconds, 1e-6);
            Assert.AreEqual(Confidence.Ok, m.Hold.Confidence);
        }

        [Test]
        public void ShortHolds_LeadToHoldToToggleProposal()
        {
            _test.Begin();
            Tap(Keyboard.spaceKey, 10.2, 11.4);   // 1.2 s
            TickAt(_test, 11.5);
            TickAt(_test, 12.5);
            Tap(Keyboard.spaceKey, 12.6, 14.4);   // 1.8 s

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(1.8, m.Hold.MaxHoldSeconds, 1e-6);
            Assert.IsTrue(RulesEngine.Evaluate(m, new RulesConfig()).Profile.holdToToggle);
        }

        [Test]
        public void NoPressAtAll_IsUncertain_NotAFailure()
        {
            _test.Begin();
            TickAt(_test, 10 + _test.WaitForPressSeconds);       // trial 1 times out
            TickAt(_test, Now + _test.PauseBetweenTrialsSeconds);
            TickAt(_test, Now + _test.WaitForPressSeconds);      // trial 2 times out

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.IsTrue(_test.IsFinished);
            Assert.AreEqual(Confidence.Uncertain, m.Hold.Confidence);
        }
    }

    public class TapTestTests : CalibrationTestFixture
    {
        TapTest _test;

        public override void Setup()
        {
            base.Setup();
            _test = AddTest<TapTest>();
        }

        [Test]
        public void CountsPressesByEventTime_InA5sWindowFromTheFirstPress()
        {
            _test.Begin();
            // 21 presses from 10.5 to 15.5 s (every 0.25 s), then one at 15.75 s — outside the window.
            for (int i = 0; i <= 21; i++)
            {
                double t = 10.5 + i * 0.25;
                Tap(Keyboard.spaceKey, t, t + 0.1);
            }
            TickAt(_test, 15.6);
            Assert.IsTrue(_test.IsFinished);

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(21, m.Taps.PressCount);
            Assert.AreEqual(4.2, m.Taps.PressesPerSecond, 1e-6);
            Assert.AreEqual(InputDeviceKind.Keyboard, m.Taps.Device);
        }

        [Test]
        public void SlowPressing_LeadsToHoldInsteadProposal()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            _test.Begin();
            for (int i = 0; i < 10; i++)
            {
                double t = 11 + i * 0.5;           // 2 presses / s
                Tap(gamepad.buttonSouth, t, t + 0.2);
            }
            TickAt(_test, 16.1);

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(2.0, m.Taps.PressesPerSecond, 1e-6);
            Assert.AreEqual(MashingMode.HoldInstead, RulesEngine.Evaluate(m, new RulesConfig()).Profile.mashingMode);
        }

        [Test]
        public void TimerStartsAtFirstPress_NotAtTestStart()
        {
            _test.Begin();
            TickAt(_test, 14.0);                   // 4 s of thinking: no presses yet, still waiting
            Assert.IsFalse(_test.IsCounting);
            Tap(Keyboard.spaceKey, 14.0, 14.1);
            TickAt(_test, 18.0);                   // 4 s after the first press: still counting
            Assert.IsTrue(_test.IsRunning);
            TickAt(_test, 19.0);
            Assert.IsTrue(_test.IsFinished);
        }
    }
}

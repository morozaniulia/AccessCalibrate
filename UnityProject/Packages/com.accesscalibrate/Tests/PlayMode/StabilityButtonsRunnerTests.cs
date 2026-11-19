// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Linq;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    public class StabilityTestTests : CalibrationTestFixture
    {
        StabilityTest _test;
        Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _test = AddTest<StabilityTest>();
            _test.PreferredDevice = InputDeviceKind.Gamepad;
        }

        /// <summary>
        /// Plays frames every 0.1 s until the test ends. The stick starts at rest (centre, which is
        /// outside the off-centre target) for <paramref name="restFrames"/> frames, like a real
        /// player; then <paramref name="stickAt"/> gives the position for each later frame.
        /// </summary>
        void RunFrames(System.Func<int, Vector2> stickAt, int restFrames = 3, int maxFrames = 300)
        {
            for (int frame = 0; frame < maxFrames && _test.IsRunning; frame++)
            {
                Set(_gamepad.leftStick, frame < restFrames ? Vector2.zero : stickAt(frame));
                TickAt(_test, 10.0 + 0.1 * frame);
            }
        }

        Measurements Result()
        {
            Assert.IsTrue(_test.IsFinished, "test should have finished");
            var m = new Measurements();
            _test.WriteResult(m);
            return m;
        }

        /// <summary>Small, real movement around a point (a steady but living hand).</summary>
        static Vector2 Jitter(Vector2 around, int f, float amount = 0.01f) =>
            around + new Vector2(f % 2 == 0 ? amount : -amount, f % 3 == 0 ? amount : 0);

        [Test]
        public void SteadyHand_OnTarget_GivesASmallSpread_AndIsOk()
        {
            _test.Begin();
            Assert.AreEqual(StabilityTest.Mode.Stick, _test.ActiveMode);
            RunFrames(f => Jitter(_test.StickTarget, f));

            var m = Result();
            Assert.Less(m.Stability.Spread, 0.1);
            Assert.Greater(m.Stability.Spread, _test.MinMeaningfulSpread);
            Assert.AreEqual(Confidence.Ok, m.Stability.Confidence);
            Assert.AreEqual(InputDeviceKind.Gamepad, m.Stability.Device);
        }

        [Test]
        public void SteadyButOffCentre_IsNotUnsteady_AndGetsNoAimAssist()
        {
            _test.Begin();
            var offCentre = _test.StickTarget + new Vector2(0.6f * _test.StickRadius, 0);
            RunFrames(f => Jitter(offCentre, f));

            var m = Result();
            Assert.Less(m.Stability.Spread, 0.1);
            Assert.AreEqual(0.6, m.Stability.MeanOffset, 0.05);    // offset kept as information only
            var p = RulesEngine.Evaluate(m, new RulesConfig()).Profile;
            Assert.AreEqual(AimAssist.Off, p.aimAssist);
            Assert.AreEqual(new RulesConfig().DefaultStickDeadzone, p.stickDeadzone);
        }

        [Test]
        public void ShakingAroundOwnAverage_GivesSpreadInRadiusUnits()
        {
            _test.Begin();
            // ±0.05 stick units around a point that is itself off-centre; radius 0.25 → spread 0.2.
            var centre = _test.StickTarget + new Vector2(0, 0.1f);
            RunFrames(f => centre + new Vector2(f % 2 == 0 ? 0.05f : -0.05f, 0));

            var m = Result();
            Assert.AreEqual(0.2, m.Stability.Spread, 1e-3);
            Assert.AreEqual(0.4, m.Stability.MeanOffset, 1e-3);
            Assert.AreEqual(AimAssist.Medium, RulesEngine.Evaluate(m, new RulesConfig()).Profile.aimAssist);
        }

        [Test]
        public void MeasuringStarts_OnlyAfterEnteringTheCircle_PlusSettleTime()
        {
            _test.Begin();
            // Frames 0–19 (2 s): far outside, moving wildly. Then inside with a steady hand.
            RunFrames(f => f < 20 ? new Vector2(f % 2 == 0 ? -0.9f : 0.9f, -0.8f) : Jitter(_test.StickTarget, f), restFrames: 0);

            var m = Result();
            Assert.Less(m.Stability.Spread, 0.1);   // the wild part outside was not measured
            // 5 s of measuring at 10 frames/s ≈ 50 samples (settling frames not included).
            Assert.That(_test.SampleCount, Is.InRange(49, 52));
        }

        [Test]
        public void DotAlreadyInsideAtTheStart_DoesNotStartMeasuring_UntilBroughtInActively()
        {
            Set(_gamepad.leftStick, _test.StickTarget);   // already held in the circle when the check starts
            _test.Begin();
            // Held inside from the very first frame: never seen outside → no measuring.
            RunFrames(f => Jitter(_test.StickTarget, f), restFrames: 0, maxFrames: 30);
            Assert.AreEqual(StabilityTest.Phase.WaitingToEnter, _test.CurrentPhase);

            // Out, then back in: now it counts.
            for (int f = 30; f < 300 && _test.IsRunning; f++)
            {
                Set(_gamepad.leftStick, f < 33 ? Vector2.zero : Jitter(_test.StickTarget, f));
                TickAt(_test, 10.0 + 0.1 * f);
            }
            Assert.AreEqual(Confidence.Ok, Result().Stability.Confidence);
        }

        [Test]
        public void NoInputWhileMeasuring_IsUncertain_NotSteady()
        {
            _test.Begin();
            // Brought in, then not a single change (hand off the stick / device asleep).
            RunFrames(_ => _test.StickTarget);

            var m = Result();
            Assert.AreEqual(0, _test.MovedSamples);
            Assert.AreEqual(Confidence.Uncertain, m.Stability.Confidence);
            Assert.IsFalse(RulesEngine.Evaluate(m, new RulesConfig()).Proposes("aimAssist"));
        }

        [Test]
        public void PracticallyZeroSpread_IsUncertain_EvenWithInput()
        {
            _test.Begin();
            RunFrames(f => Jitter(_test.StickTarget, f, amount: 0.00005f));   // input arrives, but no real movement

            var m = Result();
            Assert.Greater(_test.MovedSamples, 0);
            Assert.Less(m.Stability.Spread, _test.MinMeaningfulSpread);
            Assert.AreEqual(Confidence.Uncertain, m.Stability.Confidence);
        }

        [Test]
        public void NeverReachingTheCircle_EndsAfterTimeout_AsUncertain_WithNoProposal()
        {
            _test.Begin();
            RunFrames(_ => new Vector2(-0.9f, 0));

            var m = Result();
            Assert.AreEqual(Confidence.Uncertain, m.Stability.Confidence);
            Assert.IsFalse(RulesEngine.Evaluate(m, new RulesConfig()).Proposes("aimAssist"));
        }

        [Test]
        public void NoStickAndNoMouse_IsSkippedAutomatically()
        {
            InputSystem.RemoveDevice(_gamepad);
            _test.PreferredDevice = InputDeviceKind.Keyboard;
            _test.Begin();

            var m = Result();
            Assert.AreEqual(Confidence.Skipped, m.Stability.Confidence);
        }
    }

    public class StabilityMouseTests : CalibrationTestFixture
    {
        StabilityTest _test;
        Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _test = AddTest<StabilityTest>();
            _test.PreferredDevice = InputDeviceKind.Mouse;
        }

        void MoveMouse(Vector2 to) => Set(_mouse.position, to);

        [Test]
        public void CursorInTheMiddle_TheCircleIsPlacedAway_SoTheCursorStartsOutside()
        {
            var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
            MoveMouse(centre);
            _test.Begin();

            Assert.AreEqual(StabilityTest.Mode.Mouse, _test.ActiveMode);
            float distance = Vector2.Distance(centre, _test.Target);
            Assert.GreaterOrEqual(distance, 2.5f * _test.Radius, "the cursor must start outside the circle");
        }

        [Test]
        public void ParkedMouse_InsideTheCircle_IsUncertain()
        {
            MoveMouse(Vector2.zero);
            _test.Begin();
            var target = _test.Target;
            MoveMouse(target);                          // brought in once…
            for (int f = 0; f < 100 && _test.IsRunning; f++)
                TickAt(_test, 10.0 + 0.1 * f);        // …then the hand leaves the mouse

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.IsTrue(_test.IsFinished);
            Assert.AreEqual(0.0, m.Stability.Spread, 1e-9);
            Assert.AreEqual(Confidence.Uncertain, m.Stability.Confidence, "a still mouse is not proof of a still hand");
        }

        [Test]
        public void HandOnTheMouse_SmallMovements_IsOk()
        {
            MoveMouse(Vector2.zero);
            _test.Begin();
            var target = _test.Target;
            for (int f = 0; f < 100 && _test.IsRunning; f++)
            {
                MoveMouse(target + new Vector2(f % 2 == 0 ? 2f : -2f, f % 3 == 0 ? 1f : 0f));   // a couple of pixels
                TickAt(_test, 10.0 + 0.1 * f);
            }
            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(Confidence.Ok, m.Stability.Confidence);
            Assert.AreEqual(InputDeviceKind.Mouse, m.Stability.Device);
        }
    }

    public class ButtonAccessTestTests : CalibrationTestFixture
    {
        ButtonAccessTest _test;
        Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _test = AddTest<ButtonAccessTest>();
            _test.Buttons = ButtonAccessTest.DefaultButtons(); // A, X, LB, RB
        }

        [Test]
        public void ButtonsNotPressedWithin5s_AreListed_AndDriveRemapProposal()
        {
            _test.Begin();
            Assert.AreEqual(CalibrationTexts.ButtonJump, _test.CurrentPrompt.Name);

            Tap(_gamepad.buttonSouth, 10.5, 10.6);   // A: pressed
            TickAt(_test, 10.6);
            Assert.AreEqual(CalibrationTexts.ButtonAction, _test.CurrentPrompt.Name);

            Tap(_gamepad.buttonEast, 11.0, 11.1);    // wrong button: doesn't count for X
            TickAt(_test, 15.7);                      // X: 5 s passed → not pressed
            Assert.AreEqual(CalibrationTexts.ButtonLeftBumper, _test.CurrentPrompt.Name);

            Tap(_gamepad.leftShoulder, 16.0, 16.1);  // LB: pressed
            TickAt(_test, 16.1);
            TickAt(_test, 21.2);                      // RB: not pressed
            Assert.IsTrue(_test.IsFinished);

            var m = new Measurements();
            _test.WriteResult(m);
            CollectionAssert.AreEqual(new[] { CalibrationTexts.ButtonAction, CalibrationTexts.ButtonRightBumper }, m.Buttons.UnpressedButtons);
            Assert.AreEqual(4, m.Buttons.RequestedButtons.Count);

            var p = RulesEngine.Evaluate(m, new RulesConfig()).Profile;
            CollectionAssert.AreEqual(m.Buttons.UnpressedButtons, p.remapSuggestions);
            Assert.IsTrue(p.suggestOneHandedLayout);   // 2 of 4 = half
        }

        [Test]
        public void KeyboardBindingsCountToo()
        {
            _test.Begin();
            foreach (var key in new[] { Keyboard.spaceKey, Keyboard.eKey, Keyboard.qKey, Keyboard.rKey })
            {
                Tap(key, Now + 0.2, Now + 0.3);
                TickAt(_test, Now + 0.3);
            }
            var m = new Measurements();
            _test.WriteResult(m);
            Assert.IsEmpty(m.Buttons.UnpressedButtons);
            Assert.AreEqual(InputDeviceKind.Keyboard, m.Buttons.Device);
        }
    }

    public class CalibrationRunnerTests : CalibrationTestFixture
    {
        CalibrationRunner _runner;
        RulesResult _proposal;

        public override void Setup()
        {
            AccessCalibrate.ResetStaticState();
            base.Setup();
            Host.SetActive(false);                   // configure before OnEnable
            _runner = Host.AddComponent<CalibrationRunner>();
            _runner.UseBuiltInView = false;
            _runner.View = NullCalibrationView.Instance;
            _runner.Clock = () => Now;
            Host.SetActive(true);
            _runner.ProposalReady += (r, m) => _proposal = r;
        }

        public override void TearDown()
        {
            base.TearDown();
            AccessCalibrate.ResetStaticState();
        }

        [Test]
        public void RequestCalibration_StartsTheRunner_WithTheFirstIntro()
        {
            AccessCalibrate.RequestCalibration();
            Assert.IsTrue(_runner.IsCalibrating);
            Assert.IsTrue(_runner.InIntro);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
        }

        [Test]
        public void SkippingEveryTest_ProposesNothing_AllMeasurementsSkipped()
        {
            _runner.StartCalibration();

            Press(Keyboard.escapeKey, time: 10.1);   // Esc on the first intro card
            Release(Keyboard.escapeKey, time: 10.2);
            _runner.Tick();
            Assert.AreSame(_runner.Hold, _runner.CurrentTest);

            for (int i = 0; i < 4; i++) _runner.SkipCurrent();   // the on-screen "Skip" button

            Assert.IsFalse(_runner.IsCalibrating);
            Assert.IsNotNull(_proposal);
            Assert.IsEmpty(_proposal.Changes);
            var m = _runner.LastMeasurements;
            Assert.IsTrue(new MeasurementBase[] { m.Reaction, m.Hold, m.Taps, m.Stability, m.Buttons }
                .All(x => x.Confidence == Confidence.Skipped));
        }

        [Test]
        public void UnansweredCheck_RecordsTheDeviceThatStartedIt_NotTheOneUsedBefore()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            _runner.StartCalibration();

            Tap(mouse.leftButton, 10.1, 10.2);            // Reaction started with a mouse click…
            _runner.Tick();
            Assert.IsTrue(_runner.Reaction.IsRunning);
            _runner.SkipCurrent();                         // …then skipped
            _runner.SkipCurrent();                         // Hold (from its intro)
            _runner.SkipCurrent();                         // Repeated presses
            _runner.SkipCurrent();                         // Steadiness
            Assert.AreSame(_runner.ButtonAccess, _runner.CurrentTest);

            Tap(Keyboard.enterKey, 11.1, 11.2);            // Button reach started from the keyboard
            _runner.Tick();
            Assert.IsTrue(_runner.ButtonAccess.IsRunning);
            for (int i = 0; i < 10 && _runner.ButtonAccess.IsRunning; i++)
                TickAt(_runner.ButtonAccess, Now + 6);     // no prompt answered: every one times out

            var buttons = _runner.LastMeasurements.Buttons;
            Assert.AreEqual(_runner.ButtonAccess.Buttons.Count, buttons.UnpressedButtons.Count);
            Assert.AreEqual(InputDeviceKind.Keyboard, buttons.Device,
                "the keyboard started this check; the mouse click belonged to an earlier one");
        }

        [Test]
        public void AnyButtonOnIntro_StartsTheTest()
        {
            _runner.StartCalibration();
            Tap(Keyboard.spaceKey, 10.1, 10.2);
            _runner.Tick();
            Assert.IsFalse(_runner.InIntro);
            Assert.IsTrue(_runner.Reaction.IsRunning);
        }
    }
}

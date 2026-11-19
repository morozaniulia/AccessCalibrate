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

        /// <summary>Runs settle + 5 s of measuring at 10 frames/s, letting <paramref name="stickAt"/> pick the stick position per frame.</summary>
        void RunFrames(System.Func<int, Vector2> stickAt)
        {
            int frame = 0;
            for (double t = 10.0; t <= 10 + _test.SettleSeconds + _test.MeasureSeconds + 0.15; t += 0.1, frame++)
            {
                Set(_gamepad.leftStick, stickAt(frame));
                TickAt(_test, t);
            }
        }

        [Test]
        public void PerfectlyStill_OnTarget_GivesRmsZero()
        {
            _test.Begin();
            Assert.AreEqual(StabilityTest.Mode.Stick, _test.ActiveMode);
            RunFrames(_ => _test.StickTarget);

            Assert.IsTrue(_test.IsFinished);
            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(0, m.Stability.RmsDeviation, 1e-6);
            Assert.AreEqual(Confidence.Ok, m.Stability.Confidence);
            Assert.AreEqual(InputDeviceKind.Gamepad, m.Stability.Device);
        }

        [Test]
        public void ShakingAroundTarget_GivesRmsInRadiusUnits()
        {
            _test.Begin();
            // ±0.05 stick units around the target; radius 0.25 → RMS = 0.2 radii.
            RunFrames(f => _test.StickTarget + new Vector2(f % 2 == 0 ? 0.05f : -0.05f, 0));

            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(0.2, m.Stability.RmsDeviation, 1e-4);
            Assert.AreEqual(AimAssist.Medium, RulesEngine.Evaluate(m, new RulesConfig()).Profile.aimAssist);
        }

        [Test]
        public void NoStickAndNoMouse_IsSkippedAutomatically()
        {
            InputSystem.RemoveDevice(_gamepad);
            _test.PreferredDevice = InputDeviceKind.Keyboard;
            _test.Begin();

            Assert.IsTrue(_test.IsFinished);
            var m = new Measurements();
            _test.WriteResult(m);
            Assert.AreEqual(Confidence.Skipped, m.Stability.Confidence);
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
            Assert.AreEqual("Sărit (A / Spațiu)", _test.CurrentPrompt.Name);

            Tap(_gamepad.buttonSouth, 10.5, 10.6);   // A: pressed
            TickAt(_test, 10.6);
            Assert.AreEqual("Acțiune (X / E)", _test.CurrentPrompt.Name);

            Tap(_gamepad.buttonEast, 11.0, 11.1);    // wrong button: doesn't count for X
            TickAt(_test, 15.7);                      // X: 5 s passed → not pressed
            Assert.AreEqual("Umăr stânga (LB / Q)", _test.CurrentPrompt.Name);

            Tap(_gamepad.leftShoulder, 16.0, 16.1);  // LB: pressed
            TickAt(_test, 16.1);
            TickAt(_test, 21.2);                      // RB: not pressed
            Assert.IsTrue(_test.IsFinished);

            var m = new Measurements();
            _test.WriteResult(m);
            CollectionAssert.AreEqual(new[] { "Acțiune (X / E)", "Umăr dreapta (RB / R)" }, m.Buttons.UnpressedButtons);
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

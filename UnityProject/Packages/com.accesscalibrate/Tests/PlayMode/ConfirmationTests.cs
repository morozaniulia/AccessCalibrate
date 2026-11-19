// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>The confirmation screen: the player decides; uncertain proposals start switched off.</summary>
    public class ConfirmationTests : CalibrationTestFixture
    {
        CalibrationRunner _runner;
        string _dir;
        AccessibilityProfile _completedWith;
        Gamepad _gamepad;

        public override void Setup()
        {
            AccessCalibrate.ResetStaticState();
            base.Setup();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            AccessCalibrate.Config = new RulesConfig { ReactionLatencyMarginMs = 0 };

            Host.SetActive(false);
            _runner = Host.AddComponent<CalibrationRunner>();
            _runner.UseBuiltInView = false;
            _runner.View = NullCalibrationView.Instance;
            _runner.Clock = () => Now;
            Host.SetActive(true);
            _runner.Completed += p => _completedWith = p;
        }

        public override void TearDown()
        {
            base.TearDown();
            AccessCalibrate.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        /// <summary>Reaction reliable (timing ×1.5), hold uncertain (hold-to-toggle).</summary>
        static Measurements Sample() => new Measurements
        {
            Reaction = new ReactionMeasurement { MedianMs = 600, Confidence = Confidence.Ok },
            Hold = new HoldMeasurement { MaxHoldSeconds = 1.2, Confidence = Confidence.Uncertain },
        };

        int IndexOf(string setting) => _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == setting);

        /// <summary>Press a button after the confirmation input delay and let the runner handle it.</summary>
        void PressAndTick(UnityEngine.InputSystem.Controls.ButtonControl button)
        {
            Now += 0.3;
            Tap(button, Now, Now + 0.05);
            _runner.Tick();
        }

        [Test]
        public void Accept_AppliesReliableProposals_NotUncertainOnes_AndSaves()
        {
            _runner.ShowProposal(Sample());
            Assert.IsTrue(_runner.IsReviewing);

            _runner.Accept();

            var p = AccessCalibrate.Profile;
            Assert.AreEqual(1.5f, p.timingMultiplier);
            Assert.IsFalse(p.holdToToggle, "uncertain proposal is off unless the player turns it on");
            Assert.IsTrue(AccessCalibrate.HasSavedProfile);
            Assert.IsFalse(_runner.IsReviewing);
            Assert.AreEqual(p, _completedWith);
        }

        [Test]
        public void NothingIsAppliedBeforeAccept()
        {
            _runner.ShowProposal(Sample());
            Assert.AreEqual(1f, AccessCalibrate.Profile.timingMultiplier);
            Assert.IsFalse(AccessCalibrate.HasSavedProfile);
        }

        [Test]
        public void Gamepad_CanSwitchOnTheUncertainProposal_ThenAccept()
        {
            _runner.ShowProposal(Sample());
            Now += _runner.ConfirmationInputDelaySeconds;
            Assert.AreEqual(_runner.AcceptIndex, _runner.Selection);   // starts on Accept

            // Move up from Accept to the hold row (the last proposal), switch it on, go back down, accept.
            int steps = _runner.AcceptIndex - IndexOf("holdToToggle");
            for (int i = 0; i < steps; i++) PressAndTick(_gamepad.dpad.up);
            Assert.AreEqual(IndexOf("holdToToggle"), _runner.Selection);
            PressAndTick(_gamepad.buttonSouth);
            Assert.IsTrue(_runner.Review.Items[IndexOf("holdToToggle")].Accepted);

            for (int i = 0; i < steps; i++) PressAndTick(_gamepad.dpad.down);
            PressAndTick(_gamepad.buttonSouth);

            Assert.IsTrue(AccessCalibrate.Profile.holdToToggle);
            Assert.AreEqual(1.5f, AccessCalibrate.Profile.timingMultiplier);
        }

        [Test]
        public void Keyboard_CanSwitchOffAReliableProposal()
        {
            _runner.ShowProposal(Sample());
            Now += _runner.ConfirmationInputDelaySeconds;
            int steps = _runner.AcceptIndex - IndexOf("timingMultiplier");
            for (int i = 0; i < steps; i++) PressAndTick(Keyboard.upArrowKey);
            PressAndTick(Keyboard.enterKey);                      // switch timing off
            for (int i = 0; i < steps; i++) PressAndTick(Keyboard.downArrowKey);
            PressAndTick(Keyboard.enterKey);                      // Accept

            Assert.AreEqual(1f, AccessCalibrate.Profile.timingMultiplier);
            Assert.IsTrue(AccessCalibrate.HasSavedProfile);
        }

        [Test]
        public void PressRightAfterTheLastTest_IsIgnored()
        {
            _runner.ShowProposal(Sample());
            Tap(_gamepad.buttonSouth, Now + 0.1, Now + 0.15);    // within the input delay
            Now += 0.2;
            _runner.Tick();
            Assert.IsTrue(_runner.IsReviewing, "an early press must not accept");
        }

        [Test]
        public void Redo_StartsTheCalibrationAgain()
        {
            _runner.ShowProposal(Sample());
            _runner.Redo();
            Assert.IsFalse(_runner.IsReviewing);
            Assert.IsTrue(_runner.IsCalibrating);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
            Assert.IsFalse(AccessCalibrate.HasSavedProfile);
        }

        [Test]
        public void Skip_FirstTime_SavesDefaults_SoItIsNotAskedEveryLaunch()
        {
            _runner.ShowProposal(Sample());
            _runner.Skip();
            Assert.IsTrue(AccessCalibrate.HasSavedProfile);
            Assert.AreEqual(AccessCalibrate.DefaultProfile(), AccessCalibrate.Profile);
            Assert.IsFalse(AccessCalibrate.RunIfFirstLaunch());
        }

        [Test]
        public void Skip_AfterARecalibration_KeepsThePlayersExistingProfile()
        {
            var existing = new AccessibilityProfile { aimAssist = AimAssist.High, holdToToggle = true };
            AccessCalibrate.SetProfile(existing);

            _runner.ShowProposal(Sample());
            _runner.Skip();

            Assert.AreEqual(existing, AccessCalibrate.Profile);
        }

        [Test]
        public void FullRun_EndsOnTheConfirmationScreen()
        {
            _runner.StartCalibration();
            for (int i = 0; i < 5; i++) _runner.SkipCurrent();
            Assert.IsTrue(_runner.IsReviewing);
            Assert.IsEmpty(_runner.Review.Items);
        }

        [Test]
        public void ProposalTexts_ShowValueAndReason_InPlainWords()
        {
            _runner.ShowProposal(Sample());
            var timing = _runner.Review.Items[IndexOf("timingMultiplier")].Change;
            Assert.AreEqual("More time for quick moments (timing windows ×1.5)", ProposalTexts.Title(timing));
            Assert.AreEqual("Median reaction time 600 ms (rule: above 450 ms)", ProposalTexts.Reason(timing));
        }
    }
}

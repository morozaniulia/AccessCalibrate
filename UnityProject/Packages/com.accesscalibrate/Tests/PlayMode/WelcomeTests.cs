// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>The optional Welcome screen (Start / Not now), driven with simulated keyboard input.</summary>
    public class WelcomeTests : CalibrationTestFixture
    {
        sealed class WelcomeView : ICalibrationView, ICalibrationWelcomeView
        {
            public int WelcomeShown, Selection = -1, IntroShown, Hidden;
            public void ShowWelcome() => WelcomeShown++;
            public void SetWelcomeSelection(int index) => Selection = index;
            public void ShowIntro(int index, int count, string title, string instructions) => IntroShown++;
            public void ShowTest(string title, string instructions) { }
            public void SetStatus(string text) { }
            public void ShowStimulus(bool visible) { }
            public void PlayCue() { }
            public void SetPrompt(string text) { }
            public void ShowStabilityArena(bool visible) { }
            public void SetCursor(float xInRadii, float yInRadii) { }
            public void ShowConfirmation(ProposalReview review) { }
            public void SetConfirmationSelection(int index) { }
            public void RefreshConfirmation() { }
            public void Hide() => Hidden++;
        }

        CalibrationRunner _runner;
        WelcomeView _view;
        string _dir;
        AccessibilityProfile _completed;

        public override void Setup()
        {
            AccessCalibrate.ResetStaticState();
            base.Setup();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            Host.SetActive(false);
            _runner = Host.AddComponent<CalibrationRunner>();
            _runner.UseBuiltInView = false;
            _runner.View = _view = new WelcomeView();
            _runner.Clock = () => Now;
            Host.SetActive(true);
            _runner.Completed += p => _completed = p;
        }

        public override void TearDown()
        {
            base.TearDown();
            AccessCalibrate.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        void PressAndTick(UnityEngine.InputSystem.Controls.ButtonControl key)
        {
            Now += 0.5;   // past the welcome input delay
            Tap(key, Now, Now + 0.05);
            _runner.Tick();
        }

        [Test]
        public void ViewWithAWelcomeScreen_StartsThere_WithStartSelected()
        {
            _runner.StartCalibration();
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.IsFalse(_runner.InIntro);
            Assert.AreEqual(1, _view.WelcomeShown);
            Assert.AreEqual(0, _view.Selection);
        }

        [Test]
        public void Enter_OnStart_GoesToTheFirstCheck()
        {
            _runner.StartCalibration();
            PressAndTick(Keyboard.enterKey);
            Assert.IsFalse(_runner.IsWelcoming);
            Assert.IsTrue(_runner.InIntro);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
            Assert.AreEqual(1, _view.IntroShown);
        }

        [Test]
        public void Down_ThenEnter_IsNotNow_AndSavesDefaultsOnFirstLaunch()
        {
            _runner.StartCalibration();
            PressAndTick(Keyboard.downArrowKey);
            Assert.AreEqual(1, _view.Selection);
            PressAndTick(Keyboard.enterKey);

            Assert.IsFalse(_runner.IsCalibrating);
            Assert.IsTrue(AccessCalibrate.HasSavedProfile);
            Assert.AreEqual(AccessCalibrate.DefaultProfile(), _completed);
            Assert.AreEqual(1, _view.Hidden);
        }

        [Test]
        public void Escape_IsNotNow_AndKeepsAnExistingProfile()
        {
            var existing = new AccessibilityProfile { holdToToggle = true };
            AccessCalibrate.SetProfile(existing);
            _runner.StartCalibration();
            PressAndTick(Keyboard.escapeKey);

            Assert.IsFalse(_runner.IsCalibrating);
            Assert.AreEqual(existing, AccessCalibrate.Profile);
        }

        [Test]
        public void PressRightAway_IsIgnored()
        {
            _runner.StartCalibration();
            Now += 0.1;   // inside the welcome input delay
            Tap(Keyboard.enterKey, Now, Now + 0.05);
            _runner.Tick();
            Assert.IsTrue(_runner.IsWelcoming);
        }

        [Test]
        public void ViewWithoutAWelcomeScreen_GoesStraightToTheFirstCheck()
        {
            _runner.View = NullCalibrationView.Instance;
            _runner.StartCalibration();
            Assert.IsFalse(_runner.IsWelcoming);
            Assert.IsTrue(_runner.InIntro);
        }
    }
}

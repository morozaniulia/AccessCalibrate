// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>
    /// UI v2 behaviour (spec §5): Back, Skip, Skip the whole setup, "What are you playing with?",
    /// the Advanced value, and the summary after Apply — driven with simulated input.
    /// </summary>
    public class NavigationTests : CalibrationTestFixture
    {
        sealed class FullView : ICalibrationView, ICalibrationWelcomeView, ICalibrationIntroView, ICalibrationDeviceView, ICalibrationSummaryView, ICalibrationTouchView
        {
            /// <summary>The press area: the left part of the screen.</summary>
            public bool IsInPressArea(UnityEngine.Vector2 p) => p.x < 100;
            public double Intro;
            public int Hidden, Refreshed, SummaryShown, PickerShown;
            public bool? SummaryApplied, Copied;
            public int DeviceSelection = -1, SummarySelection = -1;
            public IReadOnlyList<DeviceChoice> Choices;
            public DeviceChoice? Used;
            public bool PickerOpen;
            public double IntroSeconds => Intro;
            public bool CanReplayIntro => true;
            public void ShowWelcome() { }
            public void SetWelcomeSelection(int index) { }
            public void ShowIntro(int index, int count, string title, string instructions) { }
            public void ShowTest(string title, string instructions) { }
            public void SetStatus(string text) { }
            public void ShowStimulus(bool visible) { }
            public void PlayCue() { }
            public void SetPrompt(string text) { }
            public void ShowStabilityArena(bool visible) { }
            public void SetCursor(float xInRadii, float yInRadii) { }
            public void ShowConfirmation(ProposalReview review) { }
            public void SetConfirmationSelection(int index) { }
            public void RefreshConfirmation() => Refreshed++;
            public void Hide() => Hidden++;
            public void ShowDevicePicker(IReadOnlyList<DeviceChoice> choices, DeviceChoice detected, int selected)
            { Choices = choices; DeviceSelection = selected; PickerOpen = true; PickerShown++; }
            public void SetDeviceSelection(int index) => DeviceSelection = index;
            public void HideDevicePicker() => PickerOpen = false;
            public void UseDevice(DeviceChoice choice) => Used = choice;
            public void ShowSummary(Measurements measurements, ProposalReview review, bool applied) { SummaryShown++; SummaryApplied = applied; }
            public void SetSummarySelection(int index) => SummarySelection = index;
            public void ShowCopyResult(bool copied) => Copied = copied;
        }

        CalibrationRunner _runner;
        FullView _view;
        string _dir;
        int _completed;
        Gamepad _gamepad;

        public override void Setup()
        {
            AccessCalibrate.ResetStaticState();
            base.Setup();
            _completed = 0;   // NUnit keeps one fixture instance for all tests
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            AccessCalibrate.Config = new RulesConfig { ReactionLatencyMarginMs = 0 };
            Host.SetActive(false);
            _runner = Host.AddComponent<CalibrationRunner>();
            _runner.UseBuiltInView = false;
            _runner.View = _view = new FullView();
            _runner.Clock = () => Now;
            _runner.ConfirmationInputDelaySeconds = 0;
            Host.SetActive(true);
            _runner.Completed += _ => _completed++;
        }

        public override void TearDown()
        {
            base.TearDown();
            AccessCalibrate.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        void PressAndTick(UnityEngine.InputSystem.Controls.ButtonControl button)
        {
            Now += 0.5;   // past the input delays
            Tap(button, Now, Now + 0.05);
            _runner.Tick();
        }

        void StartChecks()
        {
            _runner.StartCalibration();
            _runner.WelcomeStart();
            Assert.IsTrue(_runner.Reaction.IsRunning);
        }

        static Measurements Sample() => new Measurements
        {
            Reaction = new ReactionMeasurement { MedianMs = 600, ValidTrialCount = 5, Confidence = Confidence.Ok, Device = InputDeviceKind.Keyboard },
            Hold = new HoldMeasurement { MaxHoldSeconds = 1.2, Confidence = Confidence.Uncertain, Device = InputDeviceKind.Keyboard },
        };

        int IndexOf(string setting) => _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == setting);

        // ------------------------------------------------------------------ Back / Skip / Skip all

        [Test]
        public void Backspace_GoesBackToThePreviousCheck_AndStartsItAgain()
        {
            StartChecks();
            _runner.SkipCurrent();
            Assert.AreSame(_runner.Hold, _runner.CurrentTest);
            PressAndTick(Keyboard.backspaceKey);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
            Assert.IsTrue(_runner.Reaction.IsRunning);
            Assert.IsFalse(_runner.Hold.IsRunning, "the check we left is stopped, not finished");
        }

        [Test]
        public void Back_OnTheFirstCheck_ReturnsToTheWelcomeScreen()
        {
            StartChecks();
            _runner.Back();
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.IsFalse(_runner.Reaction.IsRunning);
        }

        [Test]
        public void GamepadView_GoesBack()
        {
            StartChecks();
            _runner.SkipCurrent();
            PressAndTick(_gamepad.selectButton);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
        }

        [Test]
        public void Esc_SkipsOnlyThisCheck()
        {
            StartChecks();
            PressAndTick(Keyboard.escapeKey);
            Assert.AreSame(_runner.Hold, _runner.CurrentTest);
            Assert.AreEqual(Confidence.Skipped, _runner.LastMeasurements.Reaction.Confidence);
            Assert.IsTrue(_runner.IsCalibrating);
        }

        [Test]
        public void ShiftEsc_SkipsTheWholeSetup_WithoutSuggestions()
        {
            StartChecks();
            _runner.SkipCurrent();
            Now += 0.5;
            Press(Keyboard.leftShiftKey, time: Now);
            Tap(Keyboard.escapeKey, Now + 0.1, Now + 0.15);
            _runner.Tick();
            Release(Keyboard.leftShiftKey, time: Now + 0.2);

            Assert.IsFalse(_runner.IsCalibrating);
            Assert.IsFalse(_runner.IsReviewing, "no suggestions screen");
            Assert.AreEqual(1, _completed);
            Assert.AreEqual(1, _view.Hidden);
            Assert.IsTrue(AccessCalibrate.HasSavedProfile, "first launch: defaults saved, like Not now");
            Assert.AreEqual(AccessCalibrate.DefaultProfile(), AccessCalibrate.Profile);
        }

        [Test]
        public void SkipAll_KeepsAnExistingProfile()
        {
            var existing = new AccessibilityProfile { holdToToggle = true };
            AccessCalibrate.SetProfile(existing);
            StartChecks();
            _runner.SkipAll();
            Assert.AreEqual(existing, AccessCalibrate.Profile);
        }

        // ------------------------------------------------------------------ intro + first signal

        [Test]
        public void Reaction_FirstSignal_ComesOnlyAfterTheIntro()
        {
            _view.Intro = 10;
            _runner.StartCalibration();
            double start = Now;
            _runner.WelcomeStart();
            var r = _runner.Reaction;
            Assert.IsTrue(r.IsRunning && r.IsInIntro);
            for (double t = start; t < start + 10; t += 0.25)
            {
                TickAt(r, t);
                Assert.IsFalse(r.StimulusVisible, "never during the intro");
            }
            TickAt(r, start + 10);
            Assert.IsFalse(r.IsInIntro);
            Assert.GreaterOrEqual(r.NextStimulusAt, start + 10 + r.MinWaitSeconds, "intro, then the usual random wait");
            Assert.LessOrEqual(r.NextStimulusAt, start + 10 + r.MaxWaitSeconds + 1e-9);
        }

        [Test]
        public void EveryCheck_WaitsForTheIntro_NoTimerRunsDuringIt()
        {
            _view.Intro = 10;
            StartChecks();
            for (int i = 0; i < 2; i++) _runner.SkipCurrent();   // Repeated presses: gives up after 6 s without a press
            var taps = _runner.Taps;
            double start = Now;
            TickAt(taps, start + 9);
            Assert.IsTrue(taps.IsRunning && taps.IsInIntro, "its 6 s wait has not started yet");
            TickAt(taps, start + 10);
            TickAt(taps, start + 15);
            Assert.IsTrue(taps.IsRunning, "5 s after the intro: still waiting for the first press");
            TickAt(taps, start + 16.1);
            Assert.IsFalse(taps.IsRunning, "6 s after the intro it moves on");
        }

        [Test]
        public void R_ReplaysTheIntro_TheCheckStartsAgain()
        {
            _view.Intro = 10;
            StartChecks();
            _runner.SkipCurrent();                         // Hold
            TickAt(_runner.Hold, Now + 10.5);              // intro over, waiting for the press
            Assert.IsFalse(_runner.Hold.IsInIntro);
            PressAndTick(Keyboard.rKey);
            Assert.AreSame(_runner.Hold, _runner.CurrentTest);
            Assert.IsTrue(_runner.Hold.IsInIntro, "the intro plays again");
        }

        // ------------------------------------------------------------------ device picker

        [Test]
        public void Picker_WithoutATouchScreen_OffersThreeDevices()
        {
            StartChecks();
            _runner.OpenDevicePicker();
            Assert.IsTrue(_runner.IsChoosingDevice);
            CollectionAssert.AreEqual(new[] { DeviceChoice.Xbox, DeviceChoice.PlayStation, DeviceChoice.KeyboardMouse }, _view.Choices);
        }

        [Test]
        public void Picker_DownThenEnter_UsesPlayStation_AndRestartsTheCheck()
        {
            StartChecks();
            _runner.SkipCurrent();                         // on Hold
            _runner.OpenDevicePicker();
            int from = _runner.DeviceSelection;
            while (_runner.DeviceSelection != 1) PressAndTick(Keyboard.downArrowKey);
            Assert.AreEqual(1, _view.DeviceSelection);
            Assert.AreNotEqual(-1, from);
            PressAndTick(Keyboard.enterKey);

            Assert.IsFalse(_runner.IsChoosingDevice);
            Assert.IsFalse(_view.PickerOpen);
            Assert.AreEqual(DeviceChoice.PlayStation, _view.Used);
            Assert.AreEqual(DeviceChoice.PlayStation, _runner.ChosenDevice);
            Assert.AreSame(_runner.Hold, _runner.CurrentTest, "same check…");
            Assert.IsTrue(_runner.Hold.IsRunning, "…started again with the chosen device");
        }

        [Test]
        public void Picker_CancelChangesNothing()
        {
            StartChecks();
            _runner.OpenDevicePicker();
            PressAndTick(_gamepad.buttonEast);   // B
            Assert.IsFalse(_runner.IsChoosingDevice);
            Assert.IsNull(_runner.ChosenDevice);
            Assert.IsNull(_view.Used);
            Assert.IsTrue(_runner.IsCalibrating);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
        }

        [Test]
        public void Picker_InputDoesNotReachTheCheck()
        {
            StartChecks();
            _runner.OpenDevicePicker();
            PressAndTick(Keyboard.escapeKey);   // Esc closes the picker; it must not skip the check
            Assert.IsFalse(_runner.IsChoosingDevice);
            Assert.AreSame(_runner.Reaction, _runner.CurrentTest);
        }

        [Test]
        public void Touch_IsOfferedWithATouchScreen_AndSkipsButtonReach()
        {
            _runner.ForceTouchAvailable = true;
            StartChecks();
            _runner.OpenDevicePicker();
            Assert.Contains(DeviceChoice.Touch, _view.Choices.ToList());
            _runner.ChooseDevice(DeviceChoice.Touch);
            for (int i = 0; i < 4; i++) _runner.SkipCurrent();   // reaction, hold, presses, steadiness

            Assert.IsFalse(_runner.IsCalibrating, "Button reach is skipped on touch");
            Assert.IsTrue(_runner.IsReviewing);
            Assert.AreEqual(Confidence.Skipped, _runner.LastMeasurements.Buttons.Confidence);
        }

        // ------------------------------------------------------------------ touch press area

        void TouchAt(float x, float y)
        {
            Now += 0.5;
            BeginTouch(1, new UnityEngine.Vector2(x, y));
            EndTouch(1, new UnityEngine.Vector2(x, y));
            _runner.Tick();
        }

        [Test]
        public void Touch_OnlyTouchesInThePressAreaCount()
        {
            InputSystem.AddDevice<Touchscreen>();
            StartChecks();
            _runner.ChooseDevice(DeviceChoice.Touch);
            _runner.SkipCurrent();                         // Hold
            Assert.IsNotNull(_runner.Hold.TouchFilter);
            BeginTouch(1, new UnityEngine.Vector2(500, 300));    // on a button (Skip, Back, Change…)
            Assert.IsFalse(_runner.Hold.IsHolding, "a touch outside the press area is not a hold");
            EndTouch(1, new UnityEngine.Vector2(500, 300));
            BeginTouch(1, new UnityEngine.Vector2(50, 300));     // on the circle
            Assert.IsTrue(_runner.Hold.IsHolding);
            EndTouch(1, new UnityEngine.Vector2(50, 300));

            _runner.SkipCurrent();                         // Repeated presses
            TouchAt(500, 300);
            TouchAt(600, 50);
            Assert.AreEqual(0, _runner.Taps.PressCount, "taps on buttons are never counted");
            TouchAt(40, 300);
            TouchAt(60, 200);
            Assert.AreEqual(2, _runner.Taps.PressCount);
        }

        [Test]
        public void OtherDevices_AreNotFilteredByThePressArea()
        {
            StartChecks();
            _runner.ChooseDevice(DeviceChoice.Xbox);
            Assert.IsNull(_runner.Reaction.TouchFilter);
        }

        // ------------------------------------------------------------------ Advanced value

        [Test]
        public void RightArrow_AdjustsTheSelectedValue_AndThatIsWhatGetsSaved()
        {
            _runner.ShowProposal(Sample());
            while (_runner.Selection != IndexOf("timingMultiplier")) PressAndTick(Keyboard.downArrowKey);
            PressAndTick(Keyboard.rightArrowKey);
            PressAndTick(Keyboard.rightArrowKey);
            Assert.AreEqual("2", _runner.Review.Items[IndexOf("timingMultiplier")].Value);
            Assert.AreEqual(2, _view.Refreshed);
            PressAndTick(_gamepad.dpad.left);
            Assert.AreEqual("1.75", _runner.Review.Items[IndexOf("timingMultiplier")].Value);

            _runner.Accept();
            Assert.AreEqual(1.75f, AccessCalibrate.Profile.timingMultiplier, 1e-4);
            Assert.AreEqual(1.75f, new ProfileStore(_dir).Load().timingMultiplier, 1e-4, "saved with the adjusted value");
        }

        [Test]
        public void Adjust_StopsAtTheEnds_AndIgnoresOnOffRows()
        {
            _runner.ShowProposal(Sample());
            int t = IndexOf("timingMultiplier"), h = IndexOf("holdToToggle");
            for (int i = 0; i < 20; i++) _runner.Adjust(t, +1);
            Assert.AreEqual("2.5", _runner.Review.Items[t].Value);
            for (int i = 0; i < 20; i++) _runner.Adjust(t, -1);
            Assert.AreEqual("1", _runner.Review.Items[t].Value);
            string before = _runner.Review.Items[h].Value;
            _runner.Adjust(h, +1);
            Assert.AreEqual(before, _runner.Review.Items[h].Value);
        }

        // ------------------------------------------------------------------ summary

        [Test]
        public void Apply_ShowsTheSummary_ContinueCloses()
        {
            _runner.ShowProposal(Sample());
            _runner.Accept();
            Assert.IsTrue(_runner.IsShowingSummary);
            Assert.AreEqual(true, _view.SummaryApplied);
            Assert.AreEqual(0, _view.SummarySelection, "Continue is selected");
            Assert.AreEqual(0, _completed, "not over until the player leaves the summary");
            Assert.IsTrue(AccessCalibrate.HasSavedProfile, "already applied and saved");

            PressAndTick(Keyboard.enterKey);
            Assert.IsFalse(_runner.IsShowingSummary);
            Assert.AreEqual(1, _completed);
            Assert.AreEqual(1, _view.Hidden);
        }

        [Test]
        public void Summary_CopyResults_PutsPlainTextOnTheClipboard()
        {
            _runner.ShowProposal(Sample());
            _runner.Adjust(IndexOf("timingMultiplier"), +1);
            _runner.Accept();
            PressAndTick(Keyboard.downArrowKey);
            Assert.AreEqual(1, _view.SummarySelection);
            PressAndTick(Keyboard.enterKey);

            Assert.AreEqual(true, _view.Copied);
            Assert.AreEqual(UnityEngine.GUIUtility.systemCopyBuffer, _runner.LastResultsText);
            StringAssert.Contains("Suggestions (applied)", _runner.LastResultsText);
            StringAssert.Contains("timingMultiplier = 1.75", _runner.LastResultsText);
            Assert.IsTrue(_runner.IsShowingSummary, "copying keeps the summary open");
        }

        [Test]
        public void Summary_Redo_StartsAgainFromWelcome()
        {
            _runner.ShowProposal(Sample());
            _runner.Accept();
            PressAndTick(Keyboard.upArrowKey);   // wraps to Redo
            Assert.AreEqual(2, _view.SummarySelection);
            PressAndTick(_gamepad.buttonSouth);
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.AreEqual(0, _completed);
        }

        [Test]
        public void NothingToSuggest_Continue_AlsoShowsTheSummary()
        {
            _runner.ShowProposal(new Measurements());
            Assert.IsEmpty(_runner.Review.Items);
            Assert.AreEqual(_runner.AcceptIndex, _runner.Selection);
            PressAndTick(Keyboard.enterKey);
            Assert.IsTrue(_runner.IsShowingSummary);
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;

namespace AccessCalibration
{
    /// <summary>
    /// What a calibration test can show. The logic only talks to this interface, so the visual
    /// design can be replaced later without touching the tests' logic.
    /// </summary>
    public interface ICalibrationView
    {
        /// <summary>Intro card before a test: "Test 2 of 5", title, instructions, "press any button to start".</summary>
        void ShowIntro(int index, int count, string title, string instructions);
        /// <summary>The test is running: title + instructions stay visible.</summary>
        void ShowTest(string title, string instructions);
        /// <summary>Short status line, e.g. "Trial 3 of 6" or "Keep holding…".</summary>
        void SetStatus(string text);
        /// <summary>The reaction signal (big visual + audio cue). Must not flash.</summary>
        void ShowStimulus(bool visible);
        /// <summary>A short, neutral sound cue (start of a trial, signal).</summary>
        void PlayCue();
        /// <summary>Big prompt text, e.g. the name of the button to press.</summary>
        void SetPrompt(string text);
        /// <summary>Stability test: show/hide the circle; cursor offset is in units of the circle radius.</summary>
        void ShowStabilityArena(bool visible);
        void SetCursor(float xInRadii, float yInRadii);
        /// <summary>
        /// The confirmation screen: one switch per proposal (with its reason, and a "check this"
        /// note when uncertain), plus Accept / Redo calibration / Skip.
        /// Rows are indexed 0…n−1, then n = Accept, n+1 = Redo, n+2 = Skip.
        /// </summary>
        void ShowConfirmation(ProposalReview review);
        /// <summary>Highlight the row the keyboard / gamepad cursor is on.</summary>
        void SetConfirmationSelection(int index);
        /// <summary>Re-read the switches from the review (after a keyboard / gamepad toggle).</summary>
        void RefreshConfirmation();
        void Hide();
    }

    /// <summary>A view that shows nothing — used by automated tests and headless runs.</summary>
    public sealed class NullCalibrationView : ICalibrationView
    {
        public static readonly NullCalibrationView Instance = new NullCalibrationView();
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
        public void RefreshConfirmation() { }
        public void Hide() { }
    }
}

namespace AccessCalibration
{
    /// <summary>
    /// Optional: a view that also has a Welcome screen (Start / Not now) before the first check.
    /// The runner shows it only when its view implements this interface; other views go straight
    /// to the first check, as before. Items: 0 = Start, 1 = Not now.
    /// </summary>
    public interface ICalibrationWelcomeView
    {
        void ShowWelcome();
        void SetWelcomeSelection(int index);
    }
}

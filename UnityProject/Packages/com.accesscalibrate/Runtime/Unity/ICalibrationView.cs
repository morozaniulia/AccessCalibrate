// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
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
        /// <summary>End of calibration (placeholder until the confirmation screen exists).</summary>
        void ShowSummary(IReadOnlyList<ProposedChange> changes);
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
        public void ShowSummary(IReadOnlyList<ProposedChange> changes) { }
        public void Hide() { }
    }
}

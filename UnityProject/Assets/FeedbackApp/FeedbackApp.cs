// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AccessCalibration.FeedbackApp
{
    /// <summary>
    /// The playable feedback build (WebGL and Windows): opens straight into the calibration. After
    /// Apply, the package's summary shows the results with "Copy results" (plain text, nothing sent
    /// anywhere) and "Redo"; its main button says "Done" and starts over. The profile is kept in a
    /// temporary folder, so every launch starts with a fresh calibration.
    /// </summary>
    [RequireComponent(typeof(CalibrationRunner))]
    public sealed class FeedbackApp : MonoBehaviour
    {
        CalibrationRunner _runner;
        bool _restart;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AC_SetState(string state);
#endif
        string _state;
        string _savedContinue;

        void Awake()
        {
            _runner = GetComponent<CalibrationRunner>();
            // ?blur=0: fog-ins without blur (to compare frame times); ?motion=0: reduce motion
            string url = Application.absoluteURL ?? "";
            if (url.Contains("blur=0")) _runner.MotionBlur = false;
            if (url.Contains("motion=0")) _runner.ReduceMotion = true;
            AccessCalibrate.UseStore(new ProfileStore(Path.Combine(Application.temporaryCachePath, "AccessCalibrateFeedback")));
            _savedContinue = ScreenTexts.SummaryContinue;
            ScreenTexts.SummaryContinue = "Done";
            // Done, Not now or Skip the whole setup: a fresh calibration from the Welcome screen.
            _runner.Completed += _ => _restart = true;
        }

        void OnDestroy()
        {
            // The texts are static: give the package its own back (tests run the package view after this app).
            if (_savedContinue != null) ScreenTexts.SummaryContinue = _savedContinue;
        }

        void Start()
        {
            if (!_runner.IsCalibrating) _runner.StartCalibration();
        }

        /// <summary>
        /// Which screen is showing: "welcome", "picker", "test:HoldTest", "signal", "suggestions",
        /// "summary". In WebGL it is also published as window.accessCalibrateState, so a browser test
        /// can follow the calibration.
        /// </summary>
        public string State
        {
            get
            {
                if (_runner.IsChoosingDevice) return "picker";
                if (_runner.IsShowingSummary) return "summary";
                if (_runner.IsWelcoming) return "welcome";
                if (_runner.IsReviewing) return "suggestions";
                var test = _runner.CurrentTest;
                if (test == null) return "idle";
                if (test is ReactionTest r && r.StimulusVisible) return "signal";
                return "test:" + test.GetType().Name;
            }
        }

        void Update()
        {
            if (_restart)
            {
                _restart = false;
                AccessCalibrate.ResetToDefaults();
                _runner.StartCalibration();
            }
            string state = State;
            if (state == _state) return;
            _state = state;
#if UNITY_WEBGL && !UNITY_EDITOR
            AC_SetState(state);
#endif
        }
    }
}

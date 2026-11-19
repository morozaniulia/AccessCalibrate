// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Runs the five calibration tests in order: intro card → test → next intro … → proposal.
    ///
    /// Add it to a GameObject in your first scene. It listens to
    /// <see cref="AccessCalibrate.OnCalibrationRequested"/>, so <c>AccessCalibrate.RunIfFirstLaunch()</c>
    /// starts it. When all tests are done it raises <see cref="ProposalReady"/> with the proposed
    /// settings — nothing is applied until the player confirms them.
    /// </summary>
    public sealed class CalibrationRunner : MonoBehaviour
    {
        [Tooltip("The game's main buttons for the button-access test.")]
        public List<ButtonPrompt> Buttons = ButtonAccessTest.DefaultButtons();

        [Tooltip("Turn off animations (respect the player's 'reduce motion' preference).")]
        public bool ReduceMotion;

        [Tooltip("Create the placeholder UI Toolkit screen automatically.")]
        public bool UseBuiltInView = true;

        /// <summary>Current time, same axis as input event times. Tests may replace it.</summary>
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>Where everything is shown. Set it before the calibration starts to use your own UI.</summary>
        public ICalibrationView View;

        /// <summary>All tests done: the proposed profile (with a reason per setting) and the raw measurements.</summary>
        public event Action<RulesResult, Measurements> ProposalReady;

        public bool IsCalibrating { get; private set; }
        public bool InIntro { get; private set; }
        public CalibrationTest CurrentTest => IsCalibrating ? _tests[_index] : null;
        public IReadOnlyList<CalibrationTest> Tests => _tests;
        public Measurements LastMeasurements { get; private set; }
        public RulesResult LastResult { get; private set; }

        public ReactionTest Reaction { get; private set; }
        public HoldTest Hold { get; private set; }
        public TapTest Taps { get; private set; }
        public StabilityTest Stability { get; private set; }
        public ButtonAccessTest ButtonAccess { get; private set; }

        readonly List<CalibrationTest> _tests = new List<CalibrationTest>();
        int _index;
        InputDeviceKind _preferred = InputDeviceKind.Gamepad;
        InputAction _start, _skip;
        bool _startRequested, _skipRequested;

        void Awake()
        {
            Reaction = GetOrAdd<ReactionTest>();
            Hold = GetOrAdd<HoldTest>();
            Taps = GetOrAdd<TapTest>();
            Stability = GetOrAdd<StabilityTest>();
            ButtonAccess = GetOrAdd<ButtonAccessTest>();
            _tests.AddRange(new CalibrationTest[] { Reaction, Hold, Taps, Stability, ButtonAccess });
            foreach (var t in _tests) t.Finished += OnTestFinished;
        }

        T GetOrAdd<T>() where T : CalibrationTest
        {
            // Explicit check: Unity's "fake null" objects don't work with the ?? operator.
            var existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }

        void OnEnable()
        {
            AccessCalibrate.OnCalibrationRequested += StartCalibration;

            _start = InputDevices.CreateAnyButton("AccessCalibrate/Start");
            _start.performed += OnStartPressed;
            _start.Enable();
            _skip = InputDevices.CreateSkip("AccessCalibrate/Skip");
            _skip.performed += OnSkipPressed;
            _skip.Enable();
        }

        void OnDisable()
        {
            AccessCalibrate.OnCalibrationRequested -= StartCalibration;
            _start?.Dispose();
            _skip?.Dispose();
            _start = _skip = null;
        }

        /// <summary>Starts (or restarts) the whole calibration.</summary>
        public void StartCalibration()
        {
            if (View == null)
                View = UseBuiltInView ? (ICalibrationView)UIToolkitCalibrationView.Create(this) : NullCalibrationView.Instance;

            foreach (var t in _tests) if (t.IsRunning) t.Skip();

            LastMeasurements = new Measurements();
            LastResult = null;
            IsCalibrating = true;
            _index = 0;
            _startRequested = _skipRequested = false;
            ButtonAccess.Buttons = Buttons;
            ShowIntro();
        }

        void ShowIntro()
        {
            InIntro = true;
            var t = _tests[_index];
            t.Clock = Clock;
            t.View = View;
            t.PreferredDevice = _preferred;
            View.ShowIntro(_index + 1, _tests.Count, t.Title, t.Instructions);
        }

        /// <summary>"Ready" on the intro card: start the current test.</summary>
        public void BeginCurrent()
        {
            if (!IsCalibrating || !InIntro) return;
            InIntro = false;
            _tests[_index].Begin();
        }

        /// <summary>Skip the current test (from its intro card or while it runs).</summary>
        public void SkipCurrent()
        {
            if (!IsCalibrating) return;
            InIntro = false;
            _tests[_index].Skip();
        }

        void OnStartPressed(InputAction.CallbackContext ctx)
        {
            if (!IsCalibrating || !InIntro || InputDevices.IsSkipKey(ctx)) return;
            _preferred = InputDevices.KindOf(ctx.control.device);
            _startRequested = true; // handled in Tick, outside the input callback
        }

        void OnSkipPressed(InputAction.CallbackContext ctx)
        {
            if (IsCalibrating) _skipRequested = true;
        }

        /// <summary>Handles queued start/skip requests. Called from Update; tests call it directly.</summary>
        public void Tick()
        {
            if (_skipRequested)
            {
                _skipRequested = _startRequested = false;
                SkipCurrent();
            }
            else if (_startRequested)
            {
                _startRequested = false;
                BeginCurrent();
            }
        }

        void Update() => Tick();

        void OnTestFinished(CalibrationTest test)
        {
            if (!IsCalibrating || test != _tests[_index]) return;
            test.WriteResult(LastMeasurements);
            if (!test.WasSkipped) _preferred = test.Device;

            _index++;
            if (_index < _tests.Count) { ShowIntro(); return; }

            IsCalibrating = false;
            InIntro = false;
            LastResult = RulesEngine.Evaluate(LastMeasurements, AccessCalibrate.Config);
            View.ShowSummary(LastResult.Changes);
            ProposalReady?.Invoke(LastResult, LastMeasurements);
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Runs the calibration: intro card → test (×5) → confirmation screen.
    ///
    /// Add it to a GameObject in your first scene. It listens to
    /// <see cref="AccessCalibrate.OnCalibrationRequested"/>, so <c>AccessCalibrate.RunIfFirstLaunch()</c>
    /// starts it. Nothing is applied until the player presses Accept on the confirmation screen,
    /// and only the proposals the player left switched on are applied.
    /// </summary>
    public sealed class CalibrationRunner : MonoBehaviour
    {
        [Tooltip("The game's main buttons for the button-access test.")]
        public List<ButtonPrompt> Buttons = ButtonAccessTest.DefaultButtons();

        [Tooltip("Turn off animations (respect the player's 'reduce motion' preference).")]
        public bool ReduceMotion;

        [Tooltip("Create the placeholder UI Toolkit screen automatically.")]
        public bool UseBuiltInView = true;

        /// <summary>
        /// On the confirmation screen, keyboard / gamepad presses are ignored for this long, so a
        /// press meant for the last test can't accept or skip by accident.
        /// </summary>
        public double ConfirmationInputDelaySeconds = 1.0;

        /// <summary>Current time, same axis as input event times. Tests may replace it.</summary>
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>Where everything is shown. Set it before the calibration starts to use your own UI.</summary>
        public ICalibrationView View;

        /// <summary>All tests done: the proposed profile (with a reason per setting) and the raw measurements.</summary>
        public event Action<RulesResult, Measurements> ProposalReady;

        /// <summary>The player closed the confirmation screen (Accept or Skip). Carries the profile now in use.</summary>
        public event Action<AccessibilityProfile> Completed;

        public bool IsCalibrating { get; private set; }
        public bool InIntro { get; private set; }
        public bool IsReviewing { get; private set; }
        public CalibrationTest CurrentTest => IsCalibrating ? _tests[_index] : null;
        public IReadOnlyList<CalibrationTest> Tests => _tests;
        public Measurements LastMeasurements { get; private set; }
        public RulesResult LastResult { get; private set; }

        /// <summary>The proposals being reviewed (null outside the confirmation screen).</summary>
        public ProposalReview Review { get; private set; }

        /// <summary>Keyboard / gamepad cursor on the confirmation screen (see <see cref="ICalibrationView.ShowConfirmation"/>).</summary>
        public int Selection { get; private set; }
        public int AcceptIndex => Review != null ? Review.Items.Count : -1;
        public int RedoIndex => AcceptIndex + 1;
        public int SkipIndex => AcceptIndex + 2;

        public ReactionTest Reaction { get; private set; }
        public HoldTest Hold { get; private set; }
        public TapTest Taps { get; private set; }
        public StabilityTest Stability { get; private set; }
        public ButtonAccessTest ButtonAccess { get; private set; }

        readonly List<CalibrationTest> _tests = new List<CalibrationTest>();
        int _index;
        InputDeviceKind _preferred = InputDeviceKind.Gamepad;
        InputAction _start, _skip, _up, _down, _submit;
        bool _startRequested, _skipRequested;
        int _moveRequested;
        bool _submitRequested;
        double _reviewShownAt;

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
            _skip = InputDevices.CreateSkip("AccessCalibrate/Skip");
            _skip.performed += OnSkipPressed;

            _up = CreateButtonAction("AccessCalibrate/Up", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up", "<Keyboard>/upArrow", "<Keyboard>/w");
            _up.performed += _ => { if (IsReviewing) _moveRequested = -1; };
            _down = CreateButtonAction("AccessCalibrate/Down", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down", "<Keyboard>/downArrow", "<Keyboard>/s");
            _down.performed += _ => { if (IsReviewing) _moveRequested = +1; };
            _submit = CreateButtonAction("AccessCalibrate/Submit", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space");
            _submit.performed += _ => { if (IsReviewing) _submitRequested = true; };

            foreach (var a in new[] { _start, _skip, _up, _down, _submit }) a.Enable();
        }

        static InputAction CreateButtonAction(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        void OnDisable()
        {
            AccessCalibrate.OnCalibrationRequested -= StartCalibration;
            foreach (var a in new[] { _start, _skip, _up, _down, _submit }) a?.Dispose();
            _start = _skip = _up = _down = _submit = null;
        }

        void EnsureView()
        {
            if (View == null)
                View = UseBuiltInView ? (ICalibrationView)UIToolkitCalibrationView.Create(this) : NullCalibrationView.Instance;
        }

        // ================================================================ calibration

        /// <summary>Starts (or restarts) the whole calibration.</summary>
        public void StartCalibration()
        {
            EnsureView();
            foreach (var t in _tests) if (t.IsRunning) t.Skip();

            LastMeasurements = new Measurements();
            LastResult = null;
            Review = null;
            IsReviewing = false;
            IsCalibrating = true;
            _index = 0;
            _startRequested = _skipRequested = _submitRequested = false;
            _moveRequested = 0;
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

        /// <summary>Handles queued input requests. Called from Update; tests call it directly.</summary>
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

            if (IsReviewing)
            {
                bool ready = Clock() - _reviewShownAt >= ConfirmationInputDelaySeconds;
                if (ready && _moveRequested != 0) MoveSelection(_moveRequested);
                if (ready && _submitRequested) ActivateSelection();
            }
            _moveRequested = 0;
            _submitRequested = false;
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
            ShowProposal(LastMeasurements);
        }

        // ================================================================ confirmation

        /// <summary>
        /// Evaluates the rules and opens the confirmation screen. Called automatically at the end
        /// of the calibration; can also be called directly with measurements (e.g. from a replay).
        /// </summary>
        public void ShowProposal(Measurements measurements)
        {
            EnsureView();
            LastMeasurements = measurements ?? throw new ArgumentNullException(nameof(measurements));
            LastResult = RulesEngine.Evaluate(measurements, AccessCalibrate.Config);
            Review = new ProposalReview(LastResult);
            IsReviewing = true;
            _reviewShownAt = Clock();
            _moveRequested = 0;
            _submitRequested = false;

            View.ShowConfirmation(Review);
            Selection = AcceptIndex;   // the most common action is one press away
            View.SetConfirmationSelection(Selection);
            ProposalReady?.Invoke(LastResult, measurements);
        }

        /// <summary>Move the keyboard / gamepad cursor (wraps around).</summary>
        public void MoveSelection(int delta)
        {
            if (!IsReviewing) return;
            int count = Review.Items.Count + 3;
            Selection = ((Selection + delta) % count + count) % count;
            View.SetConfirmationSelection(Selection);
        }

        /// <summary>Press the selected row: toggle a proposal, or press Accept / Redo / Skip.</summary>
        public void ActivateSelection()
        {
            if (!IsReviewing) return;
            if (Selection < Review.Items.Count) SetAccepted(Selection, !Review.Items[Selection].Accepted);
            else if (Selection == AcceptIndex) Accept();
            else if (Selection == RedoIndex) Redo();
            else Skip();
        }

        /// <summary>Switch one proposal on or off (mouse / touch on the switch, or keyboard / gamepad).</summary>
        public void SetAccepted(int index, bool accepted)
        {
            if (!IsReviewing || index < 0 || index >= Review.Items.Count) return;
            Review.Items[index].Accepted = accepted;
            View.RefreshConfirmation();
        }

        /// <summary>Save the default settings plus the switched-on proposals, and close the screen.</summary>
        public void Accept()
        {
            if (!IsReviewing) return;
            AccessCalibrate.SetProfile(Review.BuildProfile(AccessCalibrate.DefaultProfile()));
            Close();
        }

        /// <summary>Run the calibration again from the first test.</summary>
        public void Redo()
        {
            if (!IsReviewing) return;
            IsReviewing = false;
            StartCalibration();
        }

        /// <summary>
        /// Close without applying anything. If the player had no saved profile yet, the defaults are
        /// saved so the calibration isn't offered again on every launch; an existing profile is kept.
        /// </summary>
        public void Skip()
        {
            if (!IsReviewing) return;
            if (!AccessCalibrate.HasSavedProfile) AccessCalibrate.SkipCalibration();
            Close();
        }

        void Close()
        {
            IsReviewing = false;
            Review = null;
            View.Hide();
            Completed?.Invoke(AccessCalibrate.Profile);
        }
    }
}

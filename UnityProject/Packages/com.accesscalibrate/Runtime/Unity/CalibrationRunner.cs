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

        [Tooltip("Create the built-in UI Toolkit screens automatically.")]
        public bool UseBuiltInView = true;

        [Tooltip("Look of the built-in screens. Empty = Glass Dark. AccessCalibrate.UseTheme(...) overrides it at runtime.")]
        public CalibrationTheme Theme;

        /// <summary>On the Welcome screen, presses are ignored this long (a press that opened it can't choose).</summary>
        public double WelcomeInputDelaySeconds = 0.3;

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
        /// <summary>On the Welcome screen (only with a view that has one).</summary>
        public bool IsWelcoming { get; private set; }
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
        /// <summary>"Not now"; -1 when there is nothing to suggest (the button is hidden then:
        /// it would do the same as "Continue without changes").</summary>
        public int SkipIndex => Review != null && Review.Items.Count > 0 ? AcceptIndex + 2 : -1;

        public ReactionTest Reaction { get; private set; }
        public HoldTest Hold { get; private set; }
        public TapTest Taps { get; private set; }
        public StabilityTest Stability { get; private set; }
        public ButtonAccessTest ButtonAccess { get; private set; }

        readonly List<CalibrationTest> _tests = new List<CalibrationTest>();
        int _index;
        InputDeviceKind _preferred = InputDeviceKind.Gamepad;
        InputAction _start, _skip, _up, _down, _submit, _toggle;
        bool _startRequested, _skipRequested;
        int _moveRequested;
        bool _submitRequested, _toggleRequested;
        double _reviewShownAt, _welcomeShownAt;

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
            _up.performed += _ => { if (IsReviewing || IsWelcoming) _moveRequested = -1; };
            _down = CreateButtonAction("AccessCalibrate/Down", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down", "<Keyboard>/downArrow", "<Keyboard>/s");
            _down.performed += _ => { if (IsReviewing || IsWelcoming) _moveRequested = +1; };
            // Confirm / apply (A, ✕, Enter) and, on the suggestions screen, a separate
            // on/off for the selected row (X, □, Space) — one button, one job.
            _submit = CreateButtonAction("AccessCalibrate/Submit", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            _submit.performed += _ => { if (IsReviewing || IsWelcoming) _submitRequested = true; };
            _toggle = CreateButtonAction("AccessCalibrate/Toggle", "<Gamepad>/buttonWest", "<Keyboard>/space");
            _toggle.performed += _ => { if (IsReviewing) _toggleRequested = true; };

            foreach (var a in new[] { _start, _skip, _up, _down, _submit, _toggle }) a.Enable();
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
            foreach (var a in new[] { _start, _skip, _up, _down, _submit, _toggle }) a?.Dispose();
            _start = _skip = _up = _down = _submit = _toggle = null;
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

            IsWelcoming = false;
            LastMeasurements = new Measurements();
            LastResult = null;
            Review = null;
            IsReviewing = false;
            IsCalibrating = true;
            _index = 0;
            _startRequested = _skipRequested = _submitRequested = _toggleRequested = false;
            _moveRequested = 0;
            ButtonAccess.Buttons = Buttons;

            if (View is ICalibrationWelcomeView welcome)
            {
                IsWelcoming = true;
                _welcomeShownAt = Clock();
                Selection = 0;
                welcome.ShowWelcome();
                welcome.SetWelcomeSelection(0);
                return;
            }
            ShowIntro();
        }

        /// <summary>Welcome → "Start": go to the first check.</summary>
        public void WelcomeStart()
        {
            if (!IsWelcoming) return;
            IsWelcoming = false;
            ShowIntro();
        }

        /// <summary>
        /// Welcome → "Not now": close without calibrating. On first launch the defaults are saved so
        /// the calibration isn't offered every time; an existing profile is kept.
        /// </summary>
        public void WelcomeNotNow()
        {
            if (!IsWelcoming) return;
            IsWelcoming = false;
            IsCalibrating = false;
            InIntro = false;
            if (!AccessCalibrate.HasSavedProfile) AccessCalibrate.SkipCalibration();
            View.Hide();
            Completed?.Invoke(AccessCalibrate.Profile);
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
            // The device that just started this check is the one it is done with, until a press
            // says otherwise (a check nobody answers must not inherit the previous check's device).
            _tests[_index].PreferredDevice = _preferred;
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
            if (IsWelcoming)
            {
                bool ready = Clock() - _welcomeShownAt >= WelcomeInputDelaySeconds;
                bool skip = _skipRequested;
                int move = _moveRequested;
                bool submit = _submitRequested;
                _skipRequested = _startRequested = _submitRequested = false;
                _moveRequested = 0;
                if (!ready) return;
                if (skip) { WelcomeNotNow(); return; }
                if (move != 0)
                {
                    Selection = Selection == 0 ? 1 : 0;
                    ((ICalibrationWelcomeView)View).SetWelcomeSelection(Selection);
                }
                if (submit) { if (Selection == 0) WelcomeStart(); else WelcomeNotNow(); }
                return;
            }

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
                if (ready && _toggleRequested) ToggleSelection();
                if (ready && _submitRequested) ActivateSelection();
            }
            _moveRequested = 0;
            _submitRequested = _toggleRequested = false;
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
            _submitRequested = _toggleRequested = false;

            // Study mode: log what was measured and proposed, before the player decides.
            if (StudyMode.Enabled) StudyMode.Record(measurements, LastResult.Profile);

            View.ShowConfirmation(Review);
            Selection = AcceptIndex;   // the most common action is one press away
            View.SetConfirmationSelection(Selection);
            ProposalReady?.Invoke(LastResult, measurements);
        }

        /// <summary>Move the keyboard / gamepad cursor (wraps around).</summary>
        public void MoveSelection(int delta)
        {
            if (!IsReviewing) return;
            int count = Review.Items.Count + (Review.Items.Count > 0 ? 3 : 2);   // no "Not now" when empty
            Selection = ((Selection + delta) % count + count) % count;
            View.SetConfirmationSelection(Selection);
        }

        /// <summary>
        /// Confirm (A / ✕ / Enter): on a button, press it; on a suggestion row, apply — switching
        /// rows is <see cref="ToggleSelection"/>'s job.
        /// </summary>
        public void ActivateSelection()
        {
            if (!IsReviewing) return;
            if (Selection < Review.Items.Count || Selection == AcceptIndex) Accept();
            else if (Selection == RedoIndex) Redo();
            else Skip();
        }

        /// <summary>Turn on / off (X / □ / Space): switch the selected row. Does nothing on a button.</summary>
        public void ToggleSelection()
        {
            if (!IsReviewing || Selection >= Review.Items.Count) return;
            SetAccepted(Selection, !Review.Items[Selection].Accepted);
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

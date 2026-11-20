// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Runs the calibration: Welcome → five checks (each starts with its intro and waits for the
    /// player) → suggestions → summary. On every check the player can skip it, go back to the
    /// previous one, skip the whole setup, or say which device they are playing with.
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

        [Tooltip("Look of the built-in screens. Empty = Dark. AccessCalibrate.UseTheme(...) overrides it at runtime.")]
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

        /// <summary>
        /// The calibration is over: the player left the summary after Apply, chose "Not now", or
        /// skipped the whole setup. Carries the profile now in use.
        /// </summary>
        public event Action<AccessibilityProfile> Completed;

        /// <summary>The summary after Apply is showing.</summary>
        public bool IsShowingSummary { get; private set; }
        /// <summary>"What are you playing with?" is open.</summary>
        public bool IsChoosingDevice { get; private set; }
        /// <summary>The choices offered there (Touch only with a touch screen) and the highlighted one.</summary>
        public IReadOnlyList<DeviceChoice> DeviceChoices => _deviceChoices;
        public int DeviceSelection { get; private set; }
        /// <summary>The device the player picked (null: follow the last device used).</summary>
        public DeviceChoice? ChosenDevice { get; private set; }
        /// <summary>The text "Copy results" put on the clipboard last (null before the first copy).</summary>
        public string LastResultsText { get; private set; }
        /// <summary>Pretend a touch screen is there (tests, editor).</summary>
        public bool ForceTouchAvailable;

        public bool IsCalibrating { get; private set; }
        /// <summary>On the Welcome screen (only with a view that has one).</summary>
        public bool IsWelcoming { get; private set; }
        /// <summary>Always false: each check starts at once with its intro (kept for compatibility).</summary>
        public bool InIntro => false;
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
        readonly List<DeviceChoice> _deviceChoices = new List<DeviceChoice>();
        int _index;
        InputDeviceKind _preferred = InputDeviceKind.Gamepad;
        InputAction _skip, _back, _skipAll, _up, _down, _left, _right, _submit, _toggle, _cancel;
        bool _skipRequested, _backRequested, _skipAllRequested, _submitRequested, _toggleRequested, _cancelRequested;
        int _moveRequested, _adjustRequested;
        double _reviewShownAt, _welcomeShownAt, _summaryShownAt;
        bool _applied;

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

            _skip = InputDevices.CreateSkip("AccessCalibrate/Skip");
            _skip.performed += ctx => { if (!IsShiftHeld(ctx)) _skipRequested = true; };
            _back = InputDevices.CreateBack("AccessCalibrate/Back");
            _back.performed += _ => _backRequested = true;
            _skipAll = InputDevices.CreateSkipAll("AccessCalibrate/SkipAll");
            _skipAll.performed += _ => _skipAllRequested = true;

            _up = CreateButtonAction("AccessCalibrate/Up", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up", "<Keyboard>/upArrow", "<Keyboard>/w");
            _up.performed += _ => _moveRequested = -1;
            _down = CreateButtonAction("AccessCalibrate/Down", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down", "<Keyboard>/downArrow", "<Keyboard>/s");
            _down.performed += _ => _moveRequested = +1;
            _left = CreateButtonAction("AccessCalibrate/Left", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left", "<Keyboard>/leftArrow", "<Keyboard>/a");
            _left.performed += _ => _adjustRequested = -1;
            _right = CreateButtonAction("AccessCalibrate/Right", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right", "<Keyboard>/rightArrow", "<Keyboard>/d");
            _right.performed += _ => _adjustRequested = +1;
            // Confirm (A, ✕, Enter) and, on the suggestions screen, a separate on/off for the
            // selected row (X, □, Space) — one button, one job.
            _submit = CreateButtonAction("AccessCalibrate/Submit", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            _submit.performed += ctx => { _submitRequested = true; if (IsWelcoming) _preferred = InputDevices.KindOf(ctx.control.device); };
            _toggle = CreateButtonAction("AccessCalibrate/Toggle", "<Gamepad>/buttonWest", "<Keyboard>/space");
            _toggle.performed += _ => _toggleRequested = true;
            _cancel = CreateButtonAction("AccessCalibrate/Cancel", "<Gamepad>/buttonEast", "<Keyboard>/escape");
            _cancel.performed += _ => _cancelRequested = true;

            foreach (var a in Actions) a.Enable();
        }

        InputAction[] Actions => new[] { _skip, _back, _skipAll, _up, _down, _left, _right, _submit, _toggle, _cancel };

        static bool IsShiftHeld(InputAction.CallbackContext ctx) =>
            ctx.control.device is Keyboard kb && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);

        static InputAction CreateButtonAction(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        void OnDisable()
        {
            AccessCalibrate.OnCalibrationRequested -= StartCalibration;
            foreach (var a in Actions) a?.Dispose();
            _skip = _back = _skipAll = _up = _down = _left = _right = _submit = _toggle = _cancel = null;
        }

        void EnsureView()
        {
            if (View == null)
                View = UseBuiltInView ? (ICalibrationView)UIToolkitCalibrationView.Create(this) : NullCalibrationView.Instance;
        }

        void ClearRequests()
        {
            _skipRequested = _backRequested = _skipAllRequested = _submitRequested = _toggleRequested = _cancelRequested = false;
            _moveRequested = _adjustRequested = 0;
        }

        // ================================================================ calibration

        /// <summary>Starts (or restarts) the whole calibration.</summary>
        public void StartCalibration()
        {
            EnsureView();
            foreach (var t in _tests) if (t.IsRunning) t.Abort();

            IsWelcoming = false;
            IsShowingSummary = false;
            CloseDevicePicker();
            LastMeasurements = new Measurements();
            LastResult = null;
            Review = null;
            IsReviewing = false;
            IsCalibrating = true;
            _index = 0;
            ClearRequests();
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
            StartCurrent();
        }

        /// <summary>Welcome → "Start": the first check.</summary>
        public void WelcomeStart()
        {
            if (!IsWelcoming) return;
            IsWelcoming = false;
            StartCurrent();
        }

        /// <summary>
        /// Welcome → "Not now": close without calibrating. On first launch the defaults are saved so
        /// the calibration isn't offered every time; an existing profile is kept.
        /// </summary>
        public void WelcomeNotNow()
        {
            if (!IsWelcoming) return;
            IsWelcoming = false;
            End();
        }

        /// <summary>Shows the current check (its intro) and starts it: it waits for the player itself.</summary>
        void StartCurrent()
        {
            var t = _tests[_index];
            t.Clock = Clock;
            t.View = View;
            t.PreferredDevice = _preferred;
            t.TouchFilter = _preferred == InputDeviceKind.Touch && View is ICalibrationTouchView touch ? touch.IsInPressArea : (Func<Vector2, bool>)null;
            // On a touch screen there are no buttons to reach.
            if (t is ButtonAccessTest && _preferred == InputDeviceKind.Touch) { View.ShowIntro(_index + 1, _tests.Count, t.Title, t.Instructions); t.Skip(); return; }
            if (t is ReactionTest r) r.FirstSignalDelaySeconds = View is ICalibrationIntroView intro ? intro.IntroSeconds : 0;
            View.ShowIntro(_index + 1, _tests.Count, t.Title, t.Instructions);
            t.Begin();
        }

        /// <summary>Starts the current check if it isn't running (kept for compatibility: checks start by themselves).</summary>
        public void BeginCurrent()
        {
            if (!IsCalibrating || IsWelcoming || _tests[_index].IsRunning) return;
            StartCurrent();
        }

        /// <summary>"Skip this check" (≡ tap / Esc).</summary>
        public void SkipCurrent()
        {
            if (!IsCalibrating || IsWelcoming) return;
            _tests[_index].Skip();
        }

        /// <summary>"Back" (View / Backspace): the previous check again; from the first check, the Welcome screen.</summary>
        public void Back()
        {
            if (!IsCalibrating || IsWelcoming) return;
            _tests[_index].Abort();
            if (_index == 0) { StartCalibration(); return; }
            _index--;
            StartCurrent();
        }

        /// <summary>
        /// "Skip the whole setup" (hold ≡ / Shift + Esc): close without changes, from any check or
        /// the Welcome screen. On first launch the defaults are saved, as with "Not now".
        /// </summary>
        public void SkipAll()
        {
            if (!IsCalibrating && !IsWelcoming) return;
            if (IsCalibrating) _tests[_index].Abort();
            IsWelcoming = false;
            End();
        }

        void End()
        {
            IsCalibrating = false;
            CloseDevicePicker();
            if (!AccessCalibrate.HasSavedProfile) AccessCalibrate.SkipCalibration();
            View.Hide();
            Completed?.Invoke(AccessCalibrate.Profile);
        }

        // ================================================================ device picker

        /// <summary>"Change": opens "What are you playing with?" (needs a view that has it).</summary>
        public void OpenDevicePicker()
        {
            if (!(View is ICalibrationDeviceView picker) || (!IsCalibrating && !IsWelcoming)) return;
            _deviceChoices.Clear();
            _deviceChoices.Add(DeviceChoice.Xbox);
            _deviceChoices.Add(DeviceChoice.PlayStation);
            _deviceChoices.Add(DeviceChoice.KeyboardMouse);
            if (Touchscreen.current != null || ForceTouchAvailable) _deviceChoices.Add(DeviceChoice.Touch);
            var current = CurrentDevice;
            DeviceSelection = Math.Max(0, _deviceChoices.IndexOf(current));
            IsChoosingDevice = true;
            ClearRequests();
            picker.ShowDevicePicker(_deviceChoices, current, DeviceSelection);
        }

        /// <summary>The device in use: the one picked, else the last one that pressed something.</summary>
        public DeviceChoice CurrentDevice
        {
            get
            {
                if (ChosenDevice.HasValue) return ChosenDevice.Value;
                if (View is UIToolkitCalibrationView v)
                    return v.GlyphFamily == UI.GlyphFamily.PlayStation ? DeviceChoice.PlayStation
                         : v.GlyphFamily == UI.GlyphFamily.Keyboard ? DeviceChoice.KeyboardMouse : DeviceChoice.Xbox;
                return _preferred == InputDeviceKind.Gamepad ? DeviceChoice.Xbox : _preferred == InputDeviceKind.Touch ? DeviceChoice.Touch : DeviceChoice.KeyboardMouse;
            }
        }

        /// <summary>Close the picker without changing anything (Cancel / B / Esc).</summary>
        public void CloseDevicePicker()
        {
            if (!IsChoosingDevice) return;
            IsChoosingDevice = false;
            ClearRequests();
            (View as ICalibrationDeviceView)?.HideDevicePicker();
        }

        /// <summary>
        /// "Use this device": glyphs, drawings and the rules follow it, and the current check starts
        /// again with it.
        /// </summary>
        public void ChooseDevice(DeviceChoice choice)
        {
            ChosenDevice = choice;
            _preferred = choice == DeviceChoice.KeyboardMouse ? InputDeviceKind.Keyboard : choice == DeviceChoice.Touch ? InputDeviceKind.Touch : InputDeviceKind.Gamepad;
            (View as ICalibrationDeviceView)?.UseDevice(choice);
            CloseDevicePicker();
            if (IsCalibrating && !IsWelcoming)
            {
                _tests[_index].Abort();
                StartCurrent();
            }
        }

        // ================================================================ input

        /// <summary>Handles queued input requests. Called from Update; tests call it directly.</summary>
        public void Tick()
        {
            if (IsChoosingDevice)
            {
                int move = _moveRequested; bool submit = _submitRequested, cancel = _cancelRequested || _backRequested;
                ClearRequests();
                if (move != 0 && _deviceChoices.Count > 0)
                {
                    DeviceSelection = ((DeviceSelection + move) % _deviceChoices.Count + _deviceChoices.Count) % _deviceChoices.Count;
                    (View as ICalibrationDeviceView)?.SetDeviceSelection(DeviceSelection);
                }
                if (submit) ChooseDevice(_deviceChoices[DeviceSelection]);
                else if (cancel) CloseDevicePicker();
                return;
            }

            if (IsWelcoming)
            {
                bool ready = Clock() - _welcomeShownAt >= WelcomeInputDelaySeconds;
                bool skipAll = _skipAllRequested, notNow = _skipRequested;
                int move = _moveRequested;
                bool submit = _submitRequested;
                ClearRequests();
                if (!ready) return;
                if (skipAll || notNow) { WelcomeNotNow(); return; }
                if (move != 0)
                {
                    Selection = Selection == 0 ? 1 : 0;
                    ((ICalibrationWelcomeView)View).SetWelcomeSelection(Selection);
                }
                if (submit) { if (Selection == 0) WelcomeStart(); else WelcomeNotNow(); }
                return;
            }

            if (IsCalibrating)
            {
                bool skipAll = _skipAllRequested, back = _backRequested, skip = _skipRequested;
                ClearRequests();
                if (skipAll) SkipAll();
                else if (back) Back();
                else if (skip) SkipCurrent();
                return;
            }

            if (IsReviewing)
            {
                bool ready = Clock() - _reviewShownAt >= ConfirmationInputDelaySeconds;
                int move = _moveRequested, adjust = _adjustRequested;
                bool toggle = _toggleRequested, submit = _submitRequested;
                ClearRequests();
                if (!ready) return;
                if (move != 0) MoveSelection(move);
                if (adjust != 0) AdjustSelection(adjust);
                if (toggle) ToggleSelection();
                if (submit) ActivateSelection();
                return;
            }

            if (IsShowingSummary)
            {
                bool ready = Clock() - _summaryShownAt >= ConfirmationInputDelaySeconds;
                int move = _moveRequested != 0 ? _moveRequested : _adjustRequested;
                bool submit = _submitRequested;
                ClearRequests();
                if (!ready) return;
                if (move != 0) { Selection = ((Selection + move) % 3 + 3) % 3; (View as ICalibrationSummaryView)?.SetSummarySelection(Selection); }
                if (submit) ActivateSummary();
                return;
            }
            ClearRequests();
        }

        void Update() => Tick();

        void OnTestFinished(CalibrationTest test)
        {
            if (!IsCalibrating || test != _tests[_index]) return;
            test.WriteResult(LastMeasurements);
            if (!test.WasSkipped && !ChosenDevice.HasValue) _preferred = test.Device;

            _index++;
            if (_index < _tests.Count) { StartCurrent(); return; }

            IsCalibrating = false;
            ShowProposal(LastMeasurements);
        }

        // ================================================================ suggestions

        /// <summary>
        /// Evaluates the rules and opens the suggestions screen. Called automatically at the end of
        /// the calibration; can also be called directly with measurements (e.g. from a replay).
        /// </summary>
        public void ShowProposal(Measurements measurements)
        {
            EnsureView();
            LastMeasurements = measurements ?? throw new ArgumentNullException(nameof(measurements));
            LastResult = RulesEngine.Evaluate(measurements, AccessCalibrate.Config);
            Review = new ProposalReview(LastResult);
            IsReviewing = true;
            IsShowingSummary = false;
            _reviewShownAt = Clock();
            ClearRequests();

            // Study mode: log what was measured and proposed, before the player decides.
            if (StudyMode.Enabled) StudyMode.Record(measurements, LastResult.Profile);

            View.ShowConfirmation(Review);
            Selection = Review.Items.Count > 0 ? 0 : AcceptIndex;   // first suggestion (its details show), or Continue
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

        /// <summary>← / →: adjust the selected suggestion's value (timing windows, aim assist).</summary>
        public void AdjustSelection(int delta) => Adjust(Selection, delta);

        /// <summary>Adjust one suggestion's value one step; it is what gets applied and saved.</summary>
        public void Adjust(int index, int delta)
        {
            if (!IsReviewing || index < 0 || index >= Review.Items.Count) return;
            if (Review.Items[index].Adjust(delta)) View.RefreshConfirmation();
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

        /// <summary>Save the default settings plus the switched-on proposals (with their adjusted values), then show the summary.</summary>
        public void Accept()
        {
            if (!IsReviewing) return;
            AccessCalibrate.SetProfile(Review.BuildProfile(AccessCalibrate.DefaultProfile()));
            IsReviewing = false;
            _applied = true;
            if (View is ICalibrationSummaryView summary)
            {
                IsShowingSummary = true;
                _summaryShownAt = Clock();
                Selection = 0;
                ClearRequests();
                summary.ShowSummary(LastMeasurements, Review, true);
                summary.SetSummarySelection(0);
                return;
            }
            Close();
        }

        /// <summary>Run the calibration again from the Welcome screen.</summary>
        public void Redo()
        {
            if (!IsReviewing && !IsShowingSummary) return;
            IsReviewing = false;
            IsShowingSummary = false;
            StartCalibration();
        }

        /// <summary>
        /// "Not now": close without applying anything. If the player had no saved profile yet, the
        /// defaults are saved so the calibration isn't offered again on every launch.
        /// </summary>
        public void Skip()
        {
            if (!IsReviewing) return;
            if (!AccessCalibrate.HasSavedProfile) AccessCalibrate.SkipCalibration();
            _applied = false;
            Close();
        }

        // ================================================================ summary

        void ActivateSummary()
        {
            if (Selection == 0) ContinueFromSummary();
            else if (Selection == 1) CopyResults();
            else Redo();
        }

        /// <summary>"Continue to game": close the summary.</summary>
        public void ContinueFromSummary()
        {
            if (!IsShowingSummary) return;
            IsShowingSummary = false;
            Close();
        }

        /// <summary>"Copy results": the plain-text results on the clipboard (nothing is sent anywhere).</summary>
        public void CopyResults()
        {
            LastResultsText = ResultsText.Build(LastMeasurements, Review, _applied, ResultsText.PlatformName());
            Clipboard.Copy(LastResultsText, ok => (View as ICalibrationSummaryView)?.ShowCopyResult(ok));
        }

        void Close()
        {
            IsReviewing = false;
            IsShowingSummary = false;
            View.Hide();
            Completed?.Invoke(AccessCalibrate.Profile);
            Review = null;
        }
    }
}

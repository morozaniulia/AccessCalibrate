// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using AccessCalibration.Core;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Test 2 — hold. "Hold a button as long as it's comfortable", at most 5 s. 2 trials.
    /// Result = the longest hold. Duration = release event time − press event time.
    /// </summary>
    public sealed class HoldTest : CalibrationTest
    {
        public int Trials = 2;
        public double MaxHoldSeconds = 5.0;
        /// <summary>No press this long after a trial starts → the trial counts as 0 s.</summary>
        public double WaitForPressSeconds = 6.0;
        public double PauseBetweenTrialsSeconds = 1.0;

        enum State { WaitingForRelease, WaitingForPress, Holding, Pause }

        State _state;
        double _stateSince;
        double _pressedAt;
        int _pressCount;
        readonly List<double> _holds = new List<double>();
        InputAction _button;

        // Read-only progress for screens (no effect on the measurement).
        /// <summary>The trial in progress, 1-based.</summary>
        public int TrialNumber => Math.Min(_holds.Count + 1, Trials);
        public bool IsHolding => IsRunning && _state == State.Holding;
        /// <summary>Seconds held so far in this trial (the last result between trials).</summary>
        public double CurrentHoldSeconds =>
            IsHolding ? Math.Min(Math.Max(0, Clock() - _pressedAt), MaxHoldSeconds)
                      : _holds.Count > 0 ? _holds[_holds.Count - 1] : 0;

        public override string Title => CalibrationTexts.HoldTitle;
        protected override string LetGoText => CalibrationTexts.HoldLetGo;
        public override string Instructions => CalibrationTexts.HoldInstructions;

        protected override void OnBegin(double now)
        {
            _holds.Clear();
            _pressCount = 0;
            _button = InputDevices.CreateAnyButton("AccessCalibrate/Hold");
            _button.performed += OnPress;
            _button.canceled += OnRelease;
            _button.Enable();
            StartTrial(now);
        }

        void StartTrial(double now)
        {
            _stateSince = now;
            if (InputDevices.AnyButtonHeld())
            {
                // Still holding from the previous trial (e.g. past the 5 s cap): a new press can't
                // be seen until the button goes up.
                _state = State.WaitingForRelease;
                View.SetStatus(CalibrationTexts.HoldLetGo);
                return;
            }
            _state = State.WaitingForPress;
            View.SetStatus(string.Format(CalibrationTexts.HoldTrial, _holds.Count + 1, Trials)
                           + " · " + CalibrationTexts.HoldWaiting);
        }

        void OnPress(InputAction.CallbackContext ctx)
        {
            if (!IsRunning || _state != State.WaitingForPress || Ignored(ctx)) return;
            Remember(ctx);
            _pressCount++;
            _pressedAt = ctx.time;
            _state = State.Holding;
            View.ShowStimulus(true);
            View.SetStatus(CalibrationTexts.HoldHolding);
        }

        void OnRelease(InputAction.CallbackContext ctx)
        {
            if (!IsRunning || _state != State.Holding) return;
            RecordHold(Math.Min(ctx.time - _pressedAt, MaxHoldSeconds), ctx.time);
        }

        protected override void OnTick(double now)
        {
            switch (_state)
            {
                case State.Holding when now - _pressedAt >= MaxHoldSeconds:
                    View.PlayCue();
                    View.SetStatus(CalibrationTexts.HoldMaxReached);
                    RecordHold(MaxHoldSeconds, now);
                    break;
                case State.WaitingForRelease when !InputDevices.AnyButtonHeld():
                    StartTrial(now);
                    break;
                case State.WaitingForPress when now - _stateSince >= WaitForPressSeconds:
                    RecordHold(0, now);
                    break;
                case State.Pause when now - _stateSince >= PauseBetweenTrialsSeconds:
                    StartTrial(now);
                    break;
            }
        }

        void RecordHold(double seconds, double now)
        {
            _holds.Add(Math.Max(0, seconds));
            View.ShowStimulus(false);
            if (_holds.Count >= Trials) { Finish(); return; }
            _state = State.Pause;
            _stateSince = now;
        }

        protected override void OnStop()
        {
            if (_button == null) return;
            _button.performed -= OnPress;
            _button.canceled -= OnRelease;
            Retire(_button);
            _button = null;
        }

        public override void WriteResult(Measurements m)
        {
            m.Hold = new HoldMeasurement
            {
                Device = Device,
                RawHoldsSeconds = new List<double>(_holds),
                MaxHoldSeconds = _holds.Count > 0 ? _holds.Max() : 0,
                // Never pressed at all → we can't tell why, so the result is only a hint.
                Confidence = WasSkipped ? Confidence.Skipped
                           : _pressCount == 0 ? Confidence.Uncertain
                           : Confidence.Ok,
            };
        }
    }
}

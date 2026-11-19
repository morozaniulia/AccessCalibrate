// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Test 4 — stability. Bring a cursor into a circle and keep it there as still as is
    /// comfortable for 5 s (left stick or mouse).
    ///
    /// Phases: wait until the cursor first enters the circle → 0.5 s to settle → 5 s of measuring.
    /// Result = spread: 2D standard deviation around the player's OWN average position, as a
    /// fraction of the circle radius. Holding still a bit off-centre is not unsteadiness, so the
    /// offset is stored separately (MeanOffset, information only, no rule).
    ///
    /// The cursor is never moved by code (no WarpCursorPosition — it doesn't work in WebGL):
    /// the player brings it into the circle. No stick and no mouse (keyboard-only, touch) → skipped.
    ///
    /// Stick: the circle sits off-centre (default 0.5 to the right), so the player has to hold a
    /// partial deflection — at rest a stick sits at the centre by itself and would measure nothing.
    /// Raw (unprocessed) stick values are used so the device deadzone doesn't hide small movements.
    /// </summary>
    public sealed class StabilityTest : CalibrationTest
    {
        public enum Mode { None, Stick, Mouse }
        public enum Phase { WaitingToEnter, Settling, Measuring, Done }

        /// <summary>Give up if the cursor never reaches the circle within this time (result Uncertain).</summary>
        public double WaitToEnterSeconds = 10.0;
        public double SettleSeconds = 0.5;
        public double MeasureSeconds = 5.0;

        /// <summary>Stick mode: circle centre and radius in stick units (−1…1).</summary>
        public Vector2 StickTarget = new Vector2(0.5f, 0f);
        public float StickRadius = 0.25f;

        /// <summary>Mouse mode: circle radius as a fraction of the screen height (centre = screen centre).</summary>
        public float MouseRadiusFraction = 0.08f;

        public Mode ActiveMode { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int SampleCount => _samples.Count;

        // Read-only progress for screens (no effect on the measurement).
        /// <summary>Seconds of measuring left (the full time before measuring starts).</summary>
        public double SecondsLeft =>
            CurrentPhase == Phase.Measuring ? System.Math.Max(0, MeasureSeconds - (Clock() - _phaseStart))
            : CurrentPhase == Phase.Done ? 0 : MeasureSeconds;

        double _phaseStart;
        Vector2 _target;
        float _radius;
        double _spread, _meanOffset;
        readonly List<(double x, double y)> _samples = new List<(double x, double y)>();

        public override string Title => CalibrationTexts.StabilityTitle;
        public override string Instructions =>
            ActiveMode == Mode.Mouse ? CalibrationTexts.StabilityInstructionsMouse
                                     : CalibrationTexts.StabilityInstructionsStick;

        /// <summary>Which mode the test would use with the devices connected right now.</summary>
        public Mode PickMode()
        {
            bool gamepad = Gamepad.current != null;
            bool mouse = Mouse.current != null;
            switch (PreferredDevice)
            {
                case InputDeviceKind.Gamepad: return gamepad ? Mode.Stick : mouse ? Mode.Mouse : Mode.None;
                case InputDeviceKind.Mouse:
                case InputDeviceKind.Keyboard: return mouse ? Mode.Mouse : gamepad ? Mode.Stick : Mode.None;
                default: return Mode.None; // touch: no stick, no mouse
            }
        }

        protected override void OnBegin(double now)
        {
            _samples.Clear();
            _spread = _meanOffset = 0;
            ActiveMode = PickMode();

            if (ActiveMode == Mode.None)
            {
                Skip();
                return;
            }

            if (ActiveMode == Mode.Stick)
            {
                Device = InputDevices.KindOf(Gamepad.current);
                _target = StickTarget;
                _radius = StickRadius;
            }
            else
            {
                Device = InputDeviceKind.Mouse;
                _target = new Vector2(Screen.width / 2f, Screen.height / 2f);
                _radius = Mathf.Max(1f, Screen.height * MouseRadiusFraction);
            }

            SetPhase(Phase.WaitingToEnter, now);
            View.ShowTest(Title, Instructions);
            View.ShowStabilityArena(true);
            View.SetStatus(CalibrationTexts.StabilityBringIn);
        }

        void SetPhase(Phase phase, double now)
        {
            CurrentPhase = phase;
            _phaseStart = now;
        }

        Vector2 ReadPosition()
        {
            if (ActiveMode == Mode.Stick && Gamepad.current != null)
                return Gamepad.current.leftStick.ReadUnprocessedValue();
            if (ActiveMode == Mode.Mouse && Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return _target;
        }

        protected override void OnTick(double now)
        {
            Vector2 p = ReadPosition();
            Vector2 offset = (p - _target) / _radius;
            View.SetCursor(offset.x, offset.y);
            bool inside = offset.sqrMagnitude <= 1f;
            double inPhase = now - _phaseStart;

            switch (CurrentPhase)
            {
                case Phase.WaitingToEnter:
                    if (inside)
                    {
                        SetPhase(Phase.Settling, now);
                        View.SetStatus(CalibrationTexts.StabilityGetReady);
                    }
                    else if (inPhase >= WaitToEnterSeconds)
                    {
                        Complete(); // never reached the circle: no data, result Uncertain
                    }
                    break;

                case Phase.Settling:
                    if (inPhase >= SettleSeconds)
                    {
                        SetPhase(Phase.Measuring, now);
                        View.SetStatus(CalibrationTexts.StabilityMeasuring);
                        _samples.Add((p.x, p.y));
                    }
                    break;

                case Phase.Measuring:
                    if (inPhase <= MeasureSeconds) _samples.Add((p.x, p.y));
                    else Complete();
                    break;
            }
        }

        void Complete()
        {
            if (_samples.Count > 0)
            {
                _spread = SignalProcessing.Spread2D(_samples, _radius);
                _meanOffset = SignalProcessing.MeanOffset(_samples, _target.x, _target.y, _radius);
            }
            CurrentPhase = Phase.Done;
            View.ShowStabilityArena(false);
            Finish();
        }

        protected override void OnStop() => View.ShowStabilityArena(false);

        public override void WriteResult(Measurements m)
        {
            m.Stability = new StabilityMeasurement
            {
                Device = Device,
                Spread = _spread,
                MeanOffset = _meanOffset,
                Confidence = WasSkipped ? Confidence.Skipped
                           : _samples.Count < 10 ? Confidence.Uncertain   // too few frames to trust
                           : Confidence.Ok,
            };
        }
    }
}

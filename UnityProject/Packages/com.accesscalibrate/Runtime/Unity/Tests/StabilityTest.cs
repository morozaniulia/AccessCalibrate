// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Test 4 — stability. Keep a cursor inside a circle for 5 s (left stick or mouse).
    /// Result (v1) = RMS distance from the circle centre, in units of the circle radius.
    /// No stick and no mouse (keyboard-only, touch) → the test is skipped automatically.
    ///
    /// Stick: the circle sits off-centre (default 0.5 to the right), so the player has to hold a
    /// partial deflection — at rest a stick sits at the centre by itself and would measure nothing.
    /// Raw (unprocessed) stick values are used so the device deadzone doesn't hide small movements.
    /// </summary>
    public sealed class StabilityTest : CalibrationTest
    {
        public enum Mode { None, Stick, Mouse }

        public double SettleSeconds = 1.0;
        public double MeasureSeconds = 5.0;

        /// <summary>Stick mode: circle centre and radius in stick units (−1…1).</summary>
        public Vector2 StickTarget = new Vector2(0.5f, 0f);
        public float StickRadius = 0.25f;

        /// <summary>Mouse mode: circle radius as a fraction of the screen height (centre = screen centre).</summary>
        public float MouseRadiusFraction = 0.08f;

        public Mode ActiveMode { get; private set; }
        public int SampleCount => _samples.Count;

        double _startedAt;
        Vector2 _target;
        float _radius;
        double _rms;
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
            _rms = 0;
            _startedAt = now;
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
                Mouse.current.WarpCursorPosition(_target);
            }

            View.ShowTest(Title, Instructions);
            View.ShowStabilityArena(true);
            View.SetStatus(CalibrationTexts.StabilityGetReady);
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

            double t = now - _startedAt;
            if (t < SettleSeconds) return;

            if (t <= SettleSeconds + MeasureSeconds)
            {
                if (_samples.Count == 0) View.SetStatus(CalibrationTexts.StabilityMeasuring);
                _samples.Add((p.x, p.y));
                return;
            }

            _rms = _samples.Count > 0
                ? SignalProcessing.RmsDeviation(_samples, _target.x, _target.y, _radius)
                : 0;
            View.ShowStabilityArena(false);
            Finish();
        }

        protected override void OnStop() => View.ShowStabilityArena(false);

        public override void WriteResult(Measurements m)
        {
            m.Stability = new StabilityMeasurement
            {
                Device = Device,
                RmsDeviation = _rms,
                Confidence = WasSkipped ? Confidence.Skipped
                           : _samples.Count < 10 ? Confidence.Uncertain   // too few frames to trust
                           : Confidence.Ok,
            };
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.Core;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Test 3 — repeated presses. "Press as many times as is comfortable" for 5 s.
    /// The 5 s window starts at the first press (so reaction time doesn't count) and presses
    /// are counted by their event time. Result = presses per second.
    /// </summary>
    public sealed class TapTest : CalibrationTest
    {
        public double WindowSeconds = 5.0;
        /// <summary>No first press this long after the start → the test ends (result Uncertain).</summary>
        public double WaitForFirstPressSeconds = 6.0;

        double _startedAt;
        double _windowStart = double.NaN;
        int _count;
        InputAction _button;

        public override string Title => CalibrationTexts.TapsTitle;
        public override string Instructions => CalibrationTexts.TapsInstructions;

        public bool IsCounting => IsRunning && !double.IsNaN(_windowStart);

        protected override void OnBegin(double now)
        {
            _startedAt = now;
            _windowStart = double.NaN;
            _count = 0;
            _button = InputDevices.CreateAnyButton("AccessCalibrate/Taps");
            _button.performed += OnPress;
            _button.Enable();
            View.SetStatus(CalibrationTexts.TapsWaiting);
        }

        void OnPress(InputAction.CallbackContext ctx)
        {
            if (!IsRunning || InputDevices.IsSkipKey(ctx)) return;
            if (double.IsNaN(_windowStart))
            {
                Remember(ctx);
                _windowStart = ctx.time;
                View.ShowStimulus(true);
            }
            if (ctx.time - _windowStart <= WindowSeconds)
            {
                _count++;
                View.SetStatus(string.Format(CalibrationTexts.TapsCounting, _count));
            }
        }

        protected override void OnTick(double now)
        {
            if (!double.IsNaN(_windowStart) && now - _windowStart >= WindowSeconds)
            {
                View.PlayCue();
                Finish();
            }
            else if (double.IsNaN(_windowStart) && now - _startedAt >= WaitForFirstPressSeconds)
            {
                Finish();
            }
        }

        protected override void OnStop()
        {
            if (_button == null) return;
            _button.performed -= OnPress;
            Retire(_button);
            _button = null;
        }

        public override void WriteResult(Measurements m)
        {
            m.Taps = new TapMeasurement
            {
                Device = Device,
                PressCount = _count,
                DurationSeconds = WindowSeconds,
                Confidence = WasSkipped ? Confidence.Skipped
                           : _count == 0 ? Confidence.Uncertain
                           : Confidence.Ok,
            };
        }
    }
}

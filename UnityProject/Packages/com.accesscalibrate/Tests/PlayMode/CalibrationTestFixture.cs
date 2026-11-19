// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>
    /// Shared setup: a fresh, fake input system (InputTestFixture) and a controllable clock.
    ///
    /// Time works like this in every test: <see cref="Now"/> is what the calibration's Clock
    /// returns (when a stimulus is shown), and simulated presses carry their own explicit event
    /// time. Both are on the same axis, exactly like Time.realtimeSinceStartupAsDouble and
    /// InputAction.CallbackContext.time in a real game.
    /// </summary>
    public abstract class CalibrationTestFixture : InputTestFixture
    {
        protected double Now;
        protected GameObject Host;
        protected Keyboard Keyboard;

        public override void Setup()
        {
            base.Setup();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Now = 10.0;
            Host = new GameObject("CalibrationTestHost");
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(Host);
            base.TearDown();
        }

        protected T AddTest<T>() where T : CalibrationTest
        {
            var t = Host.AddComponent<T>();
            t.Clock = () => Now;
            return t;
        }

        /// <summary>Moves the clock and lets the test react (what Update does each frame).</summary>
        protected void TickAt(CalibrationTest test, double time)
        {
            Now = time;
            test.Tick(Now);
        }

        /// <summary>A press and release whose event times are exactly <paramref name="down"/> and <paramref name="up"/>.</summary>
        protected void Tap(UnityEngine.InputSystem.Controls.ButtonControl button, double down, double up)
        {
            Press(button, time: down);
            Release(button, time: up);
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Base class for the five calibration tests.
    ///
    /// Rules shared by all tests: nobody can "fail" a test; every test can be skipped; timing is
    /// measured from the input EVENT time (<c>CallbackContext.time</c>), never from Update().
    /// Stimulus moments are stamped with <see cref="Clock"/>, which is on the same time axis
    /// (<c>Time.realtimeSinceStartupAsDouble</c>).
    /// </summary>
    public abstract class CalibrationTest : MonoBehaviour
    {
        /// <summary>Current time, same axis as input event times. Tests may replace it.</summary>
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>Where instructions, stimuli and cues are shown. Never null.</summary>
        public ICalibrationView View = NullCalibrationView.Instance;

        /// <summary>The device used in earlier tests; tests may use it to pick a mode.</summary>
        public InputDeviceKind PreferredDevice = InputDeviceKind.Gamepad;

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public bool WasSkipped { get; private set; }

        /// <summary>The device this test's input actually came from.</summary>
        public InputDeviceKind Device { get; protected set; }

        public event Action<CalibrationTest> Finished;

        public abstract string Title { get; }
        public abstract string Instructions { get; }

        /// <summary>Starts (or restarts) the test.</summary>
        public void Begin()
        {
            IsRunning = true;
            IsFinished = false;
            WasSkipped = false;
            Device = PreferredDevice;
            View.ShowTest(Title, Instructions);
            OnBegin(Clock());
        }

        /// <summary>The player chose to skip. The result is marked Skipped and the rules ignore it.</summary>
        public void Skip()
        {
            WasSkipped = true;
            if (IsRunning) Finish();
            else { IsFinished = true; Finished?.Invoke(this); }
        }

        /// <summary>Advances timers. Called from Update; tests call it directly with a fixed time.</summary>
        public void Tick(double now)
        {
            if (IsRunning) OnTick(now);
        }

        /// <summary>Copies this test's result into <paramref name="m"/>.</summary>
        public abstract void WriteResult(Measurements m);

        protected abstract void OnBegin(double now);
        protected abstract void OnTick(double now);
        /// <summary>Disable and dispose input actions.</summary>
        protected abstract void OnStop();

        protected void Finish()
        {
            if (!IsRunning) return;
            IsRunning = false;
            IsFinished = true;
            OnStop();
            View.ShowStimulus(false);
            Finished?.Invoke(this);
        }

        protected virtual void Update()
        {
            DisposeRetired();
            Tick(Clock());
        }

        protected virtual void OnDisable()
        {
            if (IsRunning) { IsRunning = false; OnStop(); }
            DisposeRetired();
        }

        readonly System.Collections.Generic.List<InputAction> _retired = new System.Collections.Generic.List<InputAction>();

        /// <summary>
        /// Stops an action right away but disposes it later, outside any input callback
        /// (disposing an action from inside its own callback isn't safe).
        /// </summary>
        protected void Retire(InputAction action)
        {
            if (action == null) return;
            action.Disable();
            _retired.Add(action);
        }

        /// <summary>Disposes actions retired earlier. Called from Update and OnDisable.</summary>
        public void DisposeRetired()
        {
            foreach (var a in _retired) a.Dispose();
            _retired.Clear();
        }

        protected void Remember(InputAction.CallbackContext ctx) => Device = InputDevices.KindOf(ctx.control.device);
    }

    /// <summary>Input helpers shared by the tests.</summary>
    public static class InputDevices
    {
        public static InputDeviceKind KindOf(InputDevice device)
        {
            switch (device)
            {
                case Touchscreen _: return InputDeviceKind.Touch;
                case Mouse _: return InputDeviceKind.Mouse;   // before Pointer: Mouse is a Pointer
                case Keyboard _: return InputDeviceKind.Keyboard;
                case Pen _: return InputDeviceKind.Touch;
                default: return InputDeviceKind.Gamepad;      // gamepads, joysticks, adaptive controllers
            }
        }

        /// <summary>
        /// "Press any button": face buttons, shoulders, triggers, stick clicks, any key,
        /// mouse buttons, touch, joystick trigger.
        /// </summary>
        public static InputAction CreateAnyButton(string name)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var path in new[]
            {
                "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth",
                "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger",
                "<Gamepad>/leftStickPress", "<Gamepad>/rightStickPress",
                "<Keyboard>/anyKey",
                "<Mouse>/leftButton", "<Mouse>/rightButton",
                "<Touchscreen>/press",
                "<Joystick>/trigger",
            })
                a.AddBinding(path);
            return a;
        }

        /// <summary>The skip shortcut (Escape / gamepad Select). Never counted as a test press.</summary>
        public static InputAction CreateSkip(string name)
        {
            var a = new InputAction(name, InputActionType.Button);
            a.AddBinding("<Keyboard>/escape");
            a.AddBinding("<Gamepad>/select");
            return a;
        }

        /// <summary>True if this "any key" press was actually the skip key.</summary>
        public static bool IsSkipKey(InputAction.CallbackContext ctx)
        {
            return ctx.control.device is Keyboard kb && kb.escapeKey.isPressed;
        }
    }
}

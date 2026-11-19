// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AccessCalibration.Demo
{
    /// <summary>The demo's controls, created in code (gamepad + keyboard).</summary>
    public sealed class DemoInput : IDisposable
    {
        public readonly InputAction Move;     // left stick / WASD / arrows
        public readonly InputAction Action;   // A / Space  — jump, tap
        public readonly InputAction Use;      // X / E      — lever
        public readonly InputAction SkipQte;  // Y / Tab    — skip a quick-time event (when allowed)
        public readonly InputAction Redo;     // Start / F1 — redo calibration

        public DemoInput()
        {
            Move = new InputAction("Demo/Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            Action = Button("Demo/Action", "<Gamepad>/buttonSouth", "<Keyboard>/space");
            Use = Button("Demo/Use", "<Gamepad>/buttonWest", "<Keyboard>/e");
            SkipQte = Button("Demo/SkipQte", "<Gamepad>/buttonNorth", "<Keyboard>/tab");
            Redo = Button("Demo/Redo", "<Gamepad>/start", "<Keyboard>/f1");

            foreach (var a in All) a.Enable();
        }

        InputAction[] All => new[] { Move, Action, Use, SkipQte, Redo };

        static InputAction Button(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        /// <summary>
        /// Movement with the profile's stick deadzone. The stick is read UNPROCESSED so the
        /// profile's deadzone replaces the device default instead of stacking on top of it.
        /// Keyboard input is digital and has no deadzone.
        /// </summary>
        public Vector2 ReadMove(float stickDeadzone)
        {
            Vector2 keys = Move.ReadValue<Vector2>();
            Vector2 stick = Gamepad.current != null
                ? DemoMechanics.ApplyDeadzone(Gamepad.current.leftStick.ReadUnprocessedValue(), stickDeadzone)
                : Vector2.zero;
            Vector2 v = keys + stick;
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        public void Dispose()
        {
            foreach (var a in All) a.Dispose();
        }
    }
}

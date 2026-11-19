// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using AccessCalibration.Core;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>One of the game's main buttons, with the input paths that count as pressing it.</summary>
    [Serializable]
    public sealed class ButtonPrompt
    {
        public string Name;
        public List<string> Bindings = new List<string>();

        public ButtonPrompt() { }
        public ButtonPrompt(string name, params string[] bindings)
        {
            Name = name;
            Bindings = bindings.ToList();
        }
    }

    /// <summary>
    /// Test 5 — button access. Each of the game's main buttons (configurable) is shown for up to
    /// 5 s. Pressed → next one immediately. Not pressed in time → it goes on the remap list.
    /// </summary>
    public sealed class ButtonAccessTest : CalibrationTest
    {
        public double SecondsPerButton = 5.0;

        /// <summary>The game's main buttons. Defaults are only an example; games set their own.</summary>
        public List<ButtonPrompt> Buttons = DefaultButtons();

        public static List<ButtonPrompt> DefaultButtons() => new List<ButtonPrompt>
        {
            new ButtonPrompt("Sărit (A / Spațiu)", "<Gamepad>/buttonSouth", "<Keyboard>/space"),
            new ButtonPrompt("Acțiune (X / E)", "<Gamepad>/buttonWest", "<Keyboard>/e"),
            new ButtonPrompt("Umăr stânga (LB / Q)", "<Gamepad>/leftShoulder", "<Keyboard>/q"),
            new ButtonPrompt("Umăr dreapta (RB / R)", "<Gamepad>/rightShoulder", "<Keyboard>/r"),
        };

        int _index;
        double _shownAt;
        bool _anyPressed;
        bool _pressedCurrent;
        InputAction _current;
        readonly List<string> _unpressed = new List<string>();

        public override string Title => CalibrationTexts.ButtonsTitle;
        public override string Instructions => CalibrationTexts.ButtonsInstructions;

        /// <summary>The button currently asked for (null when done). Exposed for tests.</summary>
        public ButtonPrompt CurrentPrompt => IsRunning && _index < Buttons.Count ? Buttons[_index] : null;

        protected override void OnBegin(double now)
        {
            _unpressed.Clear();
            _anyPressed = false;
            _index = -1;
            Next(now);
        }

        void Next(double now)
        {
            DisposeCurrent();
            _index++;
            if (_index >= Buttons.Count)
            {
                View.SetPrompt("");
                Finish();
                return;
            }

            var prompt = Buttons[_index];
            _shownAt = now;
            _pressedCurrent = false;
            _current = new InputAction("AccessCalibrate/Button/" + prompt.Name, InputActionType.Button);
            foreach (var path in prompt.Bindings) _current.AddBinding(path);
            _current.performed += OnPress;
            _current.Enable();

            View.SetPrompt(string.Format(CalibrationTexts.ButtonsPrompt, prompt.Name));
            View.SetStatus($"{_index + 1} / {Buttons.Count}");
            View.PlayCue();
        }

        void OnPress(InputAction.CallbackContext ctx)
        {
            if (!IsRunning || _pressedCurrent) return;
            if (!_anyPressed) { Remember(ctx); _anyPressed = true; }
            // Move on at the next Tick, outside this input callback.
            _pressedCurrent = true;
            _current?.Disable();
        }

        protected override void OnTick(double now)
        {
            if (_pressedCurrent)
            {
                Next(now);
            }
            else if (_index < Buttons.Count && now - _shownAt >= SecondsPerButton)
            {
                _unpressed.Add(Buttons[_index].Name);
                Next(now);
            }
        }

        void DisposeCurrent()
        {
            if (_current == null) return;
            _current.performed -= OnPress;
            Retire(_current);
            _current = null;
        }

        protected override void OnStop()
        {
            DisposeCurrent();
            View.SetPrompt("");
        }

        public override void WriteResult(Measurements m)
        {
            m.Buttons = new ButtonAccessMeasurement
            {
                Device = Device,
                RequestedButtons = Buttons.Select(b => b.Name).ToList(),
                UnpressedButtons = WasSkipped ? new List<string>() : new List<string>(_unpressed),
                Confidence = WasSkipped ? Confidence.Skipped : Confidence.Ok,
            };
        }
    }
}

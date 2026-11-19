// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace AccessCalibration.UI
{
    public enum GlyphShape { None, Cross, Circle, Square, Triangle }

    /// <summary>One prompt glyph: text ("A", "Esc") or a drawn PlayStation shape; round badge or key badge.</summary>
    public readonly struct Glyph
    {
        public readonly string Text;
        public readonly GlyphShape Shape;
        public readonly bool IsKey;
        public Glyph(string text, bool isKey = false, GlyphShape shape = GlyphShape.None)
        {
            Text = text; IsKey = isKey; Shape = shape;
        }
        public override string ToString() => Shape != GlyphShape.None ? Shape.ToString() : Text;
    }

    /// <summary>Which family of prompts to show: follows the last device that pressed a button.</summary>
    public enum GlyphFamily { Keyboard, Xbox, PlayStation, Nintendo }

    /// <summary>The actions the calibration screens show hints for (they match the runner's bindings).</summary>
    public enum PromptAction
    {
        /// <summary>Gamepad south button / Enter (runner submit).</summary>
        Confirm,
        /// <summary>Gamepad Select / Esc (runner skip).</summary>
        Skip,
        /// <summary>"Press any button" — shown as the south button / any key.</summary>
        AnyButton,
        /// <summary>D-pad / arrow keys up-down.</summary>
        Move,
        /// <summary>Turn the selected row on / off: gamepad west button / Space (runner toggle).</summary>
        Toggle,
    }

    /// <summary>
    /// Maps device → prompt family and (family, action) → glyph. Never shows a glyph the player
    /// doesn't have: a PlayStation pad gets ✕ ○ □ △, a Switch Pro pad gets Nintendo letters
    /// (the south button is "B"), a keyboard gets key names.
    /// </summary>
    public static class InputGlyphs
    {
        public static GlyphFamily FamilyOf(InputDevice device)
        {
            if (device == null || device is Keyboard || device is Mouse || device is Pointer) return GlyphFamily.Keyboard;
            string type = device.GetType().Name;
            string maker = device.description.manufacturer ?? "";
            if (type.Contains("DualShock") || type.Contains("DualSense") || maker.Contains("Sony")) return GlyphFamily.PlayStation;
            if (type.Contains("Switch") || maker.Contains("Nintendo")) return GlyphFamily.Nintendo;
            return GlyphFamily.Xbox; // Xbox and generic gamepads/joysticks
        }

        public static Glyph For(GlyphFamily family, PromptAction action)
        {
            switch (action)
            {
                case PromptAction.Confirm:
                case PromptAction.AnyButton:
                    switch (family)
                    {
                        case GlyphFamily.PlayStation: return new Glyph("", shape: GlyphShape.Cross);
                        case GlyphFamily.Nintendo: return new Glyph("B");
                        case GlyphFamily.Xbox: return new Glyph("A");
                        default: return action == PromptAction.Confirm ? new Glyph("Enter", isKey: true) : new Glyph("Any key", isKey: true);
                    }
                case PromptAction.Skip:
                    switch (family)
                    {
                        case GlyphFamily.PlayStation: return new Glyph("Create", isKey: true);
                        case GlyphFamily.Nintendo: return new Glyph("−");
                        case GlyphFamily.Xbox: return new Glyph("View", isKey: true);
                        default: return new Glyph("Esc", isKey: true);
                    }
                case PromptAction.Toggle:
                    switch (family)
                    {
                        case GlyphFamily.PlayStation: return new Glyph("", shape: GlyphShape.Square);
                        case GlyphFamily.Nintendo: return new Glyph("Y");   // west on a Switch pad
                        case GlyphFamily.Xbox: return new Glyph("X");
                        default: return new Glyph("Space", isKey: true);
                    }
                default: // Move
                    return family == GlyphFamily.Keyboard ? new Glyph("↑ ↓", isKey: true) : new Glyph("D-pad ↑↓", isKey: true);
            }
        }
    }

    /// <summary>Tracks the last device that pressed a button and raises Changed when its family changes.</summary>
    public sealed class GlyphTracker : IDisposable
    {
        IDisposable _subscription;
        public GlyphFamily Family { get; private set; }
        public event Action<GlyphFamily> Changed;

        public GlyphTracker()
        {
            Family = Gamepad.current != null ? InputGlyphs.FamilyOf(Gamepad.current) : GlyphFamily.Keyboard;
            _subscription = InputSystem.onAnyButtonPress.Call(control => Set(InputGlyphs.FamilyOf(control.device)));
        }

        public void Set(GlyphFamily family)
        {
            if (family == Family) return;
            Family = family;
            Changed?.Invoke(family);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}

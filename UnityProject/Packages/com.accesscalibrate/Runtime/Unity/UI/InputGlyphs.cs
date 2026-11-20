// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
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
        /// <summary>A drawn icon instead of text (Resources/AccessCalibrate/Drawings/icon-&lt;Icon&gt;), e.g. "menu" (≡).</summary>
        public readonly string Icon;
        public Glyph(string text, bool isKey = false, GlyphShape shape = GlyphShape.None, string icon = null)
        {
            Text = text; IsKey = isKey; Shape = shape; Icon = icon;
        }
        public override string ToString() => Icon != null ? Icon : Shape != GlyphShape.None ? Shape.ToString() : Text;
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
        /// <summary>Go back to the previous check: View / Create / − button, Backspace.</summary>
        Back,
        /// <summary>Skip the whole setup: hold Menu (≡), Shift+Esc.</summary>
        SkipAll,
        /// <summary>Change a value: d-pad / arrow keys left-right.</summary>
        Adjust,
        /// <summary>Close a dialog: east button / Esc.</summary>
        Cancel,
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

        /// <summary>The glyph of a binding path for a family, e.g. "&lt;Gamepad&gt;/buttonSouth" → A / ✕, "&lt;Keyboard&gt;/space" → Space.</summary>
        public static Glyph ForBinding(GlyphFamily family, IEnumerable<string> bindings)
        {
            string pad = null, key = null;
            foreach (var b in bindings) { if (b.StartsWith("<Gamepad>/")) pad = b.Substring(10); else if (b.StartsWith("<Keyboard>/")) key = b.Substring(11); }
            if (family == GlyphFamily.Keyboard || pad == null)
                return new Glyph(key == null ? "?" : key.Length == 1 ? key.ToUpperInvariant() : char.ToUpperInvariant(key[0]) + key.Substring(1), isKey: true);
            switch (pad)
            {
                case "buttonSouth": return family == GlyphFamily.PlayStation ? new Glyph("", shape: GlyphShape.Cross) : new Glyph(family == GlyphFamily.Nintendo ? "B" : "A");
                case "buttonEast": return family == GlyphFamily.PlayStation ? new Glyph("", shape: GlyphShape.Circle) : new Glyph(family == GlyphFamily.Nintendo ? "A" : "B");
                case "buttonWest": return family == GlyphFamily.PlayStation ? new Glyph("", shape: GlyphShape.Square) : new Glyph(family == GlyphFamily.Nintendo ? "Y" : "X");
                case "buttonNorth": return family == GlyphFamily.PlayStation ? new Glyph("", shape: GlyphShape.Triangle) : new Glyph(family == GlyphFamily.Nintendo ? "X" : "Y");
                case "leftShoulder": return new Glyph(family == GlyphFamily.PlayStation ? "L1" : family == GlyphFamily.Nintendo ? "L" : "LB", isKey: true);
                case "rightShoulder": return new Glyph(family == GlyphFamily.PlayStation ? "R1" : family == GlyphFamily.Nintendo ? "R" : "RB", isKey: true);
                case "leftTrigger": return new Glyph(family == GlyphFamily.PlayStation ? "L2" : family == GlyphFamily.Nintendo ? "ZL" : "LT", isKey: true);
                case "rightTrigger": return new Glyph(family == GlyphFamily.PlayStation ? "R2" : family == GlyphFamily.Nintendo ? "ZR" : "RT", isKey: true);
                default: return new Glyph(pad, isKey: true);
            }
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
                case PromptAction.SkipAll:
                    switch (family)
                    {
                        case GlyphFamily.Nintendo: return new Glyph("+");
                        case GlyphFamily.Keyboard: return action == PromptAction.Skip ? new Glyph("Esc", isKey: true) : new Glyph("Shift + Esc", isKey: true);
                        default: return new Glyph("", icon: "menu");   // ≡ Menu / Options
                    }
                case PromptAction.Back:
                    switch (family)
                    {
                        case GlyphFamily.Nintendo: return new Glyph("−");
                        case GlyphFamily.PlayStation: return new Glyph("Create", isKey: true);
                        case GlyphFamily.Keyboard: return new Glyph("Backspace", isKey: true);
                        default: return new Glyph("", icon: "view");
                    }
                case PromptAction.Cancel:
                    switch (family)
                    {
                        case GlyphFamily.PlayStation: return new Glyph("", shape: GlyphShape.Circle);
                        case GlyphFamily.Nintendo: return new Glyph("A");
                        case GlyphFamily.Xbox: return new Glyph("B");
                        default: return new Glyph("Esc", isKey: true);
                    }
                case PromptAction.Adjust:
                    return family == GlyphFamily.Keyboard ? new Glyph("← →", isKey: true) : new Glyph("", icon: "left-right");
                case PromptAction.Toggle:
                    switch (family)
                    {
                        case GlyphFamily.PlayStation: return new Glyph("", shape: GlyphShape.Square);
                        case GlyphFamily.Nintendo: return new Glyph("Y");   // west on a Switch pad
                        case GlyphFamily.Xbox: return new Glyph("X");
                        default: return new Glyph("Space", isKey: true);
                    }
                default: // Move
                    return family == GlyphFamily.Keyboard ? new Glyph("↑ ↓", isKey: true) : new Glyph("", icon: "up-down");
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

        /// <summary>True after the player picked a device: presses on other devices no longer change the glyphs.</summary>
        public bool Locked { get; private set; }

        /// <summary>Show this family and keep it (the player's choice in "What are you playing with?").</summary>
        public void Lock(GlyphFamily family)
        {
            Locked = false;
            Set(family);
            Locked = true;
        }

        public void Set(GlyphFamily family)
        {
            if (Locked || family == Family) return;
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

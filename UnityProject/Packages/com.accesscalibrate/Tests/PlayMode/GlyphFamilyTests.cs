// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.UI;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Switch;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>Prompts follow the device: never a glyph the player doesn't have.</summary>
    public class GlyphFamilyTests : InputTestFixture
    {
        [Test]
        public void EachDevice_GetsItsOwnFamily()
        {
            Assert.AreEqual(GlyphFamily.Keyboard, InputGlyphs.FamilyOf(InputSystem.AddDevice<Keyboard>()));
            Assert.AreEqual(GlyphFamily.Keyboard, InputGlyphs.FamilyOf(InputSystem.AddDevice<Mouse>()));
            Assert.AreEqual(GlyphFamily.Xbox, InputGlyphs.FamilyOf(InputSystem.AddDevice<Gamepad>()));
            Assert.AreEqual(GlyphFamily.PlayStation, InputGlyphs.FamilyOf(InputSystem.AddDevice<DualShock4GamepadHID>()));
            Assert.AreEqual(GlyphFamily.Nintendo, InputGlyphs.FamilyOf(InputSystem.AddDevice<SwitchProControllerHID>()));
        }

        [Test]
        public void ConfirmGlyph_MatchesTheSouthButtonOfEachFamily()
        {
            Assert.AreEqual("A", InputGlyphs.For(GlyphFamily.Xbox, PromptAction.Confirm).Text);
            Assert.AreEqual(GlyphShape.Cross, InputGlyphs.For(GlyphFamily.PlayStation, PromptAction.Confirm).Shape);
            Assert.AreEqual("B", InputGlyphs.For(GlyphFamily.Nintendo, PromptAction.Confirm).Text);   // south on Switch
            var key = InputGlyphs.For(GlyphFamily.Keyboard, PromptAction.Confirm);
            Assert.AreEqual("Enter", key.Text);
            Assert.IsTrue(key.IsKey);
            Assert.AreEqual("Esc", InputGlyphs.For(GlyphFamily.Keyboard, PromptAction.Skip).Text);
        }

        [Test]
        public void ButtonPress_SwitchesTheFamily()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            using (var tracker = new GlyphTracker())
            {
                PressAndRelease(keyboard.aKey);
                Assert.AreEqual(GlyphFamily.Keyboard, tracker.Family);
                PressAndRelease(pad.buttonSouth);
                Assert.AreEqual(GlyphFamily.Xbox, tracker.Family);
            }
        }
    }
}

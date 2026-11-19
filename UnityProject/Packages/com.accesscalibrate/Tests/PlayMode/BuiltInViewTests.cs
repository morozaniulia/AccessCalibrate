// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>
    /// Smoke test for the placeholder UI: it loads from Resources, binds every element, and draws.
    /// Each screen is rendered into a texture and saved as PNG in UnityProject/Logs/ui-*.png
    /// (Logs/ is git-ignored) so it can be checked by eye.
    /// </summary>
    public class BuiltInViewTests
    {
        GameObject _host;
        RenderTexture _rt;

        [SetUp]
        public void SetUp() => AccessCalibrate.ResetStaticState();

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
            if (_rt != null) _rt.Release();
            AccessCalibrate.ResetStaticState();
        }

        IEnumerator Snapshot(string name)
        {
            for (int i = 0; i < 3; i++) yield return null;   // let layout + render run
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "ui-" + name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator BuiltInView_LoadsBindsAndDrawsEveryScreen()
        {
            _host = new GameObject("Runner");
            var runner = _host.AddComponent<CalibrationRunner>();
            runner.StartCalibration();   // creates the built-in view

            var view = runner.View as UIToolkitCalibrationView;
            Assert.IsNotNull(view, "built-in view should be created");
            Assert.IsNotNull(view.Panel.themeStyleSheet, "theme should load from Resources");

            _rt = new RenderTexture(1280, 720, 24);
            view.Panel.targetTexture = _rt;
            view.Panel.clearColor = true;

            Assert.AreEqual(CalibrationTexts.ReactionTitle, view.Root.Q<Label>("ac-title").text);
            yield return Snapshot("1-intro");
            // The USS must really apply (a name clash once silently loaded an empty sheet).
            Assert.AreEqual(72f, view.Root.Q<Label>("ac-title").resolvedStyle.fontSize, "USS not applied");

            view.ShowTest(CalibrationTexts.ReactionTitle, CalibrationTexts.ReactionInstructions);
            view.SetStatus(string.Format(CalibrationTexts.ReactionTrial, 3, 6));
            view.ShowStimulus(true);
            yield return Snapshot("2-reaction-signal");

            view.ShowTest(CalibrationTexts.StabilityTitle, CalibrationTexts.StabilityInstructionsStick);
            view.ShowStabilityArena(true);
            view.SetCursor(0.3f, -0.2f);
            yield return Snapshot("3-stability");

            view.ShowTest(CalibrationTexts.ButtonsTitle, CalibrationTexts.ButtonsInstructions);
            view.SetPrompt(string.Format(CalibrationTexts.ButtonsPrompt, CalibrationTexts.ButtonJump));
            yield return Snapshot("4-buttons");

            var m = new Measurements
            {
                Reaction = new ReactionMeasurement { MedianMs = 620, Confidence = Confidence.Ok },
                Hold = new HoldMeasurement { MaxHoldSeconds = 1.4, Confidence = Confidence.Uncertain },
                Buttons = new ButtonAccessMeasurement
                {
                    RequestedButtons = { "A", "B", "X", "Y" }, UnpressedButtons = { "X", "Y" }, Confidence = Confidence.Ok,
                },
            };
            var review = new ProposalReview(RulesEngine.Evaluate(m, new RulesConfig()));
            view.ShowConfirmation(review);
            view.SetConfirmationSelection(review.Items.Count);   // Accept
            var toggles = view.Root.Query<Toggle>(className: "ac-toggle").ToList();
            Assert.AreEqual(4, toggles.Count);                     // timing, hold, remap, one-handed
            Assert.AreEqual(1, view.Root.Query<Label>(className: "ac-uncertain").ToList().Count);
            var holdIndex = review.Items.ToList().FindIndex(i => i.Change.Setting == "holdToToggle");
            Assert.IsFalse(toggles[holdIndex].value, "uncertain proposal must start switched off");
            Assert.IsTrue(toggles[0].value);
            yield return Snapshot("5-confirmation");
        }
    }
}

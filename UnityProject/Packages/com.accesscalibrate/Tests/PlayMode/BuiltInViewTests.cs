// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.IO;
using System.Linq;
using AccessCalibration.Core;
using AccessCalibration.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AccessCalibration.PlayModeTests
{
    /// <summary>
    /// The built-in screens, driven through the real flow with simulated input, rendered into a
    /// texture and saved as UnityProject/Logs/ui-*.png (Logs/ is git-ignored) for review.
    /// A mid-tone clear colour stands in for the game behind the translucent overlay.
    /// </summary>
    public class BuiltInViewTests : InputTestFixture
    {
        GameObject _host;
        RenderTexture _rt;
        CalibrationRunner _runner;
        UIToolkitCalibrationView _view;
        Gamepad _pad;
        Keyboard _keyboard;
        double _now = 10;
        string _dir;

        public override void Setup()
        {
            base.Setup();
            AccessCalibrate.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            _pad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_rt != null) _rt.Release();
            base.TearDown();
            AccessCalibrate.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        IEnumerator StartRunner(int width = 1280, int height = 720)
        {
            _host = new GameObject("Runner");
            _host.SetActive(false);
            _runner = _host.AddComponent<CalibrationRunner>();
            _runner.Clock = () => _now;
            _host.SetActive(true);
            _runner.StartCalibration();
            _view = _runner.View as UIToolkitCalibrationView;
            Assert.IsNotNull(_view, "built-in view should be created");

            _rt = new RenderTexture(width, height, 24);
            _view.Panel.targetTexture = _rt;
            _view.Panel.clearColor = true;
            _view.Panel.colorClearValue = new Color(0.27f, 0.36f, 0.42f);   // "the game"
            yield return null;
        }

        IEnumerator Snapshot(string name)
        {
            // Let layout, the 200 ms panel fade-in and repaint finish (frames are very short in batch mode).
            float until = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < until) yield return null;
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

        /// <summary>A press nothing on the current screen reacts to, so only the glyph family changes.</summary>
        IEnumerator UseDevice(UnityEngine.InputSystem.Controls.ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        Label Q(string name) => _view.Root.Q<Label>(name);

        [UnityTest, Timeout(120000)]
        public IEnumerator EveryScreen_LoadsStylesFollowsTheDevice_AndIsSaved()
        {
            yield return StartRunner();
            yield return UseDevice(_pad.dpad.left);
            Assert.AreEqual(GlyphFamily.Xbox, _view.GlyphFamily);

            // 1. Welcome
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.AreEqual(CalibrationTexts.WelcomeTitle, Q("ac-intro-title").text);
            Assert.AreEqual(54f, Q("ac-intro-title").resolvedStyle.fontSize, "USS tokens not applied");
            yield return Snapshot("1-welcome");

            // 2. Check intro
            _runner.WelcomeStart();
            Assert.AreEqual("Step 1 of 5", Q("ac-intro-eyebrow").text);
            yield return Snapshot("2-intro");

            // 3. Reaction, with the signal showing
            _runner.BeginCurrent();
            _now = _runner.Reaction.NextStimulusAt;
            _runner.Reaction.Tick(_now);
            Assert.IsTrue(_runner.Reaction.StimulusVisible);
            yield return Snapshot("3-reaction");

            // 4. Hold, 2.4 s into a press
            _runner.SkipCurrent();
            _runner.BeginCurrent();
            Press(_keyboard.spaceKey, time: _now);
            yield return null;
            Assert.IsTrue(_runner.Hold.IsHolding);
            _now += 2.4;
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime) yield return null;   // ring animates
            Assert.AreEqual("2.4", Q("ac-hold-seconds").text);
            yield return Snapshot("4-hold");
            Release(_keyboard.spaceKey, time: _now);
            yield return null;

            // 5. Steadiness, measuring, with a little movement
            yield return UseDevice(_pad.dpad.left);
            _runner.SkipCurrent();            // hold
            _runner.SkipCurrent();            // repeated presses (from its intro)
            _runner.BeginCurrent();           // steadiness
            Assert.AreEqual(StabilityTest.Mode.Stick, _runner.Stability.ActiveMode);
            var target = _runner.Stability.StickTarget;
            for (int f = 0; f < 28; f++)
            {
                Set(_pad.leftStick, f < 2 ? Vector2.zero   // starts at rest, outside the circle
                    : target + new Vector2(Mathf.Sin(f * 1.3f) * 0.09f, Mathf.Cos(f * 0.9f) * 0.07f + 0.04f));
                yield return null;
                _now += 0.1;
                _runner.Stability.Tick(_now);
            }
            Assert.AreEqual(StabilityTest.Phase.Measuring, _runner.Stability.CurrentPhase);
            Assert.GreaterOrEqual(_view.StabilityTrailCount, 5, "trail of the last ~6 positions");
            var measuring = Q("ac-stability-measuring");
            var left = Q("ac-stability-left");
            yield return Snapshot("5-steadiness");
            Assert.AreEqual(measuring.worldBound.center.y, left.worldBound.center.y, 4f, "\"Measuring\" and \"s left\" on one line");
            Assert.Greater(left.worldBound.xMin, measuring.worldBound.xMax, "at opposite ends");

            // 6. Suggested settings: two reliable proposals, one uncertain (starts off)
            _runner.SkipCurrent();
            _runner.SkipCurrent();
            _runner.ShowProposal(new Measurements
            {
                Reaction = new ReactionMeasurement { MedianMs = 620, Confidence = Confidence.Ok },
                Hold = new HoldMeasurement { MaxHoldSeconds = 1.4, Confidence = Confidence.Uncertain },
                Buttons = new ButtonAccessMeasurement
                {
                    RequestedButtons = { "Jump", "Action", "LB", "RB" }, UnpressedButtons = { "RB" }, Confidence = Confidence.Ok,
                },
            });
            Assert.AreEqual("Apply 2 settings", _view.Root.Q<Label>("ac-apply-label").text);
            var hintText = _view.Root.Q("ac-hints").Q<Label>(className: "ac-glyph-text");
            Assert.AreEqual("X", hintText.text, "on/off hint uses the toggle button");
            Assert.AreEqual(1, _view.Root.Query(className: "ac-row--uncertain").ToList().Count);
            Assert.AreEqual(1, _view.Root.Query<DashedOutline>().ToList().Count);
            yield return Snapshot("6-confirmation");
            Assert.IsFalse(_view.ListFadeVisible, "everything fits: no edge is faded");
            Assert.IsTrue(_view.Root.Query(className: "ac-row").ToList().All(r => r.resolvedStyle.opacity > 0.999f));
            var apply = _view.Root.Q<Button>("ac-apply");
            var applyLabel = _view.Root.Q<Label>("ac-apply-label");
            Assert.Less(apply.layout.width, 0.45f * _view.Root.Q("ac-panel").layout.width, "Apply hugs its content");
            Assert.AreEqual(30f, applyLabel.worldBound.xMin - apply.worldBound.xMin, 6f, "30 px left padding (+ border)");

            // Themes switch live, without rebuilding the screen
            AccessCalibrate.UseTheme(ThemePreset.GlassLight);
            yield return Snapshot("7-confirmation-glass-light");
            AccessCalibrate.UseTheme(ThemePreset.HighContrast);
            yield return Snapshot("8-confirmation-high-contrast");
            AccessCalibrate.UseTheme(ThemePreset.GlassDark);

            // PlayStation glyphs (drawn shapes). Device → family is covered by GlyphFamilyTests.
            _view.ForceGlyphFamily(GlyphFamily.PlayStation);
            yield return Snapshot("9-confirmation-playstation");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Confirmation_LongListFadesAndScrolls_EmptyListIsCentred()
        {
            yield return StartRunner();
            yield return UseDevice(_pad.dpad.left);

            // Eight suggestions (steadiness measured with a mouse → mouse smoothing, not a deadzone).
            _runner.ShowProposal(new Measurements
            {
                Reaction = new ReactionMeasurement { MedianMs = 820, Confidence = Confidence.Ok },
                Hold = new HoldMeasurement { MaxHoldSeconds = 1.2, Confidence = Confidence.Ok },
                Taps = new TapMeasurement { PressCount = 6, DurationSeconds = 5, Confidence = Confidence.Ok },
                Stability = new StabilityMeasurement { Spread = 0.42, Confidence = Confidence.Ok, Device = Core.InputDeviceKind.Mouse },
                Buttons = new ButtonAccessMeasurement
                {
                    RequestedButtons = { "Jump", "Action", "LB", "RB" }, UnpressedButtons = { "LB", "RB" }, Confidence = Confidence.Ok,
                },
            });
            Assert.AreEqual(8, _runner.Review.Items.Count);
            Assert.IsTrue(_runner.Review.Items.Any(i => i.Change.Setting == "mouseSmoothing"));
            Assert.IsFalse(_runner.Review.Items.Any(i => i.Change.Setting == "stickDeadzone"));
            yield return Snapshot("6b-confirmation-long");
            Assert.IsTrue(_view.ListFadeVisible, "more rows below → rows near the bottom fade out");
            Assert.AreEqual(0, _view.Root.Query(className: "ac-list-fade").ToList().Count, "no overlay is drawn on top of the list");
            var rows = _view.Root.Query(className: "ac-row").ToList();
            Assert.AreEqual(1f, rows[0].resolvedStyle.opacity, 1e-3, "rows away from the edge are untouched");
            Assert.IsTrue(rows.Any(r => r.resolvedStyle.opacity < 0.99f), "a row near the bottom is faded");
            Assert.IsTrue(rows.All(r => r.resolvedStyle.opacity >= 0.15f - 1e-3), "never below 0.15");
            var buttonsTop = _view.Root.Q<Button>("ac-apply").worldBound.y;

            // Light and High Contrast themes: same soft edge, no drawn shapes.
            AccessCalibrate.UseTheme(ThemePreset.GlassLight);
            yield return Snapshot("6b-confirmation-long-glass-light");
            AccessCalibrate.UseTheme(ThemePreset.HighContrast);
            yield return Snapshot("6b-confirmation-long-high-contrast");
            AccessCalibrate.UseTheme(ThemePreset.GlassDark);

            _view.ConfirmList.scrollOffset = new Vector2(0, 90);
            yield return null;
            Assert.IsTrue(_view.ConfirmList.ClassListContains("ac-scrolling"), "scrollbar shows while scrolling");
            yield return Snapshot("6c-confirmation-scrolling");
            Assert.Less(rows[0].resolvedStyle.opacity, 0.99f, "scrolled down: the row cut at the top fades too");

            _view.ConfirmList.scrollOffset = new Vector2(0, 10000);
            yield return Snapshot("6d-confirmation-end");
            Assert.AreEqual(1f, rows[rows.Count - 1].resolvedStyle.opacity, 1e-3, "nothing below: the last row is fully visible");
            Assert.Less(rows[0].resolvedStyle.opacity, 0.99f, "rows hidden above: the top edge fades");
            Assert.IsTrue(_view.ListFadeVisible);
            Assert.IsTrue(rows.All(r => r.resolvedStyle.opacity >= 0.15f - 1e-3), "never below 0.15");

            _view.ConfirmList.scrollOffset = Vector2.zero;
            yield return null;
            Assert.AreEqual(1f, rows[0].resolvedStyle.opacity, 1e-3, "back at the top: nothing hidden above, no top fade");
            Assert.AreEqual(buttonsTop, _view.Root.Q<Button>("ac-apply").worldBound.y, 0.5f, "the buttons never move");

            // Nothing to suggest: centred message, no "Turn on / off" hint.
            _runner.Skip();
            _runner.ShowProposal(new Measurements());
            yield return Snapshot("6e-confirmation-empty");
            Assert.AreEqual(0, _view.Root.Q("ac-hints").childCount, "no on/off hint without rows");
            var empty = _view.Root.Q<Label>("ac-confirm-empty");
            var wrap = _view.Root.Q("ac-confirm-list-wrap");
            Assert.AreEqual(DisplayStyle.Flex, empty.resolvedStyle.display);
            Assert.AreEqual(wrap.worldBound.center.y, empty.worldBound.center.y, 2f, "centred in the list area");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Welcome_ScalesWithTheScreen()
        {
            foreach (var (w, h) in new[] { (1920, 1080), (2560, 1440) })
            {
                yield return StartRunner(w, h);
                yield return Snapshot($"scale-{w}x{h}");
                var panel = _view.Root.Q("ac-panel");
                Assert.AreEqual(0.9f * _view.Root.layout.width, panel.layout.width, 2f, "panel keeps 90% width");
                Object.DestroyImmediate(_host);
                _rt.Release();
                _host = null;
                _rt = null;
            }
        }
    }
}

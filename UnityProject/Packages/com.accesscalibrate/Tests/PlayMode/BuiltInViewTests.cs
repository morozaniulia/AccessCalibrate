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
    /// The built-in screens (UI v2), driven through the real flow with simulated input, rendered
    /// into a texture and saved as UnityProject/Logs/ui-v2-&lt;theme&gt;-&lt;screen&gt;.png (Logs/ is
    /// git-ignored) to compare with Docs/design-v2/png. The flow uses the design's example values.
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

        /// <summary>
        /// Same screen with keyboard glyphs ("Enter", "Space"), saved as ui-&lt;name&gt;-keyboard.png:
        /// the README images. The family is restored afterwards, so the test goes on as before.
        /// </summary>
        IEnumerator KeyboardSnapshot(string name)
        {
            var family = _view.GlyphFamily;
            _view.ForceGlyphFamily(GlyphFamily.Keyboard);
            yield return Snapshot(name + "-keyboard");
            _view.ForceGlyphFamily(family);
            yield return null;
        }

        /// <summary>A press nothing on the current screen reacts to, so only the glyph family changes.</summary>
        IEnumerator UseDevice(UnityEngine.InputSystem.Controls.ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        T Q<T>(string name) where T : VisualElement => _view.Root.Q<T>(name);
        string[] Texts(VisualElement e) => e.Query<Label>().ToList().Where(l => l.resolvedStyle.display == DisplayStyle.Flex && !string.IsNullOrEmpty(l.text)).Select(l => l.text).ToArray();
        bool HasText(string text) => _view.Root.Query<Label>().ToList().Any(l => l.text == text && IsShown(l));
        static bool IsShown(VisualElement e) { for (var p = e; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None) return false; return true; }
        string[] HintTexts => _view.Root.Q("ac-hints").Query<Label>(className: "ac-hint-text").ToList().Select(l => l.text).ToArray();
        GlyphBadge[] HintGlyphs => _view.Root.Q("ac-hints").Query<GlyphBadge>().ToList().ToArray();

        /// <summary>The design's example measurements (Docs/design-v2 screens 12 and 14).</summary>
        static Measurements DesignMeasurements() => new Measurements
        {
            Reaction = new ReactionMeasurement { MedianMs = 650, Confidence = Confidence.Ok, RawTrialsMs = { 700, 640, 650, 660, 610, 655 }, ValidTrialCount = 5 },
            Hold = new HoldMeasurement { MaxHoldSeconds = 1.4, Confidence = Confidence.Ok, RawHoldsSeconds = { 1.4, 1.2 } },
            Taps = new TapMeasurement { PressCount = 4, DurationSeconds = 5, Confidence = Confidence.Ok },
            Stability = new StabilityMeasurement { Spread = 0.26, Confidence = Confidence.Ok, Device = Core.InputDeviceKind.Mouse },
            Buttons = new ButtonAccessMeasurement
            {
                RequestedButtons = { "A", "B", "X", "Y", "RB", "LB" }, UnpressedButtons = { "RB" }, Confidence = Confidence.Ok,
            },
        };

        static readonly ThemePreset[] AllThemes = { ThemePreset.Dark, ThemePreset.Light, ThemePreset.HighContrast };
        static string Slug(ThemePreset t) => t == ThemePreset.HighContrast ? "high-contrast" : t.ToString().ToLowerInvariant();

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryScreen_InEveryTheme_FollowsTheDesign([ValueSource(nameof(AllThemes))] ThemePreset theme)
        {
            yield return StartRunner();
            AccessCalibrate.UseTheme(theme);
            yield return UseDevice(_pad.dpad.left);
            Assert.AreEqual(GlyphFamily.Xbox, _view.GlyphFamily);
            string t = Slug(theme);

            // 01 Welcome
            Assert.IsTrue(_runner.IsWelcoming);
            Assert.AreEqual(UIToolkitCalibrationView.Screen.Welcome, _view.CurrentScreen);
            var welcome = _view.Page(UIToolkitCalibrationView.Screen.Welcome);
            CollectionAssert.IsSubsetOf(ScreenTexts.WelcomeTitle, Texts(welcome), "title on the design's two lines");
            CollectionAssert.IsSubsetOf(ScreenTexts.WelcomeSub, Texts(welcome));
            Assert.IsTrue(HasText(ScreenTexts.DeviceXbox));
            Assert.AreEqual(48f, welcome.Q<Label>(className: "ac-line").resolvedStyle.fontSize, "USS applied");
            Assert.AreEqual(0, _view.Root.Q("ac-steps").childCount, "no step indicator on the Welcome screen");
            CollectionAssert.AreEqual(new[] { ScreenTexts.HintSelect, ScreenTexts.HintConfirm }, HintTexts);
            Assert.IsFalse(_view.ReplayVisible);
            yield return Snapshot($"v2-{t}-01-welcome");

            // 03 Reaction, ready (intro and waiting for the signal look the same)
            _runner.WelcomeStart();
            Assert.AreEqual(UIToolkitCalibrationView.Screen.Check, _view.CurrentScreen);
            Assert.AreEqual("ready", _view.CheckState);
            _runner.BeginCurrent();
            yield return null;
            Assert.AreEqual("ready", _view.CheckState);
            Assert.IsTrue(HasText(ScreenTexts.ReactionGetReady));
            Assert.IsTrue(HasText("Try 1 of 6"));
            CollectionAssert.IsSubsetOf(ScreenTexts.ReactionInstruction, Texts(Q<VisualElement>("ac-col-left")), "instruction on the design's lines");
            Assert.IsTrue(HasText(ScreenTexts.SkipCheck) && HasText(ScreenTexts.Back) && HasText(ScreenTexts.SkipAll));
            CollectionAssert.AreEqual(new[] { ScreenTexts.HintSkipCheck, ScreenTexts.HintBack, ScreenTexts.HintSkipAllHold }, HintTexts, "hints on the left of the bottom bar");
            Assert.AreEqual("menu", HintGlyphs[0].Current.Icon, "≡ skips");
            Assert.AreEqual("view", HintGlyphs[1].Current.Icon, "View goes back");
            Assert.IsTrue(_view.ReplayVisible, "Replay the intro on a ready screen");
            var current = Q<VisualElement>("ac-step-0");
            Assert.IsTrue(current.ClassListContains("ac-step--current"));
            Assert.AreEqual("Reaction", current.Q<Label>(className: "ac-step-name").text);
            Assert.IsNotNull(current.Q(className: "ac-step-glow"), "soft glow behind the current step");
            yield return Snapshot($"v2-{t}-03-reaction-ready");

            // 04 Reaction, signal on
            _now = _runner.Reaction.NextStimulusAt;
            _runner.Reaction.Tick(_now);
            yield return null;
            Assert.IsTrue(_runner.Reaction.StimulusVisible);
            Assert.AreEqual("signal", _view.CheckState);
            Assert.IsTrue(HasText(ScreenTexts.ReactionPress));
            Assert.IsFalse(_view.ReplayVisible, "no replay on an active screen");
            yield return Snapshot($"v2-{t}-04-reaction-signal");

            // 05 Hold, ready (controller, "Hold A")
            _runner.SkipCurrent();
            _runner.BeginCurrent();
            yield return null;
            Assert.AreEqual("ready", _view.CheckState);
            Assert.AreEqual("controller-hold-a", _view.Drawing.DrawingName);
            Assert.IsEmpty(_view.Drawing.UnknownColours, "every colour of the drawing follows the theme");
            Assert.IsTrue(HasText("Press and hold A to begin"));
            Assert.IsTrue(_view.Root.Q("ac-step-0").ClassListContains("ac-step--done"));
            yield return Snapshot($"v2-{t}-05-hold-ready");

            // 02 Device picker, over the Hold check
            _view.ForceTouchRow = true;
            _view.ShowDevicePicker();
            yield return null;
            Assert.IsTrue(HasText(ScreenTexts.DeviceTitle));
            Assert.IsTrue(HasText("Detected · " + ScreenTexts.DeviceXboxDetail));
            Assert.AreEqual(4, _view.Root.Query(className: "ac-device").ToList().Count);
            Assert.AreEqual(1, _view.Root.Query(className: "ac-device--selected").ToList().Count);
            Assert.AreEqual(1f, _view.Root.Q(className: "ac-dialog").resolvedStyle.backgroundColor.a, 1e-3, "the dialog is opaque: nothing shows through");
            yield return Snapshot($"v2-{t}-02-change-device");
            _view.ShowTest("", "");   // closes the picker

            // 06 Hold, 2.4 s into a press
            Press(_pad.buttonSouth, time: _now);
            yield return null;
            Assert.IsTrue(_runner.Hold.IsHolding);
            _now += 2.4;
            for (float s = 0; s < 0.5f; s += Time.unscaledDeltaTime) yield return null;   // ring catches up
            Assert.AreEqual("ring", _view.CheckState);
            Assert.IsTrue(HasText("2.4") && HasText("seconds of 5") && HasText("Keep holding A"));
            Assert.IsFalse(IsShown(_view.Drawing), "no controller behind the ring");
            yield return Snapshot($"v2-{t}-06-hold-active");
            Release(_pad.buttonSouth, time: _now);
            yield return null;

            // 07 Repeated presses, ready
            _runner.SkipCurrent();
            _runner.BeginCurrent();
            yield return null;
            Assert.AreEqual("ready", _view.CheckState);
            Assert.AreEqual("controller-press-a", _view.Drawing.DrawingName);
            CollectionAssert.IsSubsetOf(ScreenTexts.PressesTip, Texts(Q<VisualElement>("ac-col-left")), "\"Comfort matters more than speed\" tip");
            yield return Snapshot($"v2-{t}-07-presses-ready");

            // 08 Repeated presses, 12 presses, 3.0 s left
            double first = _now;
            for (int i = 0; i < 12; i++)
            {
                Press(_pad.buttonSouth, time: _now); yield return null;
                Release(_pad.buttonSouth, time: _now + 0.05); yield return null;
                _now += 0.15;
            }
            _now = first + 2.0;
            yield return null;
            Assert.AreEqual(12, _runner.Taps.PressCount);
            Assert.AreEqual("counter", _view.CheckState);
            Assert.IsTrue(HasText("12") && HasText("3.0 s left") && HasText(ScreenTexts.PressesKeepGoing));
            yield return Snapshot($"v2-{t}-08-presses-active");

            // 09 Steadiness, ready (stick at rest)
            _runner.SkipCurrent();
            _runner.BeginCurrent();
            yield return null;
            Assert.AreEqual("ready", _view.CheckState);
            Assert.AreEqual("controller-left-stick", _view.Drawing.DrawingName);
            yield return Snapshot($"v2-{t}-09-steadiness-ready");

            // 10 Steadiness, measuring
            var target = _runner.Stability.StickTarget;
            for (int f = 0; f < 60 && !(_runner.Stability.CurrentPhase == StabilityTest.Phase.Measuring && _runner.Stability.SecondsLeft <= 2.85); f++)
            {
                Set(_pad.leftStick, target + new Vector2(Mathf.Sin(f * 1.3f) * 0.02f, Mathf.Cos(f * 0.9f) * 0.015f + 0.01f));
                yield return null;
                _now += 0.1;
                _runner.Stability.Tick(_now);
            }
            yield return null;
            Assert.AreEqual(StabilityTest.Phase.Measuring, _runner.Stability.CurrentPhase);
            Assert.AreEqual("target", _view.CheckState);
            Assert.IsTrue(_view.Target.Inside);
            Assert.IsTrue(HasText(ScreenTexts.SteadinessMeasuring) && HasText(ScreenTexts.SteadinessKeep));
            yield return Snapshot($"v2-{t}-10-steadiness-active");
            Set(_pad.leftStick, Vector2.zero);

            // 11 Button reach: six buttons, A B X reached, Y asked for, 2.9 s left
            _runner.SkipCurrent();
            _runner.ButtonAccess.Buttons = new System.Collections.Generic.List<ButtonPrompt>
            {
                new ButtonPrompt("A", "<Gamepad>/buttonSouth"), new ButtonPrompt("B", "<Gamepad>/buttonEast"),
                new ButtonPrompt("X", "<Gamepad>/buttonWest"), new ButtonPrompt("Y", "<Gamepad>/buttonNorth"),
                new ButtonPrompt("RB", "<Gamepad>/rightShoulder"), new ButtonPrompt("LB", "<Gamepad>/leftShoulder"),
            };
            _runner.BeginCurrent();
            yield return null;
            foreach (var (button, after) in new[] { (_pad.buttonSouth, 0.6), (_pad.buttonEast, 0.9), (_pad.buttonWest, 1.4) })
            {
                _now += after;
                // Event times stay the input system's own (the stick moves above used them); the
                // reach time comes from the runner's clock.
                Press(button); yield return null;
                _runner.ButtonAccess.Tick(_now);
                Release(button); yield return null;
            }
            _now += 2.1;
            yield return null; yield return null;
            Assert.AreEqual(3, _runner.ButtonAccess.PromptIndex);
            Assert.IsTrue(HasText("Press now") && HasText("2.9 s left") && HasText("Button 4 of 6"));
            Assert.AreEqual(3, _view.Root.Query<Label>().ToList().Count(l => l.text == ScreenTexts.ReachReached));
            Assert.IsTrue(HasText(ScreenTexts.ReachSkipButton) && HasText("wait 5 s"));
            Assert.IsTrue(_view.ReplayVisible);
            CollectionAssert.AreEqual(new[] { ScreenTexts.HintSkipCheck, ScreenTexts.HintBack, ScreenTexts.HintSkipAllHold }, HintTexts, "Back and Skip the whole setup live in the hints");
            yield return Snapshot($"v2-{t}-11-button-reach");

            // 12 Suggestions (the design's example: aim assist turned off)
            _runner.SkipCurrent();
            var m = DesignMeasurements();
            _runner.ShowProposal(m);
            int aim = _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == "aimAssist");
            if (aim >= 0) _runner.SetAccepted(aim, false);
            _view.SetConfirmationSelection(0);
            yield return null;
            Assert.AreEqual(UIToolkitCalibrationView.Screen.Suggestions, _view.CurrentScreen);
            Assert.IsTrue(HasText(ScreenTexts.SuggestTitle) && HasText("Timing windows ×1.5"));
            Assert.IsTrue(HasText("Median reaction 650 ms.") && HasText("Suggested above 500 ms."));
            Assert.IsTrue(HasText("0.8 s") && HasText("1.2 s") && HasText("×1.5 suggested"));
            CollectionAssert.AreEqual(new[] { ScreenTexts.HintSelect, ScreenTexts.HintToggle, ScreenTexts.HintAdjust }, HintTexts);
            Assert.IsTrue(_view.Root.Q("ac-row-0").ClassListContains("ac-selected"));
            yield return Snapshot($"v2-{t}-12-suggestions");

            // 14 Summary after Apply
            _view.ShowSummary(m, _runner.Review, applied: true);
            yield return null;
            Assert.IsTrue(HasText(ScreenTexts.SummaryTitle) && HasText("650 ms") && HasText("rule: above 500 ms") && HasText("1 of 6"));
            Assert.IsTrue(HasText("Remap RB to an easier button"));
            Assert.AreEqual(5, _view.Root.Query(className: "ac-summary-row").ToList().Count);
            yield return Snapshot($"v2-{t}-14-summary");

            // 13 No changes
            _runner.Skip();
            _runner.ShowProposal(new Measurements());
            yield return null;
            Assert.AreEqual(UIToolkitCalibrationView.Screen.NoChanges, _view.CurrentScreen);
            Assert.IsTrue(HasText(ScreenTexts.NoChangesTitle) && HasText(ScreenTexts.Continue) && HasText(ScreenTexts.Redo));
            Assert.IsFalse(HasText(ScreenTexts.NotNow), "no Not now when there is nothing to keep");
            yield return Snapshot($"v2-{t}-13-no-changes");

            // Background follows the theme
            if (theme == ThemePreset.Dark) Assert.IsNotNull(_view.Backdrop.Image, "Dark shows the galaxy");
            else Assert.IsNull(_view.Backdrop.Image);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator KeyboardAndMouse_ShowTheKeyboard_TheMouse_AndKeyHints()
        {
            InputSystem.RemoveDevice(_pad);
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, Vector2.zero);
            yield return StartRunner();
            _view.ForceGlyphFamily(GlyphFamily.Keyboard);

            _runner.WelcomeStart();
            _runner.SkipCurrent();            // reaction
            _runner.BeginCurrent();           // hold
            yield return null;
            Assert.AreEqual("keyboard-ansi", _view.Drawing.DrawingName);
            Assert.IsEmpty(_view.Drawing.UnknownColours);
            CollectionAssert.Contains(_view.Drawing.Texts.ToList(), "Hold Space");
            Assert.IsTrue(HasText("Press and hold Space to begin"));
            CollectionAssert.AreEqual(new[] { "Esc", "Backspace", "Shift + Esc" }, HintGlyphs.Select(g => g.Current.Text).ToArray());
            CollectionAssert.IsSubsetOf(ScreenTexts.HoldInstructionKey, Texts(Q<VisualElement>("ac-col-left")), "\"key\", not \"button\", on a keyboard");
            foreach (var option in _view.Root.Query(className: "ac-option").ToList())
            {
                var label = option.Q<Label>(className: "ac-option-label").worldBound;
                var trailing = option.Q(className: "ac-option-trailing").worldBound;
                bool sameLine = trailing.yMin < label.yMax && trailing.yMax > label.yMin;
                if (sameLine) Assert.GreaterOrEqual(trailing.xMin - label.xMax, 12f - 0.5f, "at least 12 px between the action and its key");
                Assert.LessOrEqual(trailing.xMax, option.worldBound.xMax, "the key stays inside the row");
            }
            CollectionAssert.AreEqual(new[] { ScreenTexts.HintSkipCheck, ScreenTexts.HintBack, ScreenTexts.HintSkipAllKeys }, HintTexts);
            Assert.IsTrue(HasText(ScreenTexts.DeviceKeyboard));
            yield return Snapshot("v2-keyboard-05-hold-ready");

            _runner.SkipCurrent();            // hold
            _runner.BeginCurrent();           // repeated presses
            yield return null;
            CollectionAssert.Contains(_view.Drawing.Texts.ToList(), "Press Space");
            CollectionAssert.IsSubsetOf(ScreenTexts.PressesInstructionKey, Texts(Q<VisualElement>("ac-col-left")));
            _runner.SkipCurrent();
            _runner.BeginCurrent();           // steadiness
            yield return null;
            Assert.AreEqual(StabilityTest.Mode.Mouse, _runner.Stability.ActiveMode);
            Assert.AreEqual("mouse", _view.Drawing.DrawingName);
            CollectionAssert.IsSubsetOf(ScreenTexts.SteadinessInstructionMouse, Texts(Q<VisualElement>("ac-col-left")));
            yield return Snapshot("v2-keyboard-09-steadiness-mouse");
        }

        /// <summary>
        /// The README images (keyboard glyphs, Steadiness with the mouse), saved as
        /// Logs/ui-readme-*.png and copied by hand to Docs/images.
        /// </summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator ReadmeImages_WithTheKeyboardAndMouse()
        {
            InputSystem.RemoveDevice(_pad);
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, Vector2.zero);
            yield return StartRunner();
            _view.ForceGlyphFamily(GlyphFamily.Keyboard);
            yield return Snapshot("readme-1-welcome");

            _runner.WelcomeStart();
            _runner.BeginCurrent();
            _now = _runner.Reaction.NextStimulusAt;
            _runner.Reaction.Tick(_now);
            yield return null;
            Assert.AreEqual("signal", _view.CheckState);
            yield return Snapshot("readme-3-reaction");

            _runner.SkipCurrent();
            _runner.BeginCurrent();
            Press(_keyboard.spaceKey, time: _now);
            yield return null;
            Assert.IsTrue(_runner.Hold.IsHolding);
            _now += 2.4;
            for (float s = 0; s < 0.5f; s += Time.unscaledDeltaTime) yield return null;
            Assert.AreEqual("ring", _view.CheckState);
            yield return Snapshot("readme-4-hold");
            Release(_keyboard.spaceKey, time: _now);
            yield return null;

            _runner.SkipCurrent();            // hold
            _runner.SkipCurrent();            // repeated presses
            _runner.BeginCurrent();           // steadiness, with the mouse
            var test = _runner.Stability;
            for (int f = 0; f < 60 && !(test.CurrentPhase == StabilityTest.Phase.Measuring && test.SecondsLeft <= 2.85); f++)
            {
                Set(mouse.position, test.Target + new Vector2(Mathf.Sin(f * 1.3f) * 0.3f + 0.15f, Mathf.Cos(f * 0.9f) * 0.25f + 0.1f) * test.Radius);
                yield return null;
                _now += 0.1;
                test.Tick(_now);
            }
            Assert.AreEqual("target", _view.CheckState);
            yield return Snapshot("readme-5-steadiness");

            _runner.SkipCurrent();
            _runner.SkipCurrent();
            _runner.ShowProposal(DesignMeasurements());
            int aim = _runner.Review.Items.ToList().FindIndex(i => i.Change.Setting == "aimAssist");
            if (aim >= 0) _runner.SetAccepted(aim, false);
            _view.SetConfirmationSelection(0);
            yield return Snapshot("readme-6-suggestions");
            AccessCalibrate.UseTheme(ThemePreset.Light);
            yield return Snapshot("readme-7-suggestions-light");
            AccessCalibrate.UseTheme(ThemePreset.HighContrast);
            yield return Snapshot("readme-8-suggestions-high-contrast");
            AccessCalibrate.UseTheme(ThemePreset.Dark);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Screens_ScaleWithTheScreen()
        {
            foreach (var (w, h) in new[] { (1920, 1080), (2560, 1440) })
            {
                yield return StartRunner(w, h);
                yield return Snapshot($"v2-scale-{w}x{h}");
                Assert.AreEqual(1280f, _view.Root.layout.width, 2f, "the 1280×720 layout scales as a whole");
                Assert.AreEqual(720f, _view.Root.layout.height, 2f);
                Object.DestroyImmediate(_host);
                _rt.Release();
                _host = null;
                _rt = null;
            }
        }

        [Test]
        public void EveryDrawing_Parses_AndEveryColourFollowsTheTheme()
        {
            foreach (var name in new[] { "controller-hold-a", "controller-press-a", "controller-left-stick", "controller-reach-y", "keyboard-ansi", "mouse",
                                         "icon-menu", "icon-view", "icon-up-down", "icon-left-right", "icon-sound", "icon-grip", "icon-pace", "icon-hand" })
            {
                var d = new SvgDrawing(name);
                Assert.Greater(d.ShapeCount, 0, name);
                Assert.IsEmpty(d.UnknownColours, name + ": colours without a theme token");
            }
            Assert.AreEqual(4, new SvgDrawing("controller-hold-a").Texts.Count(t => t.Length == 1), "A B X Y labels");
        }

        [Test]
        public void SvgPaths_ArcsAndRelativeCommands_BecomeCubics()
        {
            var (subs, closed) = SvgDrawing.PathData("M10 10 h20 v20 a10 10 0 0 1 -10 10 Z");
            Assert.AreEqual(1, subs.Count);
            Assert.IsTrue(closed[0]);
            var last = subs[0][subs[0].Count - 1];
            Assert.AreEqual('C', last.Kind);
            Assert.AreEqual(20f, last.C.x, 1e-3);
            Assert.AreEqual(40f, last.C.y, 1e-3);
        }
    }
}

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

        /// <summary>The runner with the built-in view. Layout tests turn motion off (final states at once); motion tests keep it.</summary>
        IEnumerator StartRunner(int width = 1280, int height = 720, bool reduceMotion = true)
        {
            _host = new GameObject("Runner");
            _host.SetActive(false);
            _runner = _host.AddComponent<CalibrationRunner>();
            _runner.Clock = () => _now;
            _runner.ReduceMotion = reduceMotion;
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
            _runner.ForceTouchAvailable = true;
            _runner.OpenDevicePicker();
            yield return null;
            Assert.IsTrue(HasText(ScreenTexts.DeviceTitle));
            Assert.IsTrue(HasText("Detected · " + ScreenTexts.DeviceXboxDetail));
            Assert.AreEqual(4, _view.Root.Query(className: "ac-device").ToList().Count);
            Assert.AreEqual(1, _view.Root.Query(className: "ac-device--selected").ToList().Count);
            Assert.AreEqual(1f, _view.Root.Q(className: "ac-dialog").resolvedStyle.backgroundColor.a, 1e-3, "the dialog is opaque: nothing shows through");
            yield return Snapshot($"v2-{t}-02-change-device");
            _runner.CloseDevicePicker();

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
            Assert.Contains("Press Y", _view.Drawing.Texts.ToList(), "the drawing lights the button asked for");
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
            _runner.Accept();
            yield return null;
            Assert.IsTrue(_runner.IsShowingSummary);
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

        [UnityTest, Timeout(120000)]
        public IEnumerator ChosenDevice_PlayStation_Keyboard_Touch_DrawingsFollow()
        {
            yield return StartRunner();
            _runner.ForceTouchAvailable = true;
            _runner.WelcomeStart();
            yield return null;

            // PlayStation: the Xbox drawing with ✕ ○ □ △ drawn in place of the letters
            _runner.SkipCurrent();                                   // Hold
            _runner.ChooseDevice(DeviceChoice.PlayStation);
            yield return null;
            Assert.AreEqual(GlyphFamily.PlayStation, _view.GlyphFamily);
            Assert.IsFalse(_view.Drawing.Texts.Any(x => x == "A" || x == "B" || x == "X" || x == "Y"), "no Xbox letters on a PlayStation drawing");
            Assert.Contains("Hold " + ScreenTexts.PsCross, _view.Drawing.Texts.ToList());
            Assert.IsEmpty(_view.Drawing.UnknownColours);
            yield return Snapshot("v2-ps-05-hold-ready");

            // The choice sticks: a press on another device does not switch the glyphs back
            yield return UseDevice(_keyboard.leftCtrlKey);
            Assert.AreEqual(GlyphFamily.PlayStation, _view.GlyphFamily);

            // Button reach, live: the requested button lit, the reached ones pressed
            _runner.SkipCurrent(); _runner.SkipCurrent(); _runner.SkipCurrent();
            Assert.AreSame(_runner.ButtonAccess, _runner.CurrentTest);
            yield return null; yield return null;
            Assert.Contains("Press " + ScreenTexts.PsCross, _view.Drawing.Texts.ToList(), "first prompt: Jump = ✕");
            _now += 0.8;                                             // the player takes 0.8 s to reach it
            Press(_pad.buttonSouth); yield return null;
            _runner.ButtonAccess.Tick(_now);
            Release(_pad.buttonSouth); yield return null; yield return null;
            Assert.AreEqual(0.8, _runner.ButtonAccess.ReachSeconds[0], 1e-6, "reach time = time from the prompt to the press");
            Assert.IsTrue(HasText("0.8 s"));
            Assert.Contains("Press " + ScreenTexts.PsSquare, _view.Drawing.Texts.ToList(), "second prompt: Action = □");
            yield return Snapshot("v2-ps-11-button-reach");
            _now += 0.3; Press(_pad.buttonWest); yield return null; _runner.ButtonAccess.Tick(_now); Release(_pad.buttonWest); yield return null;
            yield return null;
            Assert.Contains("Press L1", _view.Drawing.Texts.ToList(), "bumpers too");

            // Keyboard & mouse: the keyboard with the key lit
            _runner.ChooseDevice(DeviceChoice.KeyboardMouse);
            yield return null; yield return null;
            Assert.AreEqual("keyboard-ansi", _view.Drawing.DrawingName);
            Assert.Contains("Press Space", _view.Drawing.Texts.ToList());
            Assert.IsEmpty(_view.Drawing.UnknownColours);
            yield return Snapshot("v2-keyboard-11-button-reach");

            // Touch: Button reach is skipped; Hold and Repeated presses get a big press area
            _runner.Back(); _runner.Back(); _runner.Back();
            _runner.ChooseDevice(DeviceChoice.Touch);
            yield return null;
            Assert.AreSame(_runner.Hold, _runner.CurrentTest);
            Assert.IsNotNull(Q<VisualElement>("ac-touch-area"));
            Assert.IsTrue(HasText(ScreenTexts.HoldAreaTouch) && HasText(ScreenTexts.HoldReadyTouch));
            Assert.IsTrue(HasText(ScreenTexts.DeviceTouch), "the top bar names the chosen device");
            Assert.IsEmpty(HintTexts, "no key hints on touch");
            Assert.IsFalse(HasText(ScreenTexts.ButtonsMatch));
            Assert.AreEqual(0, _view.Root.Q("ac-col-right").Query<GlyphBadge>().ToList().Count, "options are plain buttons");
            Assert.AreEqual(0, _view.Root.Q("ac-bottombar").Query<GlyphBadge>().ToList().Count);
            Assert.IsTrue(HasText("<u>" + ScreenTexts.ReplayIntroTouch + "</u>"), "no (R) key on touch");
            Assert.IsTrue(HasText(ScreenTexts.HoldTipTouch[0]), "the grip tip speaks of the device, not a controller");
            yield return null;
            var area = Q<VisualElement>("ac-touch-area");
            var skip = _view.Root.Q("ac-col-right").Query(className: "ac-option").First();
            Assert.IsTrue(_view.IsInPressAreaPanel(area.worldBound.center), "a touch on the circle counts");
            Assert.IsFalse(_view.IsInPressAreaPanel(skip.worldBound.center), "a touch on Skip does not");
            Assert.IsFalse(_view.IsInPressAreaPanel(Q<VisualElement>("ac-topbar").worldBound.center), "nor on Change / the top bar");
            Assert.IsFalse(_view.IsInPressAreaPanel(Q<VisualElement>("ac-bottombar").worldBound.center));
            yield return Snapshot("v2-touch-05-hold-ready");
            _runner.SkipCurrent();
            yield return null;
            Assert.IsTrue(HasText(ScreenTexts.PressesAreaTouch));
            yield return Snapshot("v2-touch-07-presses-ready");
            _runner.SkipCurrent(); _runner.SkipCurrent();
            yield return null;
            Assert.IsTrue(_runner.IsReviewing, "no Button reach on touch");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Summary_ButtonsAndCopy()
        {
            yield return StartRunner();
            _runner.ConfirmationInputDelaySeconds = 0;
            _runner.ShowProposal(DesignMeasurements());
            _runner.Accept();
            yield return null;
            Assert.IsTrue(_runner.IsShowingSummary);
            var buttons = _view.Root.Q("ac-page-summary").Query(className: "ac-selected").ToList();
            Assert.AreEqual(1, buttons.Count, "one button selected");
            Assert.AreEqual("Continue to game", ScreenTexts.SummaryContinue, "the package's own text (the feedback app's \"Done\" must not leak)");
            Assert.IsTrue(buttons[0].Query<Label>().ToList().Any(l => l.text == ScreenTexts.SummaryContinue));
            _runner.CopyResults();
            yield return null;
            Assert.IsTrue(HasText(ScreenTexts.CopyDone));
            yield return Snapshot("v2-dark-14-summary-copied");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Motion_CheckIntro_FollowsTheSpec()
        {
            yield return StartRunner(reduceMotion: false);
            double mt = 0;
            _view.ScreenMotion.Clock = () => mt;
            _runner.WelcomeStart();                                  // Reaction: its intro starts now
            yield return null; yield return null;
            Assert.AreEqual(UIToolkitCalibrationView.CheckIntroSeconds, _view.ScreenMotion.Length, 0.01, "IntroSeconds is the timeline's length");
            Assert.AreEqual(4.0, UIToolkitCalibrationView.CheckIntroSeconds, 0.15, "about 4 s");
            Assert.AreEqual(UIToolkitCalibrationView.CheckStartSeconds, _runner.Reaction.StartDelaySeconds, 1e-9, "the check measures once its centre is visible");
            Assert.IsTrue(_runner.Reaction.IsInIntro);

            var tb = _view.Root.Q(className: "ac-title-block");
            var centre = Q<VisualElement>("ac-col-center");
            var cards = _view.Root.Q("ac-col-right").Query(className: "ac-option").ToList();
            foreach (var t in new[] { 0.3, 0.6, 1.0, 1.5, 2.0, 2.5, 3.1, 4.5 })
            {
                mt = t;
                yield return null; yield return null;
                if (t == 0.3) Assert.Less(tb.style.opacity.value, 1f, "fogging in");
                if (t == 0.6)
                {
                    Assert.AreEqual(1f, tb.style.opacity.value, 1e-3);
                    Assert.Greater(Mathf.Abs(tb.style.translate.value.x.value), 100f, "centred on the screen");
                    Assert.AreEqual(1.08f, tb.style.scale.value.value.x, 1e-3);
                    foreach (var line in tb.Query<Label>().ToList())
                    {
                        Assert.AreNotEqual(0f, line.resolvedStyle.opacity, "every line is visible: " + line.text);
                        float x = line.style.translate.value.x.value;
                        Assert.IsTrue(x > 0 && x < 145, "each line centred in its block: " + line.text + " " + x);
                    }
                    Assert.AreEqual(0f, centre.style.opacity.value, "the centre comes later");
                    Assert.AreEqual(0f, cards[0].style.opacity.value);
                }
                if (t == 1.5) Assert.AreEqual(0f, tb.style.translate.value.x.value, 0.5f, "in its column");
                if (t == 2.5) Assert.Greater(centre.style.opacity.value, 0f, "the centre fogs in at 2.3 s");
                if (t == 3.1)
                {
                    Assert.AreEqual(1f, centre.style.opacity.value, 1e-4, "the centre is in place when measuring starts");
                    Assert.AreEqual(StyleKeyword.Null, centre.style.filter.keyword, "and sharp");
                    var dropping = cards[2].Children().First();
                    Assert.AreEqual(0f, dropping.style.opacity.value, "a card's text stays hidden until the card is in place");
                }
                if (t == 4.5)
                {
                    Assert.IsFalse(_view.ScreenMotion.Playing);
                    Assert.AreEqual(StyleKeyword.Null, tb.style.opacity.keyword, "final state: the USS decides");
                    Assert.AreEqual(StyleKeyword.Null, cards[2].style.translate.keyword);
                }
                yield return Snapshot("motion-03-" + t.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.IsTrue(_runner.Reaction.IsInIntro, "the runner's own clock has not moved");
            _now += UIToolkitCalibrationView.CheckStartSeconds;
            _runner.Reaction.Tick(_now);
            Assert.IsFalse(_runner.Reaction.IsInIntro);
            Assert.IsFalse(_runner.Reaction.StimulusVisible, "then the usual random wait");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Motion_ReadyToActive_TheCentreSwapsWithAFade()
        {
            yield return StartRunner(reduceMotion: false);
            double mt = 0;
            _view.ScreenMotion.Clock = () => mt;
            _runner.WelcomeStart();
            _runner.SkipCurrent();                                   // Hold
            yield return null;
            mt = 5; yield return null;                               // intro over
            _now += UIToolkitCalibrationView.CheckStartSeconds + 0.1;
            _runner.Hold.Tick(_now);
            Assert.IsFalse(_runner.Hold.IsInIntro);
            var ready = Q<VisualElement>("ac-state-ready");
            var ring = Q<VisualElement>("ac-state-ring");
            Assert.AreEqual(GlyphFamily.Xbox, _view.GlyphFamily);
            Press(_pad.buttonSouth, time: _now); yield return null; yield return null;   // same family: no rebuild; input time on the runner's clock
            Assert.IsTrue(_runner.Hold.IsHolding);
            Assert.AreEqual("ring", _view.CheckState);
            Assert.AreEqual(DisplayStyle.Flex, ready.resolvedStyle.display, "the ready centre fades out first");
            Assert.AreEqual(DisplayStyle.None, ring.resolvedStyle.display);
            mt = 5 + 0.8; yield return null; yield return null;
            Assert.Less(ready.style.opacity.value, 1f);
            yield return Snapshot("motion-06-swap-0.8");
            mt = 5 + 1.5; yield return null; yield return null;
            Assert.AreEqual(DisplayStyle.None, ready.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, ring.resolvedStyle.display, "the ring fogs in");
            yield return Snapshot("motion-06-swap-1.5");
            mt = 5 + 2.5; yield return null; yield return null;
            Assert.IsFalse(_view.ScreenMotion.Playing);
            Release(_pad.buttonSouth, time: _now + 0.5); yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Motion_OtherScreens_AnimateIn_ThenSettle()
        {
            yield return StartRunner(reduceMotion: false);
            double mt = 0;
            _view.ScreenMotion.Clock = () => mt;
            _view.DialogMotion.Clock = () => mt;

            // 01 Welcome (built before the clock was replaced: show it again from the start)
            _view.Hide();
            _view.ShowWelcome();
            yield return null;
            var welcome = Q<VisualElement>("ac-page-welcome").Children().ToList();
            mt = 1.0; yield return null; yield return null;
            Assert.Greater(welcome[0].style.opacity.value, 0f, "title first");
            Assert.AreEqual(0f, welcome[5].style.opacity.value, "buttons last");
            yield return Snapshot("motion-01-welcome-1.0");
            mt = 2.2; yield return null; yield return null;
            yield return Snapshot("motion-01-welcome-2.2");
            mt = 6; yield return null; yield return null;
            Assert.IsFalse(_view.ScreenMotion.Playing);

            // 02 device picker
            mt = 10;
            _runner.OpenDevicePicker();
            yield return null;
            var main = Q<VisualElement>("ac-main");
            mt = 10.2; yield return null; yield return null;
            Assert.Less(main.style.opacity.value, 1f, "the screen fades out first");
            mt = 10.45; yield return null; yield return null;
            Assert.AreEqual(0f, main.style.opacity.value, "nothing shows through the panel while it comes in");
            yield return Snapshot("motion-02-picker-0.45");
            mt = 12; yield return null; yield return null;
            Assert.IsFalse(_view.DialogMotion.Playing);
            Assert.AreEqual(1f, main.resolvedStyle.opacity, "back behind the opaque panel, dimmed by the scrim");
            _runner.CloseDevicePicker();

            // 12 suggestions
            mt = 20;
            _runner.ShowProposal(DesignMeasurements());
            yield return null;
            mt = 21.4; yield return null; yield return null;
            var lastRow = Q<VisualElement>("ac-page-suggest").Query(className: "ac-suggestion").ToList().Last();
            Assert.AreEqual(0f, lastRow.Q(className: "ac-suggestion-text").style.opacity.value, "a row's text stays hidden until it is in place");
            yield return Snapshot("motion-12-suggestions-1.4");
            mt = 22.6; yield return null; yield return null;
            yield return Snapshot("motion-12-suggestions-2.6");
            mt = 26; yield return null; yield return null;
            Assert.IsFalse(_view.ScreenMotion.Playing);

            // 14 summary
            _runner.ConfirmationInputDelaySeconds = 0;
            mt = 30;
            _runner.Accept();
            yield return null;
            mt = 31.6; yield return null; yield return null;
            yield return Snapshot("motion-14-summary-1.6");
            mt = 36; yield return null; yield return null;
            Assert.IsFalse(_view.ScreenMotion.Playing);
            Assert.AreEqual(StyleKeyword.Null, Q<VisualElement>("ac-page-summary").Q(className: "ac-summary-row").style.opacity.keyword);

            // 13 no changes
            mt = 40;
            _runner.ShowProposal(new Measurements());
            yield return null;
            mt = 41.9; yield return null; yield return null;
            yield return Snapshot("motion-13-no-changes-1.9");
            mt = 46; yield return null; yield return null;
            Assert.IsFalse(_view.ScreenMotion.Playing);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Panel_ReferencesTheBlurShader_SoPlayersCanBlur()
        {
            yield return StartRunner();
            var so = Resources.Load<PanelSettings>("AccessCalibrate/AccessCalibratePanel");
            Assert.IsNotNull(so, "the panel template ships in Resources");
            Assert.AreEqual("AccessCalibratePanel(Clone)", _view.Panel.name, "the view uses a copy of it");
#if UNITY_EDITOR
            var field = new UnityEditor.SerializedObject(_view.Panel).FindProperty("m_RuntimeGaussianBlurShader");
            Assert.IsNotNull(field?.objectReferenceValue, "the blur shader is referenced (otherwise it is stripped from builds)");
#endif
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ReduceMotion_NoIntro_FinalStatesAtOnce()
        {
            yield return StartRunner(reduceMotion: true);
            _runner.WelcomeStart();
            yield return null;
            Assert.AreEqual(0, _view.IntroSeconds);
            Assert.IsFalse(_runner.Reaction.IsInIntro, "nothing to wait for");
            Assert.IsFalse(_view.ScreenMotion.Playing);
            var tb = _view.Root.Q(className: "ac-title-block");
            Assert.AreEqual(StyleKeyword.Null, tb.style.opacity.keyword, "no inline motion styles");
            Assert.AreEqual(1f, tb.resolvedStyle.opacity);
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

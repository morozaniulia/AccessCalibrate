// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AccessCalibration.Core;
using AccessCalibration.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration
{
    /// <summary>
    /// The built-in screens, UI v2 (Docs/design-v2), built with UI Toolkit: UXML + USS in
    /// Resources/AccessCalibrate, every colour / radius / font a --ac-… token, every text in
    /// <see cref="ScreenTexts"/>. Layout: a 64 px top bar (title, step indicator, device), the page,
    /// and a 60 px bottom bar (button hints on the left, a note on the right).
    ///
    /// It implements the unchanged <see cref="ICalibrationView"/> (plus the optional Welcome
    /// screen). What the interface doesn't carry — trial numbers, hold seconds, presses, time left —
    /// is read from the running test's read-only properties each frame.
    /// </summary>
    public sealed class UIToolkitCalibrationView : MonoBehaviour, ICalibrationView, ICalibrationWelcomeView
    {
        const string ResourceFolder = "AccessCalibrate/";

        /// <summary>Raised when "Skip this check" is clicked (mouse / touch).</summary>
        public event Action SkipClicked;
        /// <summary>Raised when "Replay the intro" is clicked or R is pressed on a ready screen.</summary>
        public event Action ReplayIntroClicked;

        internal enum Screen { Hidden, Welcome, Check, Suggestions, NoChanges, Summary }

        UIDocument _doc;
        AudioSource _audio;
        AudioClip _cue;
        CalibrationRunner _runner;
        GlyphTracker _glyphs;
        Screen _screen = Screen.Hidden;
        readonly List<StyleSheet> _themeSheets = new List<StyleSheet>();
        bool _reduceMotion;

        ThemeBackdrop _backdrop;
        VisualElement _root, _steps, _topRight, _main, _dialogLayer, _hints, _bottomRight;
        Label _topTitle;
        readonly Dictionary<Screen, VisualElement> _pages = new Dictionary<Screen, VisualElement>();
        VisualElement _colLeft, _colCenter, _colRight;

        // check screens
        CalibrationTest _checkTest;
        bool _inIntro;
        readonly Dictionary<string, VisualElement> _states = new Dictionary<string, VisualElement>();
        Label _status, _substatus, _holdValue, _holdUnit, _count, _measureState, _measureLeft, _reachCount;
        VisualElement _statusBlock, _progressFill, _reachProgressFill, _reachList;
        HoldRing _holdRing, _holdGlow;
        TickRing _ticks;
        SteadinessTarget _target;
        SvgDrawing _drawing;
        float _ringShown;
        string _lastStatus = "";
        bool _ready;

        // suggestions / no changes / summary
        ProposalReview _review;
        int _selection = -1, _detailIndex;
        readonly List<VisualElement> _rows = new List<VisualElement>();
        readonly List<VisualElement> _selectable = new List<VisualElement>();
        ScrollView _list;
        VisualElement _detail;
        Label _applyLabel, _topCount;
        Button _apply, _redo, _notNow, _continue, _redoEmpty;

        // welcome
        Button _start, _welcomeNotNow;
        Label _welcomeDevice;

        // bottom bar: what is shown, so it can be rebuilt when the device family changes
        (PromptAction action, string text)[] _hintSpec = Array.Empty<(PromptAction, string)>();

        /// <summary>The panel this view draws into (e.g. to render it into a texture).</summary>
        public PanelSettings Panel => _doc != null ? _doc.panelSettings : null;
        internal VisualElement Root => _root;
        internal ThemeBackdrop Backdrop => _backdrop;
        internal Screen CurrentScreen => _screen;
        internal VisualElement Page(Screen s) => _pages.TryGetValue(s, out var p) ? p : null;
        /// <summary>The centre state shown on a check screen ("ready", "signal", "ring", "counter", "target", "reach").</summary>
        internal string CheckState { get; private set; } = "";
        internal bool ReplayVisible => _bottomRight.Q(className: "ac-replay") != null;
        internal SvgDrawing Drawing => _drawing;
        internal SteadinessTarget Target => _target;
        /// <summary>The glyph family on screen (follows the last device that pressed a button).</summary>
        public GlyphFamily GlyphFamily => _glyphs?.Family ?? GlyphFamily.Keyboard;

        /// <summary>Tests: show a family without a real device press.</summary>
        internal void ForceGlyphFamily(GlyphFamily family) => _glyphs.Set(family);

        public static UIToolkitCalibrationView Create(CalibrationRunner runner)
        {
            var go = new GameObject("AccessCalibrate View");
            go.SetActive(false);
            go.transform.SetParent(runner.transform, false);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ResourceFolder + "AccessCalibrateTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 1000;

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = Resources.Load<VisualTreeAsset>(ResourceFolder + "CalibrationView");

            var view = go.AddComponent<UIToolkitCalibrationView>();
            view._doc = doc;
            view._runner = runner;
            view._audio = go.AddComponent<AudioSource>();
            view._audio.playOnAwake = false;
            go.SetActive(true);

            view.Bind(runner.ReduceMotion);
            view.SkipClicked += runner.SkipCurrent;
            return view;
        }

        // ================================================================ setup

        void Bind(bool reduceMotion)
        {
            _root = _doc.rootVisualElement.Q("ac-root");
            var sheet = Resources.Load<StyleSheet>(ResourceFolder + "CalibrationViewStyle");
            if (sheet != null) _doc.rootVisualElement.styleSheets.Add(sheet);
            _doc.rootVisualElement.pickingMode = PickingMode.Ignore;
            _reduceMotion = reduceMotion;
            _root.EnableInClassList("ac-reduce-motion", reduceMotion);
            _backdrop = new ThemeBackdrop();
            _root.Insert(0, _backdrop);

            _topTitle = _root.Q<Label>("ac-top-title");
            _steps = _root.Q("ac-steps");
            _topRight = _root.Q("ac-top-right");
            _main = _root.Q("ac-main");
            _dialogLayer = _root.Q("ac-dialog-layer");
            _hints = _root.Q("ac-hints");
            _bottomRight = _root.Q("ac-bottom-right");
            _pages[Screen.Welcome] = _root.Q("ac-page-welcome");
            _pages[Screen.Check] = _root.Q("ac-page-check");
            _pages[Screen.Suggestions] = _root.Q("ac-page-suggest");
            _pages[Screen.NoChanges] = _root.Q("ac-page-nochange");
            _pages[Screen.Summary] = _root.Q("ac-page-summary");
            _colLeft = _root.Q("ac-col-left");
            _colCenter = _root.Q("ac-col-center");
            _colRight = _root.Q("ac-col-right");

            _glyphs = new GlyphTracker();
            _glyphs.Changed += _ => RefreshGlyphs();
            _cue = MakeCue();

            BuildWelcome();

            AccessCalibrate.OnThemeChanged += OnThemeChanged;
            CalibrationTheme.Edited += OnThemeEdited;
            ApplyTheme();
            Hide();
        }

        void OnDestroy()
        {
            AccessCalibrate.OnThemeChanged -= OnThemeChanged;
            CalibrationTheme.Edited -= OnThemeEdited;
            _glyphs?.Dispose();
        }

        // ================================================================ theme

        CalibrationTheme ActiveTheme => AccessCalibrate.Theme != null ? AccessCalibrate.Theme : _runner != null ? _runner.Theme : null;

        void OnThemeChanged(CalibrationTheme _) => ApplyTheme();
        void OnThemeEdited(CalibrationTheme t) { if (t == ActiveTheme) ApplyTheme(); }

        /// <summary>Preset USS, then the theme's own overrides, after the default stylesheet. Live.</summary>
        public void ApplyTheme()
        {
            var host = _doc.rootVisualElement;
            foreach (var s in _themeSheets) host.styleSheets.Remove(s);
            _themeSheets.Clear();

            var theme = ActiveTheme;
            if (theme != null)
            {
                var preset = CalibrationTheme.LoadPresetSheet(theme.preset);
                if (preset != null) _themeSheets.Add(preset);
                if (theme.themeOverrides != null) _themeSheets.Add(theme.themeOverrides);
            }
            foreach (var s in _themeSheets) host.styleSheets.Add(s);
            ApplyFontOverride();
        }

        /// <summary>A theme font replaces Manrope everywhere (inline style beats the USS weights).</summary>
        void ApplyFontOverride()
        {
            var font = ActiveTheme != null ? ActiveTheme.font : null;
            if (font == null && !_fontOverridden) return;
            _fontOverridden = font != null;
            _root.Query<VisualElement>().ForEach(e =>
                e.style.unityFontDefinition = font != null
                    ? new StyleFontDefinition(FontDefinition.FromSDFFont(font))
                    : new StyleFontDefinition(StyleKeyword.Null));
        }
        bool _fontOverridden;

        // ================================================================ building blocks

        static Label L(string text, params string[] classes)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static VisualElement E(params string[] classes)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        /// <summary>A text on fixed lines (one element per line, so the breaks are exactly the design's).</summary>
        static VisualElement Lines(IEnumerable<string> lines, params string[] classes)
        {
            var box = E("ac-lines");
            foreach (var c in classes) box.AddToClassList(c);
            foreach (var line in lines) box.Add(L(line, "ac-line"));
            return box;
        }

        static Label Link(string text, Action onClick, params string[] classes)
        {
            var l = L("<u>" + text + "</u>", "ac-link");
            l.enableRichText = true;
            l.pickingMode = PickingMode.Position;
            foreach (var c in classes) l.AddToClassList(c);
            if (onClick != null) l.AddManipulator(new Clickable(onClick));
            return l;
        }

        GlyphBadge GlyphFor(PromptAction action, params string[] classes) => GlyphOf(InputGlyphs.For(GlyphFamily, action), classes);

        static GlyphBadge GlyphOf(Glyph glyph, params string[] classes)
        {
            var g = new GlyphBadge();
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) g.AddToClassList(c);
            g.Set(glyph);
            return g;
        }

        Button Btn(string label, bool primary, bool large, float width, Action onClick, PromptAction? glyph = null, params string[] glyphClasses)
        {
            var b = new Button(onClick) { text = "" };
            b.AddToClassList("ac-btn");
            b.AddToClassList(primary ? "ac-btn--primary" : "ac-btn--secondary");
            if (large) b.AddToClassList("ac-btn--large");
            b.style.width = width;
            b.Add(L(label, "ac-btn-label"));
            if (glyph.HasValue)
            {
                if (!primary) b.AddToClassList("ac-btn--with-glyph");
                var classes = new List<string>(glyphClasses) { "ac-btn-glyph" };
                if (primary) classes.Add("ac-glyph--inverse");
                b.Add(GlyphFor(glyph.Value, classes.ToArray()));
            }
            return b;
        }

        static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        string DeviceName
        {
            get
            {
                switch (GlyphFamily)
                {
                    case GlyphFamily.Xbox: return ScreenTexts.DeviceXbox;
                    case GlyphFamily.PlayStation: return ScreenTexts.DevicePlayStation;
                    case GlyphFamily.Nintendo: return ScreenTexts.DeviceNintendo;
                    default: return ScreenTexts.DeviceKeyboard;
                }
            }
        }

        /// <summary>The south button / Space, for texts like "Press and hold A to begin".</summary>
        string MainButton
        {
            get
            {
                switch (GlyphFamily)
                {
                    case GlyphFamily.PlayStation: return "✕";
                    case GlyphFamily.Nintendo: return "B";
                    case GlyphFamily.Xbox: return "A";
                    default: return "Space";
                }
            }
        }

        // ================================================================ top bar / bottom bar

        void TopBar(int current, bool welcome = false, string rightText = null, bool showChange = true)
        {
            _topTitle.text = ScreenTexts.TopTitle;
            _topTitle.EnableInClassList("ac-top-title--welcome", welcome);
            _steps.Clear();
            if (!welcome)
            {
                int count = _runner != null ? _runner.Tests.Count : 5;
                for (int i = 0; i < count; i++) _steps.Add(Step(i, current));
            }
            _topRight.Clear();
            if (welcome)
            {
                _topRight.Add(GlyphOf(new Glyph("Esc", isKey: true), "ac-glyph--top-key"));
                var notNow = L(ScreenTexts.NotNow, "ac-top-notnow");
                notNow.pickingMode = PickingMode.Position;
                notNow.AddManipulator(new Clickable(() => _runner?.WelcomeNotNow()));
                _topRight.Add(notNow);
                return;
            }
            var device = L(rightText ?? DeviceName, "ac-top-device", "ac-top-device-name");
            device.EnableInClassList("ac-top-device--summary", _screen == Screen.Summary);
            _topRight.Add(device);
            if (showChange) _topRight.Add(Link(ScreenTexts.Change, ShowDevicePicker, "ac-top-change"));
        }

        /// <summary>One step of the indicator: done (filled), current (pill with name and a soft glow), upcoming (outlined).</summary>
        VisualElement Step(int i, int current)
        {
            string num = (i + 1).ToString(CultureInfo.InvariantCulture);
            var step = E("ac-step", i < current ? "ac-step--done" : i == current ? "ac-step--current" : "ac-step--upcoming");
            step.name = "ac-step-" + i;
            if (i != current) { step.Add(L(num, "ac-step-num")); return step; }
            step.Add(E("ac-step-glow"));
            var dot = E("ac-step-dot");
            dot.Add(L(num, "ac-step-num"));
            step.Add(dot);
            step.Add(L(StepName(i), "ac-step-name"));
            return step;
        }

        string StepName(int i)
        {
            var tests = _runner != null ? _runner.Tests : null;
            if (tests != null && i < tests.Count) return TitleOf(tests[i]);
            return i < ScreenTexts.NoChangesChecks.Length ? ScreenTexts.NoChangesChecks[i] : "";
        }

        static string TitleOf(CalibrationTest t)
        {
            switch (t)
            {
                case ReactionTest _: return ScreenTexts.ReactionTitle;
                case HoldTest _: return ScreenTexts.HoldTitle;
                case TapTest _: return ScreenTexts.PressesTitle;
                case StabilityTest _: return ScreenTexts.SteadinessTitle;
                case ButtonAccessTest _: return ScreenTexts.ReachTitle;
                default: return t.Title;
            }
        }

        void BottomBar(string note, bool replay, bool welcomeNote = false, params (PromptAction action, string text)[] hints)
        {
            _hintSpec = hints;
            BuildHints();
            _bottomRight.Clear();
            if (!string.IsNullOrEmpty(note))
            {
                var n = L(note, "ac-bottom-note");
                n.EnableInClassList("ac-bottom-note--welcome", welcomeNote);
                _bottomRight.Add(n);
            }
            if (replay) _bottomRight.Add(Link(ScreenTexts.ReplayIntro, () => ReplayIntroClicked?.Invoke(), "ac-replay"));
        }

        void BuildHints()
        {
            _hints.Clear();
            foreach (var (action, text) in _hintSpec)
            {
                var hint = E("ac-hint");
                hint.Add(GlyphFor(action));
                hint.Add(L(text, "ac-hint-text"));
                if (action == PromptAction.Skip) { hint.pickingMode = PickingMode.Position; hint.AddManipulator(new Clickable(() => SkipClicked?.Invoke())); }
                _hints.Add(hint);
            }
        }

        (PromptAction, string)[] CheckHints => new[]
        {
            (PromptAction.Skip, ScreenTexts.HintSkipCheck),
            (PromptAction.Back, ScreenTexts.HintBack),
            (PromptAction.SkipAll, GlyphFamily == GlyphFamily.Keyboard ? ScreenTexts.HintSkipAllKeys : ScreenTexts.HintSkipAllHold),
        };

        void RefreshGlyphs()
        {
            switch (_screen)
            {
                case Screen.Welcome: ShowWelcome(); break;
                case Screen.Check: if (_checkTest != null) BuildCheck(_checkTest); break;
                case Screen.Suggestions: case Screen.NoChanges: if (_review != null) { int sel = _selection; ShowConfirmation(_review); SetConfirmationSelection(sel); } break;
                default: BuildHints(); break;
            }
            if (_dialogLayer.ClassListContains("ac-dialog-layer--shown")) ShowDevicePicker();
        }

        void ShowPage(Screen screen)
        {
            _screen = screen;
            _root.style.display = DisplayStyle.Flex;
            foreach (var kv in _pages) kv.Value.EnableInClassList("ac-page--shown", kv.Key == screen);
            HideDevicePicker();
        }

        // ================================================================ 01 welcome

        void BuildWelcome()
        {
            var page = _pages[Screen.Welcome];
            page.Clear();
            page.Add(Lines(ScreenTexts.WelcomeTitle, "ac-welcome-title", "ac-lines--center"));
            page.Add(Lines(ScreenTexts.WelcomeSub, "ac-welcome-sub", "ac-lines--center"));
            var steps = E("ac-welcome-steps");
            for (int i = 0; i < ScreenTexts.CheckNames.Length; i++)
            {
                var s = E("ac-welcome-step");
                s.Add(L((i + 1).ToString("00", CultureInfo.InvariantCulture), "ac-welcome-num"));
                s.Add(Lines(ScreenTexts.CheckNames[i], "ac-welcome-name"));
                steps.Add(s);
            }
            page.Add(steps);
            var track = E("ac-welcome-track");
            track.Add(E("ac-welcome-line"));
            for (int i = 1; i < ScreenTexts.CheckNames.Length; i++)
            {
                var dot = E("ac-welcome-dot");
                dot.style.left = i * 176 + 7;
                track.Add(dot);
            }
            var current = E("ac-welcome-current");
            current.Add(E("ac-welcome-current-glow"));
            var ring = new DashedCircle { Dash = 2, Gap = 2, Width = 1 };
            ring.AddToClassList("ac-dashed-circle--current");
            ring.style.left = 0; ring.style.top = 0; ring.style.width = 20; ring.style.height = 20;
            current.Add(ring);
            current.Add(E("ac-welcome-current-dot"));
            track.Add(current);
            page.Add(track);
            var device = E("ac-welcome-device");
            device.Add(L(ScreenTexts.WelcomePlayingWith, "ac-welcome-device-text"));
            _welcomeDevice = L("", "ac-welcome-device-name");
            device.Add(_welcomeDevice);
            device.Add(L(ScreenTexts.WelcomeDetected, "ac-welcome-device-text"));
            device.Add(Link(ScreenTexts.Change, ShowDevicePicker, "ac-welcome-change"));
            page.Add(device);
            var buttons = E("ac-buttons");
            buttons.pickingMode = PickingMode.Position;
            _start = Btn(ScreenTexts.WelcomeStart, true, true, 240, () => _runner?.WelcomeStart(), PromptAction.Confirm, "ac-glyph--small", "ac-glyph--welcome");
            _welcomeNotNow = Btn(ScreenTexts.NotNow, false, true, 160, () => _runner?.WelcomeNotNow());
            buttons.Add(_start);
            buttons.Add(_welcomeNotNow);
            page.Add(buttons);
        }

        public void ShowWelcome()
        {
            ShowPage(Screen.Welcome);
            _checkTest = null;
            BuildWelcome();
            _welcomeDevice.text = DeviceName;
            TopBar(-1, welcome: true);
            BottomBar(ScreenTexts.WelcomeNote, false, true, (PromptAction.Move, ScreenTexts.HintSelect), (PromptAction.Confirm, ScreenTexts.HintConfirm));
            ApplyFontOverride();
        }

        public void SetWelcomeSelection(int index)
        {
            _start.EnableInClassList("ac-selected", index == 0);
            _welcomeNotNow.EnableInClassList("ac-selected", index == 1);
        }

        // ================================================================ 03–11 check screens

        public void ShowIntro(int index, int count, string title, string instructions)
        {
            var test = _runner != null && index - 1 < _runner.Tests.Count ? _runner.Tests[index - 1] : null;
            _inIntro = true;
            ShowCheck(test);
        }

        public void ShowTest(string title, string instructions)
        {
            _inIntro = false;
            ShowCheck(_runner != null ? _runner.CurrentTest : null);
        }

        void ShowCheck(CalibrationTest test)
        {
            ShowPage(Screen.Check);
            _lastStatus = "";
            _ringShown = 0;
            BuildCheck(test);
        }

        void BuildCheck(CalibrationTest test)
        {
            _checkTest = test;
            _states.Clear();
            _colLeft.Clear(); _colCenter.Clear(); _colRight.Clear();
            _drawing = null; _target = null; _ticks = null; _holdRing = null; _holdGlow = null;
            int index = test != null && _runner != null ? IndexOf(test) : 0;
            TopBar(index);
            bool keyboard = GlyphFamily == GlyphFamily.Keyboard;
            bool reach = test is ButtonAccessTest;
            _colLeft.EnableInClassList("ac-col-left--reach", reach);

            // ---- left column
            switch (test)
            {
                case ReactionTest _:
                    LeftColumn(ScreenTexts.ReactionTitle, ScreenTexts.ReactionInstruction, ScreenTexts.ReactionMeasure, "sound", ScreenTexts.ReactionTipTitle, ScreenTexts.ReactionTip, ScreenTexts.ReactionPlayTone);
                    break;
                case HoldTest _:
                    LeftColumn(ScreenTexts.HoldTitle, keyboard ? ScreenTexts.HoldInstructionKey : ScreenTexts.HoldInstruction, ScreenTexts.HoldMeasure, "grip", ScreenTexts.HoldTipTitle, ScreenTexts.HoldTip, null);
                    break;
                case TapTest _:
                    LeftColumn(ScreenTexts.PressesTitle, keyboard ? ScreenTexts.PressesInstructionKey : ScreenTexts.PressesInstruction, ScreenTexts.PressesMeasure, "pace", ScreenTexts.PressesTipTitle, ScreenTexts.PressesTip, null);
                    break;
                case StabilityTest s:
                    bool mouse = s.ActiveMode == StabilityTest.Mode.Mouse || (s.ActiveMode == StabilityTest.Mode.None && keyboard);
                    LeftColumn(ScreenTexts.SteadinessTitle, mouse ? ScreenTexts.SteadinessInstructionMouse : ScreenTexts.SteadinessInstructionStick, ScreenTexts.SteadinessMeasure, "hand", ScreenTexts.SteadinessTipTitle, ScreenTexts.SteadinessTip, null);
                    break;
                case ButtonAccessTest b:
                    var tb = E("ac-title-block", "ac-title-block--reach");
                    tb.Add(L(ScreenTexts.ReachTitle, "ac-check-title"));
                    tb.Add(Lines(ScreenTexts.ReachInstruction, "ac-instruction"));
                    _colLeft.Add(tb);
                    _reachList = E("ac-reach-list");
                    _colLeft.Add(_reachList);
                    break;
                default:
                    _colLeft.Add(L(test != null ? test.Title : "", "ac-check-title"));
                    break;
            }

            // ---- centre
            if (reach) BuildReachCentre();
            else
            {
                var stage = E("ac-stage");
                _colCenter.Add(stage);
                switch (test)
                {
                    case ReactionTest _: BuildReactionCentre(stage); break;
                    case HoldTest _: BuildHoldCentre(stage, keyboard); break;
                    case TapTest _: BuildTapCentre(stage, keyboard); break;
                    case StabilityTest s: BuildStabilityCentre(stage, s, keyboard); break;
                }
                _statusBlock = E("ac-status-block");
                _status = L("", "ac-status");
                _substatus = L("", "ac-substatus");
                _statusBlock.Add(_status);
                _statusBlock.Add(_substatus);
                _colCenter.Add(_statusBlock);
            }

            // ---- right
            _colRight.Add(L(ScreenTexts.YourOptions, "ac-section-label", "ac-options-label"));
            if (reach)
            {
                _colRight.Add(Option(ScreenTexts.ReachSkipButton, null, string.Format(CultureInfo.InvariantCulture, ScreenTexts.ReachWait, (test as ButtonAccessTest).SecondsPerButton)));
                _colRight.Add(Option(ScreenTexts.SkipCheck, PromptAction.Skip, null, () => SkipClicked?.Invoke()));
                var card = E("ac-tip-card");
                card.Add(L(ScreenTexts.ReachTipTitle, "ac-section-label"));
                card.Add(Lines(ScreenTexts.ReachTip));
                _colRight.Add(card);
            }
            else
            {
                _colRight.Add(Option(ScreenTexts.SkipCheck, PromptAction.Skip, null, () => SkipClicked?.Invoke()));
                _colRight.Add(Option(ScreenTexts.Back, PromptAction.Back, null));
                _colRight.Add(Option(ScreenTexts.SkipAll, PromptAction.SkipAll, ScreenTexts.HoldWord, holdWord: GlyphFamily != GlyphFamily.Keyboard));
                _colRight.Add(Lines(ScreenTexts.SkipNote, "ac-options-note"));
            }

            _ready = true;
            UpdateCheck(force: true);
            ApplyFontOverride();
        }

        int IndexOf(CalibrationTest test)
        {
            for (int i = 0; i < _runner.Tests.Count; i++) if (_runner.Tests[i] == test) return i;
            return 0;
        }

        void LeftColumn(string title, string[] instruction, string[] measure, string icon, string tipTitle, string[] tip, string link)
        {
            var tb = E("ac-title-block");
            tb.Add(L(title, "ac-check-title"));
            tb.Add(Lines(instruction, "ac-instruction"));
            _colLeft.Add(tb);
            _colLeft.Add(E("ac-divider"));
            var m = E("ac-measure");
            m.Add(L(ScreenTexts.WhatWeMeasure, "ac-section-label"));
            m.Add(Lines(measure));
            _colLeft.Add(m);
            var t = E("ac-tip");
            var head = E("ac-tip-head");
            var ic = new SvgDrawing("icon-" + icon);
            ic.AddToClassList("ac-tip-icon");
            head.Add(ic);
            head.Add(L(tipTitle, "ac-section-label"));
            t.Add(head);
            t.Add(Lines(tip));
            if (link != null) t.Add(Link(link, PlayCue, "ac-tip-link"));
            _colLeft.Add(t);
        }

        VisualElement Option(string label, PromptAction? glyph, string trailingText, Action onClick = null, bool holdWord = false)
        {
            var row = E("ac-option");
            row.Add(L(label, "ac-option-label"));
            var trailing = E("ac-option-trailing");
            if (glyph.HasValue)
            {
                if (holdWord && trailingText != null) trailing.Add(L(trailingText, "ac-option-hold"));
                trailing.Add(GlyphFor(glyph.Value));
            }
            else if (trailingText != null) trailing.Add(L(trailingText, "ac-option-wait"));
            row.Add(trailing);
            if (onClick != null) { row.pickingMode = PickingMode.Position; row.AddManipulator(new Clickable(onClick)); }
            return row;
        }

        VisualElement State(string name, VisualElement parent, params string[] classes)
        {
            var e = E(classes);
            e.name = "ac-state-" + name;
            parent.Add(e);
            _states[name] = e;
            return e;
        }

        void ShowState(string name)
        {
            CheckState = name;
            foreach (var kv in _states) Show(kv.Value, kv.Key == name);
        }

        SvgDrawing NewDrawing(string name)
        {
            var d = new SvgDrawing(name);
            d.name = "ac-drawing";
            _drawing = d;
            return d;
        }

        void BuildReactionCentre(VisualElement stage)
        {
            var wait = State("ready", stage, "ac-ring-box");
            wait.Add(E("ac-wait-outer"));
            var inner = new DashedCircle { Dash = 4, Gap = 4, Width = 1 };
            inner.style.left = 18; inner.style.top = 18; inner.style.width = 264; inner.style.height = 264;
            wait.Add(inner);
            wait.Add(L(ScreenTexts.ReactionWait, "ac-wait-text"));

            var signal = State("signal", stage, "ac-ring-box");
            signal.Add(E("ac-signal-glow"));
            for (int i = 0; i < 3; i++) signal.Add(E("ac-ripple"));   // motion (stage 4)
            var dashed = new DashedCircle { Dash = 4, Gap = 4, Width = 1 };
            dashed.AddToClassList("ac-dashed-circle--signal");
            dashed.style.left = -16; dashed.style.top = -16; dashed.style.width = 332; dashed.style.height = 332;
            signal.Add(dashed);
            var circle = E("ac-signal");
            circle.Add(L(ScreenTexts.ReactionSignal, "ac-signal-text"));
            signal.Add(circle);
        }

        void BuildHoldCentre(VisualElement stage, bool keyboard)
        {
            var ready = State("ready", stage);
            var d = NewDrawing(keyboard ? "keyboard-ansi" : "controller-hold-a");
            ready.Add(d);
            var ringBox = State("ring", stage, "ac-ring-box");
            _holdGlow = new HoldRing(glow: true);
            _holdRing = new HoldRing();
            ringBox.Add(_holdGlow);
            ringBox.Add(_holdRing);
            var value = E("ac-ring-box");
            _holdValue = L("", "ac-big-value");
            _holdUnit = L("", "ac-big-unit");
            value.Add(_holdValue);
            value.Add(_holdUnit);
            ringBox.Add(value);
        }

        void BuildTapCentre(VisualElement stage, bool keyboard)
        {
            var ready = State("ready", stage);
            var d = NewDrawing(keyboard ? "keyboard-ansi" : "controller-press-a");
            if (keyboard) d.ReplaceText("Hold Space", string.Format(ScreenTexts.PressesCallout, "Space"));
            ready.Add(d);
            var counter = State("counter", stage, "ac-counter");
            _ticks = new TickRing();
            counter.Add(_ticks);
            var value = E("ac-counter-value");
            _count = L("", "ac-count");
            value.Add(_count);
            value.Add(L(ScreenTexts.PressesUnit, "ac-count-unit"));
            counter.Add(value);
            var progress = E("ac-progress");
            _progressFill = E("ac-progress-fill");
            progress.Add(_progressFill);
            counter.Add(progress);
        }

        void BuildStabilityCentre(VisualElement stage, StabilityTest s, bool keyboard)
        {
            stage.AddToClassList("ac-stage--tall");
            bool mouse = s.ActiveMode == StabilityTest.Mode.Mouse || (s.ActiveMode == StabilityTest.Mode.None && keyboard);
            var ready = State("ready", stage, "ac-drawing-box");
            var drawing = NewDrawing(mouse ? "mouse" : "controller-left-stick");
            if (mouse)
            {
                var box = E();   // halo behind the mouse, in the drawing's own coordinates (300 × 340)
                box.style.width = 300; box.style.height = 340;
                box.Add(E("ac-mouse-halo"));
                drawing.style.position = Position.Absolute; drawing.style.left = 0; drawing.style.top = 0;
                box.Add(drawing);
                ready.Add(box);
            }
            else ready.Add(drawing);
            var m = State("target", stage, "ac-measurement");
            _target = new SteadinessTarget();
            m.Add(_target);
            var row = E("ac-measure-row");
            _measureState = L("", "ac-measure-state");
            _measureLeft = L("", "ac-measure-left");
            row.Add(_measureState);
            row.Add(_measureLeft);
            m.Add(row);
            var progress = E("ac-progress");
            _progressFill = E("ac-progress-fill");
            progress.Add(_progressFill);
            m.Add(progress);
        }

        void BuildReachCentre()
        {
            var stage = E("ac-reach-stage");
            stage.Add(NewDrawing("controller-reach-y"));
            _colCenter.Add(stage);
            var p = E("ac-reach-progress");
            var progress = E("ac-progress");
            _reachProgressFill = E("ac-progress-fill");
            progress.Add(_reachProgressFill);
            p.Add(progress);
            _reachCount = L("", "ac-reach-count");
            p.Add(_reachCount);
            _colCenter.Add(p);
            CheckState = "reach";
        }

        // ---- ICalibrationView (check screens)

        public void SetStatus(string text) => _lastStatus = text ?? "";

        public void ShowStimulus(bool visible)
        {
            if (_screen == Screen.Check && _checkTest is ReactionTest) UpdateCheck(force: true);
        }

        public void PlayCue()
        {
            if (_audio != null && _cue != null) _audio.PlayOneShot(_cue);
        }

        public void SetPrompt(string text) { /* the reach list and the drawing follow the running test */ }

        public void ShowStabilityArena(bool visible) { /* the centre follows the running test */ }

        public void SetCursor(float xInRadii, float yInRadii)
        {
            if (_target == null) return;
            var c = new Vector2(xInRadii, yInRadii);
            if (c.sqrMagnitude > 1.1f * 1.1f) c = c.normalized * 1.1f;
            _target.Cursor = c;
        }

        // ---- live progress

        void Update()
        {
            if (_screen == Screen.Check) UpdateCheck(force: false);
        }

        void UpdateCheck(bool force)
        {
            var test = _checkTest;
            bool running = test != null && test.IsRunning && !_inIntro;
            switch (test)
            {
                case ReactionTest r:
                {
                    bool signal = running && r.StimulusVisible;
                    ShowState(signal ? "signal" : "ready");
                    SetStatusLines(signal ? ScreenTexts.ReactionPress : ScreenTexts.ReactionGetReady,
                                   string.Format(ScreenTexts.TryOf, r.TrialNumber, r.Trials));
                    _ready = !signal;
                    break;
                }
                case HoldTest h:
                {
                    bool holding = running && h.IsHolding;
                    ShowState(holding ? "ring" : "ready");
                    float target = (float)(h.CurrentHoldSeconds / h.MaxHoldSeconds);
                    _ringShown = _reduceMotion || force ? target : Mathf.MoveTowards(_ringShown, target, Time.unscaledDeltaTime * 4f);
                    if (_holdRing != null) { _holdRing.Value = _ringShown; _holdGlow.Value = _ringShown; }
                    if (_holdValue != null)
                    {
                        _holdValue.text = h.CurrentHoldSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                        _holdUnit.text = string.Format(CultureInfo.InvariantCulture, ScreenTexts.HoldSecondsOf, h.MaxHoldSeconds);
                    }
                    SetStatusLines(holding ? string.Format(ScreenTexts.HoldKeep, MainButton) : string.Format(ScreenTexts.HoldReady, MainButton),
                                   holding ? ScreenTexts.HoldLetGo : string.Format(ScreenTexts.TryOf, h.TrialNumber, h.Trials));
                    _ready = !holding;
                    break;
                }
                case TapTest t:
                {
                    bool counting = running && t.IsCounting;
                    ShowState(counting ? "counter" : "ready");
                    if (_ticks != null) _ticks.Lit = t.PressCount;
                    if (_count != null) _count.text = t.PressCount.ToString(CultureInfo.InvariantCulture);
                    if (_progressFill != null) _progressFill.style.width = Length.Percent((float)(1 - t.SecondsLeft / t.WindowSeconds) * 100f);
                    SetStatusLines(counting ? ScreenTexts.PressesKeepGoing : string.Format(ScreenTexts.PressesReady, MainButton),
                                   counting ? string.Format(CultureInfo.InvariantCulture, ScreenTexts.SecondsLeft, t.SecondsLeft) : ScreenTexts.PressesReadySub);
                    _ready = !counting;
                    break;
                }
                case StabilityTest s:
                {
                    bool active = running && (s.MovedSamples > 0 || s.CurrentPhase == StabilityTest.Phase.Settling || s.CurrentPhase == StabilityTest.Phase.Measuring);
                    bool measuring = running && s.CurrentPhase == StabilityTest.Phase.Measuring;
                    ShowState(active ? "target" : "ready");
                    if (_measureState != null)
                    {
                        _measureState.text = measuring ? ScreenTexts.SteadinessMeasuring : "";
                        _measureLeft.text = string.Format(CultureInfo.InvariantCulture, ScreenTexts.SecondsLeft, s.SecondsLeft);
                        _progressFill.style.width = Length.Percent((float)(1 - s.SecondsLeft / s.MeasureSeconds) * 100f);
                    }
                    SetStatusLines(measuring ? ScreenTexts.SteadinessKeep : ScreenTexts.SteadinessReady,
                                   measuring ? ScreenTexts.SteadinessKeepSub : ScreenTexts.SteadinessReadySub);
                    _ready = !active;
                    break;
                }
                case ButtonAccessTest b:
                    UpdateReach(b, running);
                    _ready = true;
                    break;
            }
            if (force || _ready != _replayShown)
            {
                _replayShown = _ready;
                BottomBar(ScreenTexts.ButtonsMatch, _ready, false, CheckHints);
            }
        }
        bool _replayShown;

        void SetStatusLines(string status, string sub)
        {
            if (_status == null) return;
            if (_status.text != status) _status.text = status;
            if (_substatus.text != sub) _substatus.text = sub;
        }

        void UpdateReach(ButtonAccessTest b, bool running)
        {
            int n = b.Buttons.Count;
            int current = running ? b.PromptIndex : -1;
            if (_reachList.childCount != n) { _reachList.Clear(); for (int i = 0; i < n; i++) _reachList.Add(E("ac-reach-row")); }
            for (int i = 0; i < n; i++)
            {
                var row = _reachList[i];
                bool done = i < b.ReachSeconds.Count, isCurrent = i == current && !done;
                double seconds = done ? b.ReachSeconds[i] : double.NaN;
                string status = done ? (double.IsNaN(seconds) ? ScreenTexts.ReachMissed : ScreenTexts.ReachReached) : isCurrent ? ScreenTexts.ReachPressNow : ScreenTexts.ReachNext;
                string time = done && !double.IsNaN(seconds) ? string.Format(CultureInfo.InvariantCulture, ScreenTexts.ReachTime, seconds)
                    : isCurrent ? string.Format(CultureInfo.InvariantCulture, ScreenTexts.SecondsLeft, b.PromptSecondsLeft) : "";
                string key = status + "|" + time + "|" + GlyphFamily;
                if ((string)row.userData == key) continue;
                row.userData = key;
                row.Clear();
                row.EnableInClassList("ac-reach-row--current", isCurrent);
                row.EnableInClassList("ac-reach-row--next", !done && !isCurrent);
                var left = E("ac-reach-left");
                var glyph = InputGlyphs.ForBinding(GlyphFamily, b.Buttons[i].Bindings);
                left.Add(GlyphOf(glyph, !done && !isCurrent ? "ac-glyph--mini-key" : null).WithKeyClass(glyph));
                left.Add(L(status, "ac-reach-status"));
                row.Add(left);
                if (time.Length > 0) row.Add(L(time, "ac-reach-time"));
            }
            int shown = Math.Min(current < 0 ? b.ReachSeconds.Count : current + 1, n);
            _reachCount.text = string.Format(ScreenTexts.ReachCount, Math.Max(shown, 1), n);
            _reachProgressFill.style.width = Length.Percent(n == 0 ? 0 : 100f * Math.Max(shown, 1) / n);
        }

        // ================================================================ 12 suggestions / 13 no changes

        public void ShowConfirmation(ProposalReview review)
        {
            _review = review;
            _checkTest = null;
            _rows.Clear();
            _selectable.Clear();
            bool nothing = review.Items.Count == 0;
            ShowPage(nothing ? Screen.NoChanges : Screen.Suggestions);
            int done = _runner != null ? _runner.Tests.Count : 5;
            if (nothing) { BuildNoChanges(done); return; }

            int on = review.Items.Count(i => i.Accepted);
            TopBar(done, rightText: CountText(review.Items.Count, on), showChange: false);
            _topCount = _topRight.Q<Label>(className: "ac-top-device-name");
            var page = _pages[Screen.Suggestions];
            page.Clear();
            var left = E("ac-suggest-left");
            left.Add(L(ScreenTexts.SuggestTitle, "ac-page-title"));
            left.Add(L(ScreenTexts.SuggestSub, "ac-page-sub"));
            _list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            _list.AddToClassList("ac-suggest-list");
            for (int i = 0; i < review.Items.Count; i++)
            {
                int index = i;
                var row = E("ac-suggestion");
                row.name = "ac-row-" + i;
                row.pickingMode = PickingMode.Position;
                var text = E("ac-suggestion-text");
                text.Add(L(ProposalTexts.Title(review.Items[i].Change), "ac-suggestion-title"));
                text.Add(L("", "ac-suggestion-detail"));
                row.Add(text);
                var toggle = E("ac-toggle");
                toggle.Add(E("ac-toggle-knob"));
                row.Add(toggle);
                row.RegisterCallback<ClickEvent>(_ => _runner?.SetAccepted(index, !_review.Items[index].Accepted));
                _list.Add(row);
                _rows.Add(row);
                _selectable.Add(row);
            }
            left.Add(_list);
            page.Add(left);

            var right = E("ac-suggest-right");
            _detail = E("ac-detail");
            right.Add(_detail);
            var buttons = E("ac-buttons");
            buttons.pickingMode = PickingMode.Position;
            _apply = Btn("", true, false, 340, () => _runner?.Accept(), PromptAction.Confirm, "ac-glyph--small");
            _applyLabel = _apply.Q<Label>(className: "ac-btn-label");
            _redo = Btn(ScreenTexts.Redo, false, false, 150, () => _runner?.Redo());
            _notNow = Btn(ScreenTexts.NotNow, false, false, 130, () => _runner?.Skip());
            buttons.Add(_apply); buttons.Add(_redo); buttons.Add(_notNow);
            right.Add(buttons);
            page.Add(right);
            _selectable.Add(_apply); _selectable.Add(_redo); _selectable.Add(_notNow);

            BottomBar(null, false, false, (PromptAction.Move, ScreenTexts.HintSelect), (PromptAction.Toggle, ScreenTexts.HintToggle), (PromptAction.Adjust, ScreenTexts.HintAdjust));
            _detailIndex = 0;
            RefreshConfirmation();
            ApplyFontOverride();
        }

        static string CountText(int count, int on) =>
            count == 1 ? string.Format(ScreenTexts.SuggestCountOne, on) : string.Format(ScreenTexts.SuggestCount, count, on);

        void BuildNoChanges(int done)
        {
            TopBar(done, rightText: "", showChange: false);
            var page = _pages[Screen.NoChanges];
            page.Clear();
            page.Add(L(ScreenTexts.NoChangesTitle, "ac-nochange-title"));
            page.Add(Lines(ScreenTexts.NoChangesText, "ac-nochange-text", "ac-lines--center"));
            var checks = E("ac-nochange-checks");
            checks.Add(E("ac-nochange-line"));
            foreach (var name in ScreenTexts.NoChangesChecks)
            {
                var c = E("ac-nochange-check");
                c.Add(E("ac-nochange-dot"));
                c.Add(L(name, "ac-nochange-name"));
                checks.Add(c);
            }
            page.Add(checks);
            var buttons = E("ac-buttons");
            buttons.pickingMode = PickingMode.Position;
            _continue = Btn(ScreenTexts.Continue, true, true, 240, () => _runner?.Accept(), PromptAction.Confirm);
            _continue.AddToClassList("ac-btn--glyph-30");
            _redoEmpty = Btn(ScreenTexts.Redo, false, true, 170, () => _runner?.Redo());
            buttons.Add(_continue); buttons.Add(_redoEmpty);
            page.Add(buttons);
            _selectable.Add(_continue); _selectable.Add(_redoEmpty);
            BottomBar(ScreenTexts.NoChangesNote, false, false, (PromptAction.Move, ScreenTexts.HintSelect), (PromptAction.Confirm, ScreenTexts.HintConfirm));
            ApplyFontOverride();
        }

        public void SetConfirmationSelection(int index)
        {
            _selection = index;
            for (int i = 0; i < _selectable.Count; i++) _selectable[i].EnableInClassList("ac-selected", i == index);
            if (_review != null && index >= 0 && index < _review.Items.Count)
            {
                _detailIndex = index;
                BuildDetail();
                _list?.ScrollTo(_selectable[index]);
            }
        }

        public void RefreshConfirmation()
        {
            if (_review == null || _review.Items.Count == 0) return;
            int on = 0;
            for (int i = 0; i < _rows.Count && i < _review.Items.Count; i++)
            {
                var item = _review.Items[i];
                if (item.Accepted) on++;
                var row = _rows[i];
                row.EnableInClassList("ac-suggestion--off", !item.Accepted);
                row.Q<Label>(className: "ac-suggestion-detail").text = item.Accepted ? ScreenTexts.RowDetail(item.Change)
                    : item.Change.IsUncertain ? ScreenTexts.RowOffUncertain : ScreenTexts.RowOffByPlayer;
                row.Q(className: "ac-toggle").EnableInClassList("ac-toggle--on", item.Accepted);
            }
            _applyLabel.text = on == 0 ? ScreenTexts.ApplyNone : on == 1 ? ScreenTexts.ApplyOne : string.Format(ScreenTexts.ApplyMany, on);
            if (_topCount != null) _topCount.text = CountText(_review.Items.Count, on);
            BuildDetail();
        }

        /// <summary>The detail panel of the selected suggestion: why, in the game, default vs with this, and (timing, aim assist) the advanced slider.</summary>
        void BuildDetail()
        {
            if (_detail == null || _review == null || _review.Items.Count == 0) return;
            var c = _review.Items[Mathf.Clamp(_detailIndex, 0, _review.Items.Count - 1)].Change;
            _detail.Clear();
            _detail.Add(L(ProposalTexts.Title(c), "ac-detail-title"));
            var grid = E("ac-detail-grid");
            var why = E("ac-detail-col");
            why.Add(L(ScreenTexts.Why, "ac-section-label"));
            why.Add(Lines(ScreenTexts.WhyLines(c)));
            var game = E("ac-detail-col");
            game.Add(L(ScreenTexts.InTheGame, "ac-section-label"));
            game.Add(Lines(ScreenTexts.InGameLines(c)));
            grid.Add(why); grid.Add(game);
            _detail.Add(grid);

            var bars = E("ac-bars");
            switch (c.Setting)
            {
                case "timingMultiplier":
                {
                    float m = float.Parse(c.ProposedValue, CultureInfo.InvariantCulture);
                    const float window = 0.8f, max = 2.0f;
                    bars.Add(Bar(ScreenTexts.Default, window / max, string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", window), false));
                    bars.Add(Bar(ScreenTexts.WithThis, window * m / max, string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", window * m), true));
                    break;
                }
                case "aimAssist":
                    bars.Add(Bar(ScreenTexts.Default, 0, ScreenTexts.Off, false));
                    bars.Add(Bar(ScreenTexts.WithThis, AimLevel(c.ProposedValue) / 3f, c.ProposedValue, true));
                    break;
                case "stickDeadzone":
                {
                    float d = float.Parse(c.ProposedValue, CultureInfo.InvariantCulture), def = AccessCalibrate.Config.DefaultStickDeadzone;
                    bars.Add(Bar(ScreenTexts.Default, def / 0.5f, def.ToString("0.00", CultureInfo.InvariantCulture), false));
                    bars.Add(Bar(ScreenTexts.WithThis, d / 0.5f, d.ToString("0.00", CultureInfo.InvariantCulture), true));
                    break;
                }
                default:   // on/off settings: Off → On, no bars
                    bars.Add(OnOff(ScreenTexts.Default, ScreenTexts.Off, false));
                    bars.Add(OnOff(ScreenTexts.WithThis, ScreenTexts.On, true));
                    break;
            }
            _detail.Add(bars);
            _detail.Add(E("ac-detail-spacer"));

            if (c.Setting == "timingMultiplier") _detail.Add(Advanced("×" + c.ProposedValue, new[] { "×1.0", "×1.5", "×2.0", "×2.5" },
                (float.Parse(c.ProposedValue, CultureInfo.InvariantCulture) - 1f) / 1.5f, "×" + c.ProposedValue));
            else if (c.Setting == "aimAssist") _detail.Add(Advanced(c.ProposedValue, new[] { "Off", "Low", "Medium", "High" }, AimLevel(c.ProposedValue) / 3f, c.ProposedValue));
        }

        static int AimLevel(string v) => v == "Low" ? 1 : v == "Medium" ? 2 : v == "High" ? 3 : 0;

        VisualElement Bar(string label, float fraction, string value, bool isNew)
        {
            var row = E("ac-bar-row");
            var l = L(label, "ac-bar-label"); l.EnableInClassList("ac-bar-label--new", isNew); row.Add(l);
            var bar = E("ac-bar");
            var fill = E("ac-bar-fill"); fill.EnableInClassList("ac-bar-fill--new", isNew);
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            bar.Add(fill); row.Add(bar);
            var v = L(value, "ac-bar-value"); v.EnableInClassList("ac-bar-value--new", isNew); row.Add(v);
            return row;
        }

        VisualElement OnOff(string label, string value, bool isNew)
        {
            var row = E("ac-bar-row");
            var l = L(label, "ac-bar-label"); l.EnableInClassList("ac-bar-label--new", isNew); row.Add(l);
            var v = L(value, "ac-bar-value"); v.EnableInClassList("ac-bar-value--new", isNew); v.style.width = StyleKeyword.Auto; v.style.marginLeft = 0;
            row.Add(v);
            return row;
        }

        VisualElement Advanced(string value, string[] ticks, float position, string suggested)
        {
            var adv = E("ac-advanced");
            var head = E("ac-advanced-head");
            head.Add(L(ScreenTexts.Advanced, "ac-section-label"));
            head.Add(L(value, "ac-advanced-value"));
            adv.Add(head);
            var slider = E("ac-slider");
            var dec = E("ac-slider-step"); dec.Add(L("‹", "ac-slider-step-text")); slider.Add(dec);
            var track = E("ac-slider-track");
            track.Add(E("ac-slider-line"));
            var fill = E("ac-slider-fill"); fill.style.width = Length.Percent(position * 100f); track.Add(fill);
            var thumb = E("ac-slider-thumb"); thumb.style.left = Length.Percent(position * 100f); track.Add(thumb);
            slider.Add(track);
            var inc = E("ac-slider-step"); inc.Add(L("›", "ac-slider-step-text")); slider.Add(inc);
            adv.Add(slider);
            var row = E("ac-slider-ticks");
            foreach (var t in ticks)
            {
                bool isSuggested = t == suggested;
                var l = L(isSuggested ? string.Format(ScreenTexts.Suggested, t) : t, "ac-slider-tick");
                l.EnableInClassList("ac-slider-tick--suggested", isSuggested);
                row.Add(l);
            }
            adv.Add(row);
            return adv;
        }

        // ================================================================ 14 summary

        /// <summary>
        /// The results after Apply: per check the measured value, a small scale with the rule's
        /// threshold, and the resulting setting. (Shown by the runner from stage 3 on; the feedback
        /// build has its own summary until then.)
        /// </summary>
        public void ShowSummary(Measurements m, ProposalReview review, bool applied)
        {
            ShowPage(Screen.Summary);
            _checkTest = null;
            _selectable.Clear();
            int done = _runner != null ? _runner.Tests.Count : 5;
            var page = _pages[Screen.Summary];
            page.Clear();
            int count = review.Items.Count, on = applied ? review.Items.Count(i => i.Accepted) : 0;
            var head = E("ac-summary-head");
            var titles = E();
            titles.Add(L(ScreenTexts.SummaryTitle, "ac-summary-title"));
            titles.Add(L(ScreenTexts.SummarySub, "ac-summary-sub"));
            head.Add(titles);
            head.Add(L(string.Format(ScreenTexts.SummaryApplied, on, count), "ac-summary-applied"));
            page.Add(head);

            var table = E("ac-table");
            var th = E("ac-table-head");
            th.Add(L(ScreenTexts.SummaryColCheck, "ac-table-col-label", "ac-col-check"));
            th.Add(L(ScreenTexts.SummaryColResult, "ac-table-col-label", "ac-col-result"));
            th.Add(L(ScreenTexts.SummaryColSetting, "ac-table-col-label", "ac-col-setting"));
            table.Add(th);
            var cfg = AccessCalibrate.Config;
            double reactionRule = cfg.For(m.Reaction.Device).ReactionSlowMs + cfg.ReactionLatencyMarginMs;
            var hold = cfg.For(m.Hold.Device); var taps = cfg.For(m.Taps.Device); var steady = cfg.For(m.Stability.Device);
            int buttons = Math.Max(1, m.Buttons.RequestedButtons.Count);
            table.Add(SummaryRow(0, m.Reaction, review, MeasurementKind.Reaction, string.Format(ScreenTexts.SummaryMeasure[0], Math.Max(0, m.Reaction.RawTrialsMs.Count - 1)),
                F(ScreenTexts.SummaryReaction, m.Reaction.MedianMs), F(ScreenTexts.RuleAboveMs, reactionRule), m.Reaction.MedianMs / 1000, reactionRule / 1000, applied));
            table.Add(SummaryRow(1, m.Hold, review, MeasurementKind.Hold, ScreenTexts.SummaryMeasure[1],
                F(ScreenTexts.SummaryHold, m.Hold.MaxHoldSeconds), F(ScreenTexts.RuleBelowSeconds, hold.HoldMinSeconds), m.Hold.MaxHoldSeconds / 5, hold.HoldMinSeconds / 5, applied));
            table.Add(SummaryRow(2, m.Taps, review, MeasurementKind.Taps, ScreenTexts.SummaryMeasure[2],
                F(ScreenTexts.SummaryTaps, m.Taps.PressesPerSecond), F(ScreenTexts.RuleBelowPerSecond, taps.TapMinPerSecond), m.Taps.PressesPerSecond / 8, taps.TapMinPerSecond / 8, applied));
            table.Add(SummaryRow(3, m.Stability, review, MeasurementKind.Stability, ScreenTexts.SummaryMeasure[3],
                F(ScreenTexts.SummaryStability, m.Stability.Spread), F(ScreenTexts.RuleAbove, steady.StabilityMaxSpread), m.Stability.Spread / 0.5, steady.StabilityMaxSpread / 0.5, applied));
            table.Add(SummaryRow(4, m.Buttons, review, MeasurementKind.Buttons, ScreenTexts.SummaryMeasure[4],
                F(ScreenTexts.SummaryButtons, m.Buttons.UnpressedButtons.Count, buttons), F(ScreenTexts.RuleButtons, 1), (double)m.Buttons.UnpressedButtons.Count / buttons, 1.0 / buttons, applied));
            page.Add(table);

            var row = E("ac-buttons");
            row.pickingMode = PickingMode.Position;
            var cont = Btn(ScreenTexts.SummaryContinue, true, false, 230, () => Hide(), PromptAction.Confirm);
            cont.AddToClassList("ac-btn--glyph-30");
            cont.style.paddingRight = 10;
            row.Add(cont);
            row.Add(Btn(ScreenTexts.SummaryCopy, false, false, 170, null));
            row.Add(Btn(ScreenTexts.SummaryRedo, false, false, 130, () => _runner?.Redo()));
            page.Add(row);

            TopBar(done, showChange: false);
            BottomBar(ScreenTexts.SummaryNote, false, false, (PromptAction.Move, ScreenTexts.HintSelect), (PromptAction.Confirm, ScreenTexts.HintConfirm));
            ApplyFontOverride();
        }

        static string F(string pattern, params object[] args) => string.Format(CultureInfo.InvariantCulture, pattern, args);

        VisualElement SummaryRow(int i, MeasurementBase measurement, ProposalReview review, MeasurementKind kind, string measure,
            string value, string rule, double valueFraction, double ruleFraction, bool applied)
        {
            var row = E("ac-summary-row");
            row.name = "ac-summary-row-" + i;
            var c1 = E("ac-col-check");
            c1.Add(L(ScreenTexts.NoChangesChecks[i], "ac-summary-check"));
            c1.Add(L(measure, "ac-summary-measure"));
            row.Add(c1);
            var c2 = E("ac-col-result");
            var vr = E("ac-summary-valuerow");
            bool usable = measurement.IsUsable;
            vr.Add(L(usable ? value : "—", "ac-summary-value"));
            vr.Add(L(rule, "ac-summary-rule"));
            c2.Add(vr);
            var scale = E("ac-scale");
            scale.Add(E("ac-scale-track"));
            var marker = E("ac-scale-marker"); marker.style.left = Length.Percent((float)Math.Min(1, ruleFraction) * 100f); marker.style.marginLeft = -1;
            scale.Add(marker);
            if (usable)
            {
                float x = (float)Math.Min(1, Math.Max(0, valueFraction)) * 100f;
                var glow = E("ac-scale-dot-glow"); glow.style.left = Length.Percent(x); glow.style.marginLeft = -6; scale.Add(glow);
                var dot = E("ac-scale-dot"); dot.style.left = Length.Percent(x); dot.style.marginLeft = -6; scale.Add(dot);
            }
            c2.Add(scale);
            row.Add(c2);
            var c3 = E("ac-col-setting", "ac-summary-setting-row");
            var item = review.Items.FirstOrDefault(it => it.Change.Source == kind);
            string setting = item != null ? ScreenTexts.SummarySetting(item.Change) : ScreenTexts.StatusNoChange;
            string status = !usable ? ScreenTexts.StatusSkipped : item == null ? "" : applied && item.Accepted ? ScreenTexts.StatusApplied : ScreenTexts.StatusNotApplied;
            c3.Add(L(setting, "ac-summary-setting"));
            var st = L(status, "ac-summary-status");
            st.EnableInClassList("ac-summary-status--off", status != ScreenTexts.StatusApplied);
            c3.Add(st);
            row.Add(c3);
            return row;
        }

        // ================================================================ 02 device picker

        /// <summary>"What are you playing with?" over the current screen (choosing is wired in stage 3).</summary>
        public void ShowDevicePicker()
        {
            _dialogLayer.Clear();
            var dialog = E("ac-dialog");
            dialog.pickingMode = PickingMode.Position;
            dialog.Add(L(ScreenTexts.DeviceTitle, "ac-dialog-title"));
            dialog.Add(Lines(ScreenTexts.DeviceText, "ac-dialog-text"));
            var list = E("ac-devices");
            var family = GlyphFamily;
            void Row(string name, string detail, bool selected)
            {
                var r = E("ac-device");
                r.EnableInClassList("ac-device--selected", selected);
                var t = E();
                t.Add(L(name, "ac-device-name"));
                t.Add(L(selected ? string.Format(ScreenTexts.DeviceDetected, detail) : detail, "ac-device-detail"));
                r.Add(t);
                r.Add(L(ScreenTexts.DeviceSelected, "ac-device-selected"));
                list.Add(r);
            }
            Row(ScreenTexts.DeviceXbox, ScreenTexts.DeviceXboxDetail, family == GlyphFamily.Xbox || family == GlyphFamily.Nintendo);
            Row(ScreenTexts.DevicePlayStation, ScreenTexts.DevicePlayStationDetail, family == GlyphFamily.PlayStation);
            Row(ScreenTexts.DeviceKeyboard, ScreenTexts.DeviceKeyboardDetail, family == GlyphFamily.Keyboard);
            if (UnityEngine.InputSystem.Touchscreen.current != null || ForceTouchRow) Row(ScreenTexts.DeviceTouch, ScreenTexts.DeviceTouchDetail, false);
            dialog.Add(list);
            var buttons = E("ac-buttons");
            buttons.pickingMode = PickingMode.Position;
            buttons.Add(Btn(ScreenTexts.DeviceUse, true, false, 334, HideDevicePicker, PromptAction.Confirm, "ac-glyph--small"));
            buttons.Add(Btn(ScreenTexts.HintCancel, false, false, 150, HideDevicePicker, PromptAction.Cancel, "ac-glyph--small"));
            dialog.Add(buttons);
            var wrap = new VisualElement { pickingMode = PickingMode.Ignore };
            wrap.Add(E("ac-dialog-shadow"));
            wrap.Add(dialog);
            _dialogLayer.Add(wrap);
            _dialogLayer.AddToClassList("ac-dialog-layer--shown");
            var change = _topRight.Q(className: "ac-top-change");
            if (change != null) Show(change, false);   // the picker is open: no "Change" behind it
            _dialogLayer.pickingMode = PickingMode.Position;
            var spec = _hintSpec;
            BottomBar(ScreenTexts.DeviceNote, false, false, (PromptAction.Move, ScreenTexts.HintSelect), (PromptAction.Confirm, ScreenTexts.HintConfirm), (PromptAction.Cancel, ScreenTexts.HintCancel));
            _specBeforeDialog = spec;
            ApplyFontOverride();
        }
        (PromptAction, string)[] _specBeforeDialog;

        /// <summary>Tests: show the picker as if Touch were available.</summary>
        internal bool ForceTouchRow;

        void HideDevicePicker()
        {
            if (_dialogLayer == null || !_dialogLayer.ClassListContains("ac-dialog-layer--shown")) return;
            _dialogLayer.RemoveFromClassList("ac-dialog-layer--shown");
            _dialogLayer.Clear();
            if (_screen == Screen.Check) UpdateCheck(force: true);
            else if (_specBeforeDialog != null) { _hintSpec = _specBeforeDialog; BuildHints(); }
        }

        // ================================================================ hide / cue

        public void Hide()
        {
            _screen = Screen.Hidden;
            _checkTest = null;
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>A short, soft 660 Hz tone (no file needed).</summary>
        static AudioClip MakeCue()
        {
            const int rate = 44100;
            const float seconds = 0.15f, freq = 660f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1f, i / (rate * 0.01f)) * Mathf.Min(1f, (n - i) / (rate * 0.03f));
                data[i] = 0.35f * envelope * Mathf.Sin(2 * Mathf.PI * freq * t);
            }
            var clip = AudioClip.Create("AccessCalibrate Cue", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    static class GlyphBadgeExtensions
    {
        /// <summary>Key-shaped glyphs (LB, RB, Space) in the reach list use the small key style.</summary>
        internal static GlyphBadge WithKeyClass(this GlyphBadge g, Glyph glyph)
        {
            if (glyph.IsKey) g.AddToClassList("ac-glyph--mini-key");
            return g;
        }
    }
}

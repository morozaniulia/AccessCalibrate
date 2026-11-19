// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using AccessCalibration.Core;
using AccessCalibration.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration
{
    /// <summary>
    /// The built-in "console overlay" screens (Docs/DESIGN_SPEC.md), built with UI Toolkit:
    /// UXML + USS in Resources/AccessCalibrate, every colour / radius / font a --ac-… token.
    ///
    /// It implements the unchanged <see cref="ICalibrationView"/> (plus the optional Welcome
    /// screen). Progress that the interface doesn't carry — hold seconds, steadiness time left,
    /// trial counts — is read from the running test's read-only properties each frame.
    /// </summary>
    public sealed class UIToolkitCalibrationView : MonoBehaviour, ICalibrationView, ICalibrationWelcomeView
    {
        const string ResourceFolder = "AccessCalibrate/";

        /// <summary>Raised when the on-screen "Skip" hint is clicked (mouse / touch).</summary>
        public event Action SkipClicked;

        enum Screen { Hidden, Welcome, Intro, Test, Confirm }

        UIDocument _doc;
        AudioSource _audio;
        AudioClip _cue;
        CalibrationRunner _runner;
        GlyphTracker _glyphs;
        Screen _screen = Screen.Hidden;
        readonly List<StyleSheet> _themeSheets = new List<StyleSheet>();
        bool _reduceMotion;

        VisualElement _root, _panel, _pageIntro, _pageTest, _pageConfirm, _hints;
        // welcome / intro
        Label _railEyebrow, _railNote, _introEyebrow, _introTitle, _introBody, _readyText, _readyWait, _notNowNote;
        VisualElement _railList, _welcomeButtons, _readyBar;
        Button _start, _notNow;
        GlyphBadge _introGlyph, _startGlyph;
        // test
        Label _chip, _testName, _trialLabel, _status, _stimLabel, _holdSeconds, _holdOf, _holdHeadline;
        Label _stabTitle, _stabBody, _stabMeasuring, _stabLeft, _genericBig, _genericSub, _toneText;
        VisualElement _trialBars, _stageReaction, _stageHold, _stageStability, _stageGeneric, _stim, _stabBar, _genericBar, _genericBarTrack, _toneNote;
        ProgressRing _ring;
        StabilityArena _arena;
        string _lastStatus = "";
        float _ringShown;
        // confirmation
        Label _confirmEyebrow, _confirmTitle, _confirmNote;
        ScrollView _list;
        VisualElement _listWrap;
        Label _empty;
        bool _anyRowFaded;
        IVisualElementScheduledItem _hideScrollbar;
        Button _apply, _redo, _skipAll;
        GlyphBadge _applyGlyph;
        ProposalReview _review;
        readonly List<SwitchElement> _switches = new List<SwitchElement>();
        readonly List<VisualElement> _selectable = new List<VisualElement>();
        // hint bar: what is shown, so it can be rebuilt when the device family changes
        (PromptAction action, string text, Action onClick)[] _hintSpec = Array.Empty<(PromptAction, string, Action)>();

        /// <summary>The panel this view draws into (e.g. to render it into a texture).</summary>
        public PanelSettings Panel => _doc != null ? _doc.panelSettings : null;
        internal VisualElement Root => _root;
        internal int StabilityTrailCount => _arena.TrailCount;
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

            _panel = _root.Q("ac-panel");
            _pageIntro = _root.Q("ac-page-intro");
            _pageTest = _root.Q("ac-page-test");
            _pageConfirm = _root.Q("ac-page-confirm");
            _hints = _root.Q("ac-hints");

            _railEyebrow = _root.Q<Label>("ac-rail-eyebrow");
            _railList = _root.Q("ac-rail-list");
            _railNote = _root.Q<Label>("ac-rail-note");
            _introEyebrow = _root.Q<Label>("ac-intro-eyebrow");
            _introTitle = _root.Q<Label>("ac-intro-title");
            _introBody = _root.Q<Label>("ac-intro-body");
            _welcomeButtons = _root.Q("ac-welcome-buttons");
            _start = _root.Q<Button>("ac-start");
            _notNow = _root.Q<Button>("ac-not-now");
            _notNowNote = _root.Q<Label>("ac-not-now-note");
            _readyBar = _root.Q("ac-ready-bar");
            _readyText = _root.Q<Label>("ac-ready-text");
            _readyWait = _root.Q<Label>("ac-ready-wait");
            _introGlyph = new GlyphBadge();
            _introGlyph.AddToClassList("ac-glyph--big");
            _root.Q("ac-intro-glyph").Add(_introGlyph);

            _start.text = "";
            var startLabel = new Label(CalibrationTexts.WelcomeStart) { pickingMode = PickingMode.Ignore };
            startLabel.AddToClassList("ac-btn-label");
            _start.Add(startLabel);
            _startGlyph = new GlyphBadge();
            _startGlyph.AddToClassList("ac-glyph--on-focus");
            _startGlyph.AddToClassList("ac-btn-glyph");
            _start.Add(_startGlyph);
            _start.clicked += () => _runner?.WelcomeStart();
            _notNow.text = CalibrationTexts.WelcomeNotNow;
            _notNow.clicked += () => _runner?.WelcomeNotNow();
            _notNowNote.text = CalibrationTexts.WelcomeNotNowNote;

            _chip = _root.Q<Label>("ac-chip");
            _testName = _root.Q<Label>("ac-test-name");
            _trialLabel = _root.Q<Label>("ac-trial-label");
            _trialBars = _root.Q("ac-trial-bars");
            _status = _root.Q<Label>("ac-status");
            _stageReaction = _root.Q("ac-stage-reaction");
            _stageHold = _root.Q("ac-stage-hold");
            _stageStability = _root.Q("ac-stage-stability");
            _stageGeneric = _root.Q("ac-stage-generic");
            _stim = _root.Q("ac-stim");
            _stimLabel = _root.Q<Label>("ac-stim-label");
            _stimLabel.text = CalibrationTexts.ReactionSignal;
            _ring = new ProgressRing();
            _root.Q("ac-hold-ring-host").Insert(0, _ring);
            _holdSeconds = _root.Q<Label>("ac-hold-seconds");
            _holdOf = _root.Q<Label>("ac-hold-of");
            _holdHeadline = _root.Q<Label>("ac-hold-headline");
            _arena = new StabilityArena();
            _root.Q("ac-arena-host").Add(_arena);
            _stabTitle = _root.Q<Label>("ac-stability-title");
            _stabBody = _root.Q<Label>("ac-stability-body");
            _stabMeasuring = _root.Q<Label>("ac-stability-measuring");
            _stabLeft = _root.Q<Label>("ac-stability-left");
            _stabBar = _root.Q("ac-stability-bar");
            _genericBig = _root.Q<Label>("ac-generic-big");
            _genericSub = _root.Q<Label>("ac-generic-sub");
            _genericBar = _root.Q("ac-generic-bar");
            _genericBarTrack = _root.Q("ac-generic-bar-track");
            _toneNote = _root.Q("ac-tone-note");
            _toneText = _root.Q<Label>("ac-tone-text");
            _toneText.text = CalibrationTexts.ToneNote;
            _root.Q("ac-speaker").Add(new SpeakerIcon());

            _confirmEyebrow = _root.Q<Label>("ac-confirm-eyebrow");
            _confirmTitle = _root.Q<Label>("ac-confirm-title");
            _confirmNote = _root.Q<Label>("ac-confirm-note");
            _list = _root.Q<ScrollView>("ac-confirm-list");
            _listWrap = _root.Q("ac-confirm-list-wrap");
            _empty = _root.Q<Label>("ac-confirm-empty");
            _empty.text = CalibrationTexts.ConfirmNothing;
            _list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _list.verticalScroller.valueChanged += _ => { ShowScrollbarBriefly(); UpdateFade(); };
            _list.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateFade());
            _list.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateFade());
            _apply = _root.Q<Button>("ac-apply");
            _redo = _root.Q<Button>("ac-redo");
            _skipAll = _root.Q<Button>("ac-skip-all");
            _apply.text = "";
            var applyLabel = new Label { name = "ac-apply-label", pickingMode = PickingMode.Ignore };
            applyLabel.AddToClassList("ac-btn-label");
            _apply.Add(applyLabel);
            _applyGlyph = new GlyphBadge();
            _applyGlyph.AddToClassList("ac-glyph--on-focus");
            _applyGlyph.AddToClassList("ac-btn-glyph");
            _apply.Add(_applyGlyph);
            _redo.text = CalibrationTexts.ConfirmRedo;
            _skipAll.text = CalibrationTexts.ConfirmSkip;
            _apply.clicked += () => _runner?.Accept();
            _redo.clicked += () => _runner?.Redo();
            _skipAll.clicked += () => _runner?.Skip();

            _railEyebrow.text = CalibrationTexts.RailEyebrow;
            _confirmEyebrow.text = CalibrationTexts.ConfirmEyebrow;
            _confirmTitle.text = CalibrationTexts.ConfirmTitle;
            _confirmNote.text = CalibrationTexts.ConfirmNote;
            _readyText.text = CalibrationTexts.IntroPressToStart;
            _readyWait.text = CalibrationTexts.WaitingForInput;

            _glyphs = new GlyphTracker();
            _glyphs.Changed += _ => RefreshGlyphs();
            _cue = MakeCue();

            AccessCalibrate.OnThemeChanged += OnThemeChanged;
            CalibrationTheme.Edited += OnThemeEdited;
            ApplyTheme();
            RefreshGlyphs();
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
            _root.Query<VisualElement>().ForEach(e =>
                e.style.unityFontDefinition = font != null
                    ? new StyleFontDefinition(FontDefinition.FromSDFFont(font))
                    : new StyleFontDefinition(StyleKeyword.Null));
        }

        // ================================================================ shared pieces

        void ShowScreen(Screen screen, VisualElement page)
        {
            bool wasHidden = _screen == Screen.Hidden;
            bool pageChanged = !page.ClassListContains("ac-page--shown");
            _screen = screen;
            _root.style.display = DisplayStyle.Flex;
            foreach (var p in new[] { _pageIntro, _pageTest, _pageConfirm }) p.EnableInClassList("ac-page--shown", p == page);
            if ((wasHidden || pageChanged) && !_reduceMotion)
            {
                // Fade + 12 px rise, 200 ms (USS transition).
                _panel.AddToClassList("ac-panel--enter");
                _panel.schedule.Execute(() => _panel.RemoveFromClassList("ac-panel--enter")).ExecuteLater(16);
            }
        }

        void SetHints(params (PromptAction action, string text, Action onClick)[] hints)
        {
            _hintSpec = hints;
            _hints.Clear();
            foreach (var (action, text, onClick) in hints)
            {
                var hint = new VisualElement();
                hint.AddToClassList("ac-hint");
                var badge = new GlyphBadge();
                badge.Set(InputGlyphs.For(GlyphFamily, action));
                hint.Add(badge);
                var label = new Label(text);
                label.AddToClassList("ac-hint-text");
                hint.Add(label);
                if (onClick != null) hint.AddManipulator(new Clickable(onClick));
                _hints.Add(hint);
            }
            ApplyFontOverride();
        }

        void RefreshGlyphs()
        {
            var confirm = InputGlyphs.For(GlyphFamily, PromptAction.Confirm);
            _startGlyph.Set(confirm);
            _applyGlyph.Set(confirm);
            var any = InputGlyphs.For(GlyphFamily, PromptAction.AnyButton);
            _introGlyph.Set(any);
            _introGlyph.EnableInClassList("ac-glyph--big-key", any.IsKey);
            SetHints(_hintSpec);
        }

        void BuildRail(int current, int done)
        {
            _railList.Clear();
            var tests = _runner != null ? _runner.Tests : null;
            int count = tests != null ? tests.Count : 5;
            for (int i = 0; i < count; i++)
            {
                var item = new VisualElement();
                item.AddToClassList("ac-rail-item");
                bool isDone = i < done, isCurrent = i == current;
                item.EnableInClassList("ac-rail-item--done", isDone);
                item.EnableInClassList("ac-rail-item--current", isCurrent);
                if (isDone) item.Add(new CheckMark());
                else
                {
                    var num = new Label((i + 1).ToString("00", CultureInfo.InvariantCulture));
                    num.AddToClassList("ac-rail-num");
                    item.Add(num);
                }
                var name = new Label(tests != null ? tests[i].Title : "");
                name.AddToClassList("ac-rail-name");
                item.Add(name);
                _railList.Add(item);
            }
        }

        static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        // ================================================================ welcome

        public void ShowWelcome()
        {
            ShowScreen(Screen.Welcome, _pageIntro);
            BuildRail(-1, 0);
            _railNote.text = CalibrationTexts.RailNoteWelcome;
            Show(_introEyebrow, false);
            _introTitle.text = CalibrationTexts.WelcomeTitle;
            _introBody.text = CalibrationTexts.WelcomeBody;
            Show(_welcomeButtons, true);
            Show(_introGlyph.parent, false);
            Show(_readyBar, false);
            SetHints((PromptAction.Move, CalibrationTexts.MoveHint, null));
        }

        public void SetWelcomeSelection(int index)
        {
            _start.EnableInClassList("ac-selected", index == 0);
            _notNow.EnableInClassList("ac-selected", index == 1);
        }

        // ================================================================ ICalibrationView

        public void ShowIntro(int index, int count, string title, string instructions)
        {
            ShowScreen(Screen.Intro, _pageIntro);
            BuildRail(index - 1, index - 1);
            _railNote.text = CalibrationTexts.RailNoteIntro;
            Show(_introEyebrow, true);
            _introEyebrow.text = string.Format(CalibrationTexts.StepOf, index, count);
            _introTitle.text = title;
            _introBody.text = instructions;
            Show(_welcomeButtons, false);
            Show(_introGlyph.parent, true);
            Show(_readyBar, true);
            SetHints((PromptAction.Skip, CalibrationTexts.SkipCheckHint, () => SkipClicked?.Invoke()));
        }

        public void ShowTest(string title, string instructions)
        {
            ShowScreen(Screen.Test, _pageTest);
            var test = _runner != null ? _runner.CurrentTest : null;
            int index = test != null ? IndexOf(test) : 0;
            int count = _runner != null ? _runner.Tests.Count : 5;
            _chip.text = string.Format(CultureInfo.InvariantCulture, CalibrationTexts.Chip, index + 1, count);
            _testName.text = title;
            _lastStatus = "";
            _status.text = "";

            Show(_toneNote.parent, test is ReactionTest);   // the footer only exists for the tone note
            _stageReaction.EnableInClassList("ac-stage--shown", test is ReactionTest);
            _stageHold.EnableInClassList("ac-stage--shown", test is HoldTest);
            _stageStability.EnableInClassList("ac-stage--shown", test is StabilityTest);
            _stageGeneric.EnableInClassList("ac-stage--shown", test is TapTest || test is ButtonAccessTest || test == null);
            _stim.RemoveFromClassList("ac-stim--on");
            _ring.Value = _ringShown = 0;
            _arena.ClearTrail();
            _stabTitle.text = CalibrationTexts.StabilityHeadline;
            _stabBody.text = CalibrationTexts.StabilityBody;
            if (test == null) { _genericBig.text = title; _genericSub.text = instructions; }
            SetHints((PromptAction.Skip, CalibrationTexts.SkipHint, () => SkipClicked?.Invoke()));
            UpdateTestProgress();
        }

        int IndexOf(CalibrationTest test)
        {
            for (int i = 0; i < _runner.Tests.Count; i++) if (_runner.Tests[i] == test) return i;
            return 0;
        }

        public void SetStatus(string text)
        {
            _lastStatus = text ?? "";
            // Tests prefix "Try n of N · "; the top bar already shows that.
            int dot = _lastStatus.LastIndexOf(" · ", StringComparison.Ordinal);
            string shown = dot >= 0 ? _lastStatus.Substring(dot + 3) : _lastStatus;
            if (shown == CalibrationTexts.ReactionWait) shown = "";        // the ring says it
            _status.text = shown;
        }

        public void ShowStimulus(bool visible)
        {
            if (_runner != null && _runner.CurrentTest is ReactionTest)
                _stim.EnableInClassList("ac-stim--on", visible);   // appears; never flashes
        }

        public void PlayCue()
        {
            if (_audio != null && _cue != null) _audio.PlayOneShot(_cue);
        }

        public void SetPrompt(string text)
        {
            if (_runner != null && _runner.CurrentTest is ButtonAccessTest && !string.IsNullOrEmpty(text))
                _genericBig.text = text;
        }

        public void ShowStabilityArena(bool visible) { /* the stage follows the running test */ }

        public void SetCursor(float xInRadii, float yInRadii)
        {
            var test = _runner != null ? _runner.CurrentTest as StabilityTest : null;
            bool stick = test != null && test.ActiveMode == StabilityTest.Mode.Stick;
            Vector2 target = stick ? test.StickTarget : Vector2.zero;
            float radius = stick ? test.StickRadius : 0.25f;
            _arena.Target = target;
            _arena.TargetRadius = radius;
            var p = target + new Vector2(xInRadii, yInRadii) * radius;
            if (p.sqrMagnitude > 1f) p = p.normalized;
            _arena.SetCursor(p);
        }

        public void ShowConfirmation(ProposalReview review)
        {
            ShowScreen(Screen.Confirm, _pageConfirm);
            _review = review;
            _list.Clear();
            _switches.Clear();
            _selectable.Clear();

            // Nothing proposed: "No changes needed", a centred message instead of the list, no
            // "Turn on / off" hint, and no "Not now" (it would do the same as "Continue without changes").
            bool nothing = review.Items.Count == 0;
            _listWrap.EnableInClassList("ac-confirm-list-wrap--empty", nothing);
            _confirmTitle.text = nothing ? CalibrationTexts.ConfirmTitleNothing : CalibrationTexts.ConfirmTitle;
            _skipAll.style.display = nothing ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < review.Items.Count; i++)
            {
                var item = review.Items[i];
                int index = i;
                var row = new VisualElement { name = "ac-row-" + i };
                row.AddToClassList("ac-row");
                if (item.Change.IsUncertain)
                {
                    row.AddToClassList("ac-row--uncertain");
                    row.Add(new DashedOutline());
                }
                var text = new VisualElement();
                text.AddToClassList("ac-row-text");
                var title = new Label(ProposalTexts.Title(item.Change));
                title.AddToClassList("ac-row-title");
                var reason = new Label(ProposalTexts.Reason(item.Change));
                reason.AddToClassList("ac-row-reason");
                text.Add(title);
                text.Add(reason);
                row.Add(text);
                var sw = new SwitchElement();
                row.Add(sw);
                row.RegisterCallback<ClickEvent>(_ => _runner?.SetAccepted(index, !_review.Items[index].Accepted));
                _list.Add(row);
                _switches.Add(sw);
                _selectable.Add(row);
            }
            _selectable.Add(_apply);
            _selectable.Add(_redo);
            if (!nothing) _selectable.Add(_skipAll);
            RefreshConfirmation();
            _list.scrollOffset = Vector2.zero;
            UpdateFade();
            // Apply already shows its own confirm glyph on the button, so the bar only shows on/off;
            // with nothing to switch, no hint at all.
            if (review.Items.Count > 0) SetHints((PromptAction.Toggle, CalibrationTexts.ToggleHint, null));
            else SetHints();
        }

        /// <summary>True while at least one row is faded (hidden rows above or below the visible part).</summary>
        internal bool ListFadeVisible => _anyRowFaded;
        internal ScrollView ConfirmList => _list;

        /// <summary>Height of the soft edge, in px, and the opacity a row reaches right at the edge.</summary>
        const float FadeZone = 60f, FadeMin = 0.15f;

        /// <summary>
        /// Soft edges without drawing anything on top: each row's opacity drops from 1 to 0.15 as its
        /// centre gets within 60 px of an edge — but only an edge with hidden content beyond it:
        /// the top once scrolled down, the bottom while more rows are below. When everything fits,
        /// nothing is faded. The selected row is never faded.
        /// </summary>
        void UpdateFade()
        {
            _anyRowFaded = false;
            if (_list == null || _review == null) return;
            float content = _list.contentContainer.layout.height;
            float viewportHeight = _list.contentViewport.layout.height;
            if (float.IsNaN(content) || float.IsNaN(viewportHeight)) return;

            bool overflow = content > viewportHeight + 1f;
            float y = _list.scrollOffset.y;
            bool moreBelow = overflow && y < content - viewportHeight - 1f;
            bool moreAbove = overflow && y > 1f;
            var viewport = _list.contentViewport.worldBound;

            for (int i = 0; i < _review.Items.Count && i < _selectable.Count; i++)
            {
                var row = _selectable[i];
                float opacity = 1f;
                if (!row.ClassListContains("ac-selected"))
                {
                    float centre = row.worldBound.center.y;
                    if (moreBelow) opacity = Mathf.Min(opacity, Mathf.Lerp(FadeMin, 1f, Mathf.Clamp01((viewport.yMax - centre) / FadeZone)));
                    if (moreAbove) opacity = Mathf.Min(opacity, Mathf.Lerp(FadeMin, 1f, Mathf.Clamp01((centre - viewport.yMin) / FadeZone)));
                }
                row.style.opacity = opacity;
                if (opacity < 0.999f) _anyRowFaded = true;
            }
        }

        /// <summary>The thin scrollbar appears while scrolling (and on hover, via USS), then fades out.</summary>
        void ShowScrollbarBriefly()
        {
            _list.AddToClassList("ac-scrolling");
            _hideScrollbar?.Pause();
            _hideScrollbar = _list.schedule.Execute(() => _list.RemoveFromClassList("ac-scrolling"));
            _hideScrollbar.ExecuteLater(900);
        }

        public void SetConfirmationSelection(int index)
        {
            for (int i = 0; i < _selectable.Count; i++) _selectable[i].EnableInClassList("ac-selected", i == index);
            if (_review != null && index >= 0 && index < _review.Items.Count) _list.ScrollTo(_selectable[index]);
            UpdateFade();
        }

        public void RefreshConfirmation()
        {
            if (_review == null) return;
            int on = 0;
            for (int i = 0; i < _switches.Count && i < _review.Items.Count; i++)
            {
                _switches[i].On = _review.Items[i].Accepted;
                if (_review.Items[i].Accepted) on++;
            }
            _apply.Q<Label>("ac-apply-label").text =
                on == 0 ? CalibrationTexts.ConfirmApplyNone
                : on == 1 ? CalibrationTexts.ConfirmApplyOne
                : string.Format(CalibrationTexts.ConfirmApplyMany, on);
        }

        public void Hide()
        {
            _screen = Screen.Hidden;
            _root.style.display = DisplayStyle.None;
        }

        // ================================================================ live progress

        void Update()
        {
            if (_screen == Screen.Test) UpdateTestProgress();
        }

        void SetTrials(string label, int count, int current, int done)
        {
            _trialLabel.text = label;
            if (_trialBars.childCount != count)
            {
                _trialBars.Clear();
                for (int i = 0; i < count; i++)
                {
                    var bar = new VisualElement();
                    bar.AddToClassList("ac-trial-bar");
                    _trialBars.Add(bar);
                }
            }
            for (int i = 0; i < count; i++)
            {
                var bar = _trialBars[i];
                bar.EnableInClassList("ac-trial-bar--done", i < done);
                bar.EnableInClassList("ac-trial-bar--current", i == current && i >= done);
            }
        }

        void UpdateTestProgress()
        {
            var test = _runner != null ? _runner.CurrentTest : null;
            switch (test)
            {
                case ReactionTest r:
                    SetTrials(string.Format(CalibrationTexts.ReactionTrial, r.TrialNumber, r.Trials), r.Trials, r.TrialNumber - 1, r.TrialNumber - 1);
                    if (string.IsNullOrEmpty(_status.text)) _status.text = CalibrationTexts.ReactionLineBelow;
                    break;

                case HoldTest h:
                    SetTrials(string.Format(CalibrationTexts.HoldTrial, h.TrialNumber, h.Trials), h.Trials, h.TrialNumber - 1, h.TrialNumber - 1);
                    float target = (float)(h.CurrentHoldSeconds / h.MaxHoldSeconds);
                    _ringShown = _reduceMotion ? target : Mathf.MoveTowards(_ringShown, target, Time.unscaledDeltaTime * 4f);
                    _ring.Value = _ringShown;
                    _holdSeconds.text = h.CurrentHoldSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                    _holdOf.text = string.Format(CultureInfo.InvariantCulture, CalibrationTexts.HoldSecondsOf, h.MaxHoldSeconds);
                    if (h.IsHolding)
                    {
                        _holdHeadline.text = CalibrationTexts.HoldKeepHolding;
                        _status.text = CalibrationTexts.HoldLetGoWhen;
                    }
                    else
                    {
                        _holdHeadline.text = StatusTail();
                        _status.text = "";
                    }
                    break;

                case TapTest t:
                    SetTrials(string.Format(CultureInfo.InvariantCulture, CalibrationTexts.SecondsLeft, t.SecondsLeft), 0, -1, 0);
                    _genericBig.text = t.PressCount.ToString(CultureInfo.InvariantCulture);
                    _genericSub.text = CalibrationTexts.TapsPresses;
                    Show(_genericBarTrack, true);
                    _genericBar.style.width = Length.Percent((float)(1 - t.SecondsLeft / t.WindowSeconds) * 100f);
                    if (_status.text.StartsWith("Presses", StringComparison.Ordinal)) _status.text = "";
                    break;

                case StabilityTest s:
                    SetTrials(s.ActiveMode == StabilityTest.Mode.Mouse ? CalibrationTexts.DeviceMouse : CalibrationTexts.DeviceLeftStick, 0, -1, 0);
                    bool measuring = s.CurrentPhase == StabilityTest.Phase.Measuring;
                    _stabMeasuring.text = measuring ? CalibrationTexts.StabilityMeasuring : StatusTail();
                    _stabLeft.text = string.Format(CultureInfo.InvariantCulture, CalibrationTexts.SecondsLeft, s.SecondsLeft);
                    _stabBar.style.width = Length.Percent((float)(1 - s.SecondsLeft / s.MeasureSeconds) * 100f);
                    _status.text = "";
                    break;

                case ButtonAccessTest b:
                    int n = b.Buttons.Count;
                    SetTrials(string.Format(CalibrationTexts.ButtonsCount, Math.Min(b.PromptIndex + 1, n), n), n, b.PromptIndex, b.PromptIndex);
                    _genericSub.text = CalibrationTexts.ButtonsSub;
                    Show(_genericBarTrack, true);
                    _genericBar.style.width = Length.Percent((float)(1 - b.PromptSecondsLeft / b.SecondsPerButton) * 100f);
                    break;
            }
        }

        string StatusTail()
        {
            int dot = _lastStatus.LastIndexOf(" · ", StringComparison.Ordinal);
            return dot >= 0 ? _lastStatus.Substring(dot + 3) : _lastStatus;
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
}

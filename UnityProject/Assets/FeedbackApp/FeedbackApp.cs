// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Runtime.InteropServices;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AccessCalibration.FeedbackApp
{
    /// <summary>
    /// The playable feedback build (WebGL and Windows): opens straight into the calibration and,
    /// after the suggestions screen, shows a summary with "Copy results" and "Redo".
    /// Nothing is sent anywhere; the profile is kept in a temporary folder, so every launch starts
    /// with a fresh calibration.
    /// </summary>
    [RequireComponent(typeof(CalibrationRunner))]
    public sealed class FeedbackApp : MonoBehaviour
    {
        /// <summary>Input is ignored this long after the summary opens, so the Apply press can't also press a button here.</summary>
        public double InputDelaySeconds = 0.4;

        CalibrationRunner _runner;
        ProposalReview _review;
        Measurements _measurements;
        bool _applied;

        UIDocument _doc;
        VisualElement _root, _panel, _measured, _suggested;
        Label _appliedLabel, _status;
        Button _copy, _redo;
        int _selection;
        double _shownAt;
        InputAction _left, _right, _submit;

        public bool SummaryVisible { get; private set; }
        /// <summary>The last block handed to the clipboard (tests, and the "copy failed" case).</summary>
        public string LastResultsText { get; private set; }
        /// <summary>null until a copy finishes; then true / false.</summary>
        public bool? LastCopySucceeded { get; private set; }
        public VisualElement SummaryRoot => _root;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AC_CopyToClipboard(string text, string objectName, string methodName);
        [DllImport("__Internal")] static extern void AC_SetState(string state);
#endif
        string _state;

        void Awake()
        {
            _runner = GetComponent<CalibrationRunner>();
            AccessCalibrate.UseStore(new ProfileStore(Path.Combine(Application.temporaryCachePath, "AccessCalibrateFeedback")));
            BuildUi();
            _runner.ProposalReady += (_, m) => { _review = _runner.Review; _measurements = m; };
            _runner.Completed += OnCompleted;
        }

        void Start()
        {
            if (!_runner.IsCalibrating && !SummaryVisible) _runner.StartCalibration();
        }

        void OnEnable()
        {
            _left = Action("AccessCalibrate/Feedback/Left", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            _right = Action("AccessCalibrate/Feedback/Right", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Keyboard>/downArrow", "<Gamepad>/dpad/down", "<Keyboard>/tab");
            _submit = Action("AccessCalibrate/Feedback/Submit", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space");
            _left.performed += _ => { if (Accepting()) Select(_selection - 1); };
            _right.performed += _ => { if (Accepting()) Select(_selection + 1); };
            _submit.performed += _ => { if (Accepting()) Activate(); };
            _left.Enable(); _right.Enable(); _submit.Enable();
        }

        void OnDisable()
        {
            foreach (var a in new[] { _left, _right, _submit }) a?.Dispose();
            _left = _right = _submit = null;
        }

        static InputAction Action(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        /// <summary>
        /// Which screen is showing: "welcome", "intro:HoldTest", "test:ReactionTest", "signal",
        /// "suggestions", "summary". In WebGL it is also published as window.accessCalibrateState,
        /// so a browser test can follow the calibration.
        /// </summary>
        public string State
        {
            get
            {
                if (SummaryVisible) return "summary";
                if (_runner.IsWelcoming) return "welcome";
                if (_runner.IsReviewing) return "suggestions";
                var test = _runner.CurrentTest;
                if (test == null) return "idle";
                if (_runner.InIntro) return "intro:" + test.GetType().Name;
                if (test is ReactionTest r && r.StimulusVisible) return "signal";
                return "test:" + test.GetType().Name;
            }
        }

        void Update()
        {
            string state = State;
            if (state == _state) return;
            _state = state;
#if UNITY_WEBGL && !UNITY_EDITOR
            AC_SetState(state);
#endif
        }

        bool Accepting() => SummaryVisible && Time.realtimeSinceStartupAsDouble - _shownAt >= InputDelaySeconds;

        // ================================================================ flow

        void OnCompleted(AccessibilityProfile profile)
        {
            // Apply saves exactly the switched-on suggestions; "Not now" keeps the defaults.
            _applied = _review != null && _review.Items.Count > 0
                && profile.Equals(_review.BuildProfile(AccessCalibrate.DefaultProfile()));
            ShowSummary();
        }

        public void ShowSummary()
        {
            Fill();
            _status.text = "";
            LastCopySucceeded = null;
            _root.style.display = DisplayStyle.Flex;
            SummaryVisible = true;
            _shownAt = Time.realtimeSinceStartupAsDouble;
            Select(0);
        }

        /// <summary>"Redo": back to the Welcome screen with a fresh calibration.</summary>
        public void Redo()
        {
            _root.style.display = DisplayStyle.None;
            SummaryVisible = false;
            _review = null;
            _measurements = null;
            AccessCalibrate.ResetToDefaults();
            _runner.StartCalibration();
        }

        /// <summary>"Copy results": the plain-text block on the system clipboard.</summary>
        public void CopyResults()
        {
            LastResultsText = ResultsText.Build(_measurements, _review, _applied, PlatformName());
            LastCopySucceeded = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            AC_CopyToClipboard(LastResultsText, gameObject.name, nameof(OnCopyResult));
#else
            GUIUtility.systemCopyBuffer = LastResultsText;
            OnCopyResult("ok");
#endif
        }

        /// <summary>Called by the WebGL clipboard plugin (SendMessage) or directly elsewhere.</summary>
        public void OnCopyResult(string result)
        {
            LastCopySucceeded = result == "ok";
            _status.text = LastCopySucceeded.Value
                ? "Copied. Paste it into your feedback message."
                : "The browser blocked copying. Click \"Copy results\" or press Enter on the keyboard: some browsers only allow copying after a click or a key press.";
        }

        static string PlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WebGLPlayer: return "browser (WebGL)";
                case RuntimePlatform.WindowsPlayer: return "Windows";
                case RuntimePlatform.OSXPlayer: return "macOS";
                case RuntimePlatform.LinuxPlayer: return "Linux";
                default: return Application.isEditor ? "Unity editor" : Application.platform.ToString();
            }
        }

        // ================================================================ UI

        void BuildUi()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("AccessCalibrate/AccessCalibrateTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 1100;

            var go = new GameObject("Feedback Summary");
            go.transform.SetParent(transform, false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = settings;

            var top = _doc.rootVisualElement;
            var baseSheet = Resources.Load<StyleSheet>("AccessCalibrate/CalibrationViewStyle");
            if (baseSheet != null) top.styleSheets.Add(baseSheet);
            var sheet = Resources.Load<StyleSheet>("FeedbackSummary");
            if (sheet != null) top.styleSheets.Add(sheet);

            _root = new VisualElement { name = "fb-root" };
            _root.AddToClassList("ac-root");
            _root.style.display = DisplayStyle.None;
            top.Add(_root);

            _panel = new VisualElement();
            _panel.AddToClassList("ac-panel");
            _root.Add(_panel);

            var page = new VisualElement();
            page.AddToClassList("fb-page");
            _panel.Add(page);

            var header = new VisualElement();
            header.AddToClassList("fb-header");
            var titles = new VisualElement();
            titles.Add(Text("CONTROL SETUP · SUMMARY", "ac-eyebrow"));
            titles.Add(Text("Your results", "ac-h1", "ac-h1--confirm"));
            header.Add(titles);
            _appliedLabel = Text("", "fb-applied");
            header.Add(_appliedLabel);
            page.Add(header);

            var columns = new VisualElement();
            columns.AddToClassList("fb-columns");
            var left = new VisualElement();
            left.AddToClassList("fb-col");
            left.AddToClassList("fb-col--measured");
            left.Add(Text("WHAT WAS MEASURED", "fb-col-title"));
            _measured = new VisualElement { name = "fb-measured" };
            left.Add(_measured);
            var right = new VisualElement();
            right.AddToClassList("fb-col");
            right.Add(Text("SUGGESTIONS", "fb-col-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "fb-suggested" };
            scroll.AddToClassList("fb-scroll");
            scroll.AddToClassList("ac-confirm-list");   // the plugin's thin scrollbar
            _suggested = scroll;
            right.Add(scroll);
            columns.Add(left);
            columns.Add(right);
            page.Add(columns);

            var footer = new VisualElement();
            footer.AddToClassList("fb-footer");
            _copy = MakeButton("fb-copy", "Copy results", primary: true, CopyResults);
            _redo = MakeButton("fb-redo", "Redo", primary: false, Redo);
            footer.Add(_copy);
            footer.Add(_redo);
            _status = Text("", "fb-status");
            _status.name = "fb-status";
            footer.Add(_status);
            page.Add(footer);
        }

        static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        Button MakeButton(string name, string label, bool primary, System.Action onClick)
        {
            var b = new Button(onClick) { name = name, text = "" };
            b.AddToClassList("ac-btn");
            b.AddToClassList(primary ? "ac-btn--primary" : "ac-btn--secondary");
            b.AddToClassList("ac-btn--apply");
            var l = new Label(label) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("ac-btn-label");
            b.Add(l);
            b.RegisterCallback<PointerEnterEvent>(_ => Select(b == _copy ? 0 : 1));
            return b;
        }

        void Select(int index)
        {
            _selection = ((index % 2) + 2) % 2;
            _copy.EnableInClassList("ac-selected", _selection == 0);
            _redo.EnableInClassList("ac-selected", _selection == 1);
        }

        void Activate()
        {
            if (_selection == 0) CopyResults();
            else Redo();
        }

        void Fill()
        {
            var m = _measurements ?? new Measurements();
            _measured.Clear();
            foreach (var line in ResultsText.Measurements(m, buttonNames: false))
                _measured.Add(Row(line.Name, line.Value, line.Device, null));

            _suggested.Clear();
            int count = _review?.Items.Count ?? 0;
            if (count == 0)
            {
                _suggested.Add(Text(_measurements == null
                    ? "The calibration was closed before the checks."
                    : "Nothing to suggest: the defaults fit these measurements.", "fb-empty"));
            }
            else
            {
                foreach (var item in _review.Items)
                    _suggested.Add(Row(ProposalTexts.Title(item.Change), ProposalTexts.Reason(item.Change), null, item.Accepted));
            }

            int on = 0;
            if (_review != null) foreach (var i in _review.Items) if (i.Accepted) on++;
            _appliedLabel.text = count == 0 ? ""
                : _applied ? $"Applied {on} of {count} suggestions"
                : "Nothing applied (Not now)";
        }

        static VisualElement Row(string name, string value, string device, bool? on)
        {
            var row = new VisualElement();
            row.AddToClassList("fb-row");
            var text = new VisualElement();
            text.AddToClassList("fb-row-text");
            text.Add(Text(name, "fb-row-name"));
            text.Add(Text(value, "fb-row-value"));
            row.Add(text);
            if (!string.IsNullOrEmpty(device)) row.Add(Text(device, "fb-row-device"));
            if (on.HasValue) row.Add(Text(on.Value ? "On" : "Off", "fb-state", on.Value ? "fb-state--on" : "fb-state--off"));
            return row;
        }
    }
}

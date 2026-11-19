// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.Demo
{
    /// <summary>
    /// The demo's on-screen layer (UI Toolkit, built in code): a small "active settings" panel,
    /// a "Redo calibration" button, and a prompt with a progress bar for the current challenge.
    /// It sits below the calibration screen (lower sorting order).
    /// </summary>
    public sealed class DemoHud : MonoBehaviour
    {
        public event Action RedoClicked;
        /// <summary>Study mode switch and label (bottom-right panel).</summary>
        public event Action<bool> StudyToggled;
        public event Action<string> StudyLabelChanged;
        /// <summary>True while the label field has keyboard focus: game controls must ignore keys.</summary>
        public bool IsTyping { get; private set; }

        public PanelSettings Panel { get; private set; }
        /// <summary>The prompt currently shown (null when hidden).</summary>
        public string PromptText { get; private set; }
        public string HintText => _hint?.text;
        public VisualElement Root { get; private set; }

        Label _settings, _prompt, _hint, _toast, _controls, _studyPath;
        Toggle _studyToggle;
        TextField _studyLabel;
        VisualElement _bar, _barFill, _promptBox;
        float _toastUntil;

        static readonly Color Ink = Color.white;
        static readonly Color Dim = new Color(1, 1, 1, 0.75f);
        static readonly Color Accent = new Color(1f, 0.83f, 0f);
        static readonly Color Glass = new Color(0.05f, 0.05f, 0.07f, 0.72f);

        public static DemoHud Create(Transform parent)
        {
            var go = new GameObject("Demo HUD");
            go.SetActive(false);
            go.transform.SetParent(parent, false);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("AccessCalibrate/AccessCalibrateTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            panel.sortingOrder = 0;   // calibration screen uses 1000, so it always covers the HUD

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var hud = go.AddComponent<DemoHud>();
            go.SetActive(true);
            hud.Panel = panel;
            hud.Build(doc.rootVisualElement);
            return hud;
        }

        void Build(VisualElement root)
        {
            Root = root;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

            // Top-left: active settings (discreet).
            var settingsBox = Box();
            settingsBox.style.position = Position.Absolute;
            settingsBox.style.left = 24; settingsBox.style.top = 24;
            settingsBox.Add(Text("ACTIVE SETTINGS", 18, Accent, bold: true));
            _settings = Text("", 20, Dim);
            settingsBox.Add(_settings);
            root.Add(settingsBox);

            // Top-right: redo calibration.
            var redo = new Button(() => RedoClicked?.Invoke()) { text = "Redo calibration (Start / F1)" };
            redo.style.position = Position.Absolute;
            redo.style.right = 24; redo.style.top = 24;
            redo.style.fontSize = 22;
            redo.style.color = Ink;
            redo.style.backgroundColor = Glass;
            SetBorder(redo, 2, Ink, 8);
            redo.style.paddingLeft = 16; redo.style.paddingRight = 16;
            redo.style.paddingTop = 8; redo.style.paddingBottom = 8;
            root.Add(redo);

            // Bottom-centre: prompt + progress bar.
            _promptBox = Box();
            _promptBox.style.position = Position.Absolute;
            _promptBox.style.bottom = 70;
            _promptBox.style.alignSelf = Align.Center;
            _promptBox.style.left = new Length(50, LengthUnit.Percent);
            _promptBox.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _promptBox.style.minWidth = 640;
            _promptBox.style.alignItems = Align.Center;
            _prompt = Text("", 40, Ink, bold: true);
            _prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            _promptBox.Add(_prompt);
            _bar = new VisualElement();
            _bar.style.width = 560; _bar.style.height = 18; _bar.style.marginTop = 10;
            _bar.style.backgroundColor = new Color(1, 1, 1, 0.15f);
            SetBorder(_bar, 0, Ink, 9);
            _barFill = new VisualElement();
            _barFill.style.height = Length.Percent(100);
            _barFill.style.backgroundColor = Accent;
            SetBorder(_barFill, 0, Accent, 9);
            _bar.Add(_barFill);
            _promptBox.Add(_bar);
            _hint = Text("", 24, Dim);
            _hint.style.marginTop = 8;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _promptBox.Add(_hint);
            root.Add(_promptBox);

            // Top-centre: short messages ("Missed it — let's try again").
            _toast = Text("", 30, Ink, bold: true);
            _toast.style.position = Position.Absolute;
            _toast.style.top = 110;
            _toast.style.left = new Length(50, LengthUnit.Percent);
            _toast.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _toast.style.backgroundColor = Glass;
            _toast.style.paddingLeft = 20; _toast.style.paddingRight = 20;
            _toast.style.paddingTop = 8; _toast.style.paddingBottom = 8;
            SetBorder(_toast, 0, Ink, 8);
            root.Add(_toast);

            // Bottom-left: controls.
            _controls = Text("Move: left stick / WASD   ·   Jump: A / Space", 18, Dim);
            _controls.style.position = Position.Absolute;
            _controls.style.left = 24; _controls.style.bottom = 20;
            root.Add(_controls);

            // Bottom-right: study mode (for comparing sessions; data stays on this device).
            var study = Box();
            study.style.position = Position.Absolute;
            study.style.right = 24; study.style.bottom = 20;
            study.style.width = 380;
            _studyToggle = new Toggle { text = "Study mode (saves a local CSV)" };
            _studyToggle.style.color = Ink;
            _studyToggle.Q<Label>(className: "unity-toggle__text").style.fontSize = 20;
            _studyToggle.Q<Label>(className: "unity-toggle__text").style.color = Ink;
            _studyToggle.RegisterValueChangedCallback(e =>
            {
                _studyLabel.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                _studyPath.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                StudyToggled?.Invoke(e.newValue);
            });
            study.Add(_studyToggle);
            _studyLabel = new TextField("Label") { value = "" };
            _studyLabel.style.fontSize = 20;
            _studyLabel.labelElement.style.color = Dim;
            _studyLabel.labelElement.style.minWidth = 60;
            _studyLabel.textEdition.placeholder = "e.g. normal, gloves";
            _studyLabel.RegisterValueChangedCallback(e => StudyLabelChanged?.Invoke(e.newValue));
            _studyLabel.RegisterCallback<FocusInEvent>(_ => IsTyping = true);
            _studyLabel.RegisterCallback<FocusOutEvent>(_ => IsTyping = false);
            _studyLabel.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
                    _studyLabel.Blur();
            }, TrickleDown.TrickleDown);
            study.Add(_studyLabel);
            _studyPath = Text("", 15, Dim);
            study.Add(_studyPath);
            _studyLabel.style.display = DisplayStyle.None;
            _studyPath.style.display = DisplayStyle.None;
            root.Add(study);

            SetPrompt(null);
            _toast.style.display = DisplayStyle.None;
        }

        /// <summary>Shows where the study CSV is written.</summary>
        public void ShowStudyPath(string path) => _studyPath.text = "Saved locally to:\n" + path;

        /// <summary>For tests: switch study mode and set the label as if typed.</summary>
        public void SetStudy(bool on, string label)
        {
            _studyLabel.value = label;
            _studyToggle.value = on;
        }

        static VisualElement Box()
        {
            var b = new VisualElement();
            b.style.backgroundColor = Glass;
            b.style.paddingLeft = 16; b.style.paddingRight = 16;
            b.style.paddingTop = 12; b.style.paddingBottom = 12;
            SetBorder(b, 0, Ink, 10);
            b.pickingMode = PickingMode.Ignore;
            return b;
        }

        static Label Text(string text, int size, Color color, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
                e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        public void ShowSettings(AccessibilityProfile p) =>
            _settings.text = string.Join("\n", DemoMechanics.DescribeActiveSettings(p));

        /// <summary>Shows a prompt; <paramref name="progress"/> 0…1 shows the bar, negative hides it. Null hides everything.</summary>
        public void SetPrompt(string text, float progress = -1f, string hint = null)
        {
            bool visible = !string.IsNullOrEmpty(text);
            PromptText = visible ? text : null;
            _promptBox.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            _prompt.text = text;
            _bar.style.display = progress >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _barFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            _hint.text = hint ?? "";
            _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Toast(string text, float seconds = 2.5f)
        {
            _toast.text = text;
            _toast.style.display = DisplayStyle.Flex;
            _toastUntil = Time.unscaledTime + seconds;
        }

        public void ClearToast() => _toast.style.display = DisplayStyle.None;

        public void SetVisible(bool visible) => Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        void Update()
        {
            if (_toast != null && _toast.style.display == DisplayStyle.Flex && Time.unscaledTime > _toastUntil)
                _toast.style.display = DisplayStyle.None;
        }
    }
}

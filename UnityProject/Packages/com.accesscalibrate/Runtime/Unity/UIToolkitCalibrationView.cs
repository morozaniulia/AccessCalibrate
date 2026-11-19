// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration
{
    /// <summary>
    /// Placeholder screen built with UI Toolkit (UXML + USS in Resources/AccessCalibrate).
    /// Simple shapes, big text, high contrast. Replace it with your own <see cref="ICalibrationView"/>
    /// for the final art — the test logic doesn't change.
    /// </summary>
    public sealed class UIToolkitCalibrationView : MonoBehaviour, ICalibrationView
    {
        const string ResourceFolder = "AccessCalibrate/";

        /// <summary>Raised when the on-screen "Skip" button is clicked.</summary>
        public event Action SkipClicked;

        UIDocument _doc;
        AudioSource _audio;
        AudioClip _cue;
        VisualElement _root, _stage, _stimulus, _arena, _target, _cursor, _summary;
        Label _counter, _title, _instructions, _status, _prompt, _hint;
        Button _skip;

        /// <summary>The panel this view draws into (e.g. to render it into a texture).</summary>
        public PanelSettings Panel => _doc != null ? _doc.panelSettings : null;
        internal VisualElement Root => _root;

        public static UIToolkitCalibrationView Create(CalibrationRunner runner)
        {
            var go = new GameObject("AccessCalibrate View");
            go.SetActive(false);
            go.transform.SetParent(runner.transform, false);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ResourceFolder + "AccessCalibrateTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            panel.sortingOrder = 1000;

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = Resources.Load<VisualTreeAsset>(ResourceFolder + "CalibrationView");

            var view = go.AddComponent<UIToolkitCalibrationView>();
            view._doc = doc;
            view._audio = go.AddComponent<AudioSource>();
            view._audio.playOnAwake = false;
            go.SetActive(true);

            view.Bind(runner.ReduceMotion);
            view.SkipClicked += runner.SkipCurrent;
            return view;
        }

        void Bind(bool reduceMotion)
        {
            _root = _doc.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>(ResourceFolder + "CalibrationViewStyle");
            if (sheet != null) _root.styleSheets.Add(sheet);
            _root.EnableInClassList("ac-reduce-motion", reduceMotion);

            _counter = _root.Q<Label>("ac-counter");
            _title = _root.Q<Label>("ac-title");
            _instructions = _root.Q<Label>("ac-instructions");
            _status = _root.Q<Label>("ac-status");
            _prompt = _root.Q<Label>("ac-prompt");
            _hint = _root.Q<Label>("ac-hint");
            _stage = _root.Q("ac-stage");
            _stimulus = _root.Q("ac-stimulus");
            _arena = _root.Q("ac-arena");
            _target = _root.Q("ac-target");
            _cursor = _root.Q("ac-cursor");
            _summary = _root.Q("ac-summary");
            _skip = _root.Q<Button>("ac-skip");

            _skip.text = CalibrationTexts.SkipButton;
            _skip.clicked += () => SkipClicked?.Invoke();
            _hint.text = CalibrationTexts.SkipHint;
            _cue = MakeCue();
            ResetStage();
        }

        void ResetStage()
        {
            ShowStimulus(false);
            ShowStabilityArena(false);
            SetPrompt("");
            SetStatus("");
            _summary.style.display = DisplayStyle.None;
            _summary.Clear();
            _stage.style.display = DisplayStyle.Flex;
            _skip.style.display = DisplayStyle.Flex;
            _hint.style.display = DisplayStyle.Flex;
        }

        public void ShowIntro(int index, int count, string title, string instructions)
        {
            _root.style.display = DisplayStyle.Flex;
            ResetStage();
            _counter.text = string.Format(CalibrationTexts.TestCounter, index, count);
            _title.text = title;
            _instructions.text = instructions;
            SetStatus(CalibrationTexts.IntroPressToStart);
        }

        public void ShowTest(string title, string instructions)
        {
            ResetStage();
            _title.text = title;
            _instructions.text = instructions;
        }

        public void SetStatus(string text) => _status.text = text;

        public void ShowStimulus(bool visible) => _stimulus.EnableInClassList("ac-stimulus--on", visible);

        public void PlayCue()
        {
            if (_audio != null && _cue != null) _audio.PlayOneShot(_cue);
        }

        public void SetPrompt(string text)
        {
            _prompt.text = text;
            _prompt.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ShowStabilityArena(bool visible) =>
            _arena.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetCursor(float xInRadii, float yInRadii)
        {
            // The target circle is 200 px wide (radius 100) in the middle of a 400 px arena.
            const float radiusPx = 100f, centrePx = 200f, cursorHalf = 14f;
            float x = Mathf.Clamp(centrePx + xInRadii * radiusPx, 0, 2 * centrePx);
            float y = Mathf.Clamp(centrePx - yInRadii * radiusPx, 0, 2 * centrePx); // screen Y goes down
            _cursor.style.left = x - cursorHalf;
            _cursor.style.top = y - cursorHalf;
            _target.EnableInClassList("ac-target--inside", xInRadii * xInRadii + yInRadii * yInRadii <= 1f);
        }

        public void ShowSummary(IReadOnlyList<ProposedChange> changes)
        {
            ResetStage();
            _stage.style.display = DisplayStyle.None;
            _skip.style.display = DisplayStyle.None;
            _hint.style.display = DisplayStyle.None;
            _counter.text = "";
            _title.text = CalibrationTexts.SummaryTitle;
            _instructions.text = "";
            _summary.style.display = DisplayStyle.Flex;
            if (changes.Count == 0)
                _summary.Add(new Label(CalibrationTexts.SummaryNothing) { name = "ac-summary-empty" });
            foreach (var c in changes)
            {
                var row = new Label("• " + c.Summary);
                row.AddToClassList("ac-summary-row");
                _summary.Add(row);
            }
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

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

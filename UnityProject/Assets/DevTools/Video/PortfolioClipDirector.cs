// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development tool for THIS project only: plays short scenes of the calibration by itself (a
// virtual Xbox-style gamepad) and saves their frames, for the portfolio's looping clips.
// Editor-only; never part of the package or a build. Started by PortfolioClipRecorder.
// Game time is fixed at 60 fps (frame n is at n/60 s); tools/make_portfolio_clips.py turns the
// frames into seamless MP4 loops.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AccessCalibration;
using AccessCalibration.Core;
using AccessCalibration.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

public sealed class PortfolioClipDirector : MonoBehaviour
{
    public static bool Armed;
    public static bool Done;
    /// <summary>One folder per clip is made in here (JPEG frames, numbered from 0).</summary>
    public static string FramesRoot;

    static RenderTexture s_Target;
    Texture2D _frame;
    string _clipDir;
    int _clipFrame;
    double _clipStart;

    CalibrationRunner _runner;
    Keyboard _keyboard;
    Gamepad _pad;
    InputSettings.EditorInputBehaviorInPlayMode _oldEditorBehavior;
    InputSettings.BackgroundBehavior _oldBackground;
    readonly List<InputDevice> _disabled = new List<InputDevice>();
    readonly System.Random _rng = new System.Random(20261120);

    static double T => Time.timeAsDouble;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeScene()
    {
        if (!Armed) return;
        Done = false;
        string dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateClips", Guid.NewGuid().ToString("N"));
        AccessCalibrate.UseStore(new ProfileStore(dir));
        StudyMode.Enabled = false;
        Time.captureFramerate = 60;
        if (s_Target == null)
        {
            s_Target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "Portfolio Clip Frame" };
            s_Target.Create();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterScene()
    {
        if (!Armed) return;
        new GameObject("Portfolio Clip Director").AddComponent<PortfolioClipDirector>();
    }

    void Awake()
    {
        var s = InputSystem.settings;
        _oldEditorBehavior = s.editorInputBehaviorInPlayMode;
        _oldBackground = s.backgroundBehavior;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground = true;

        _keyboard = InputSystem.AddDevice<Keyboard>("ClipKeyboard");
        _pad = InputSystem.AddDevice<Gamepad>("ClipGamepad");   // a generic gamepad: Xbox glyphs
        foreach (var d in InputSystem.devices.ToArray())
        {
            if (d == _keyboard || d == _pad || !d.enabled) continue;
            if (d is Keyboard || d is Pointer || d is Gamepad || d is Joystick) { InputSystem.DisableDevice(d); _disabled.Add(d); }
        }

        AccessCalibrate.UseTheme(ThemePreset.Dark);
        var go = new GameObject("AccessCalibrate");
        go.SetActive(false);
        _runner = go.AddComponent<CalibrationRunner>();
        _runner.Clock = () => Time.timeAsDouble;   // the calibration and its motion on game time
        go.SetActive(true);
    }

    void LateUpdate()
    {
        foreach (var cam in Camera.allCameras)
            if (cam.targetTexture != s_Target) cam.targetTexture = s_Target;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            var ps = doc.panelSettings;
            if (ps != null && ps.targetTexture != s_Target) { ps.targetTexture = s_Target; ps.clearColor = false; }
        }
    }

    /// <summary>Saves the frame rendered in the previous frame (batch mode never reaches end-of-frame).</summary>
    void Update()
    {
        if (_clipDir != null)
        {
            if (_frame == null) _frame = new Texture2D(s_Target.width, s_Target.height, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = s_Target;
            _frame.ReadPixels(new Rect(0, 0, s_Target.width, s_Target.height), 0, 0);
            _frame.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_clipDir, _clipFrame.ToString("D5") + ".jpg"), _frame.EncodeToJPG(97));
            _clipFrame++;
        }
        if (Gamepad.current != _pad) _pad.MakeCurrent();
        if (Time.frameCount % 120 == 0) Debug.Log($"[Clips] frame {Time.frameCount} t={T:0.00} real={Time.realtimeSinceStartup:0.0}");
    }

    void Start() => StartCoroutine(Scenario());

    void OnDestroy()
    {
        var s = InputSystem.settings;
        s.editorInputBehaviorInPlayMode = _oldEditorBehavior;
        s.backgroundBehavior = _oldBackground;
        foreach (var d in _disabled) if (d.added) InputSystem.EnableDevice(d);
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
    }

    // ================================================================ clips

    void BeginClip(string name)
    {
        _clipDir = Path.Combine(FramesRoot, name);
        if (Directory.Exists(_clipDir)) Directory.Delete(_clipDir, true);
        Directory.CreateDirectory(_clipDir);
        _clipFrame = 0;
        _clipStart = T;
        Debug.Log($"[Clips] begin {name} at t={T:0.00}");
    }

    IEnumerator EndClip()
    {
        yield return null;   // the last frame drawn is saved on the next Update
        Debug.Log($"[Clips] end {Path.GetFileName(_clipDir)}: {_clipFrame} frames");
        _clipDir = null;
    }

    double ClipTime => T - _clipStart;

    IEnumerator Scenario()
    {
        yield return null;
        var m = DesignMeasurements();

        // a. Welcome and device detection: it fogs in with keyboard glyphs; a press on the
        //    controller switches every glyph and the device line to the Xbox controller.
        _runner.StartCalibration();
        yield return null;
        var view = (UIToolkitCalibrationView)_runner.View;
        Debug.Log($"[Clips] family at start: {view.GlyphFamily}");
        // Input isn't always processed in the first moments of Play mode: press until it counts.
        for (int i = 0; i < 40 && view.GlyphFamily != GlyphFamily.Keyboard; i++)
        {
            yield return Wait(0.25f);
            yield return Tap(_keyboard.tabKey);
        }
        yield return Wait(0.2f);
        Debug.Log($"[Clips] family after a key press: {view.GlyphFamily}");
        if (view.GlyphFamily != GlyphFamily.Keyboard) throw new InvalidOperationException("the keyboard press never arrived");
        view.Hide();
        view.ShowWelcome();   // from the start of its motion
        BeginClip("a-welcome");
        yield return Wait(5.6f);
        yield return Tap(_pad.leftShoulder);
        yield return null;
        Debug.Log($"[Clips] family after the controller: {view.GlyphFamily}");
        if (view.GlyphFamily != GlyphFamily.Xbox) throw new InvalidOperationException("the controller press never arrived");
        yield return Wait(3.0f);
        yield return EndClip();

        // b. One check with its intro: Reaction, the signal, a press.
        _runner.Reaction.Random = new System.Random(3);
        BeginClip("b-reaction");
        _runner.WelcomeStart();
        yield return Until(() => _runner.Reaction.StimulusVisible, 15);
        yield return Wait(0.36f);
        yield return Tap(_pad.buttonSouth, 0.1f);
        yield return Wait(1.3f);
        while (ClipTime < 7.4) yield return null;
        yield return EndClip();

        // c. Steadiness active: the left stick brings the dot into the circle and holds it there.
        _runner.SkipCurrent(); _runner.SkipCurrent(); _runner.SkipCurrent();
        var st = _runner.Stability;
        yield return Until(() => st.IsRunning && !st.IsInIntro, 10);
        yield return Wait(1.4f);   // the options settle
        BeginClip("c-steadiness");
        yield return Wait(0.8f);
        var target = st.StickTarget;
        double t0 = T;
        while (T - t0 < 1.0)
        {
            float k = Mathf.SmoothStep(0, 1, (float)((T - t0) / 1.0));
            Send(_pad.leftStick, Vector2.Lerp(Vector2.zero, target + new Vector2(0.03f, 0.02f), k));
            yield return null;
        }
        t0 = T;
        while (T - t0 < 5.0)
        {
            float t = (float)(T - t0);
            var wobble = new Vector2(Mathf.Sin(t * 2.1f) * 0.025f + Mathf.Sin(t * 5.3f) * 0.008f, Mathf.Cos(t * 1.7f) * 0.02f + Mathf.Sin(t * 4.1f) * 0.007f);
            var drift = new Vector2(0.03f, 0.02f) * Mathf.Exp(-t * 1.5f);
            Send(_pad.leftStick, target + drift + wobble + new Vector2((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f) * 0.004f);
            yield return null;
        }
        yield return EndClip();
        Send(_pad.leftStick, Vector2.zero);

        // d. Suggestions with a reason: move to two suggestions (their detail: why, in the game),
        //    then back to the first, so the clip ends where it began.
        _runner.SkipAll();
        yield return Wait(0.3f);
        _runner.ShowProposal(m);
        yield return Wait(4.2f);
        BeginClip("d-suggestions");
        yield return Wait(1.2f);
        yield return Tap(_pad.dpad.down);
        yield return Wait(2.3f);
        yield return Tap(_pad.dpad.down);
        yield return Wait(2.3f);
        yield return Tap(_pad.dpad.up);
        yield return Wait(0.45f);
        yield return Tap(_pad.dpad.up);
        yield return Wait(1.0f);
        yield return EndClip();

        // e. Summary, then Light and High Contrast (three holds; the script dissolves between them).
        _runner.Accept();
        yield return Wait(3.4f);
        foreach (var (theme, name) in new[] { (ThemePreset.Dark, "e-summary-1-dark"), (ThemePreset.Light, "e-summary-2-light"), (ThemePreset.HighContrast, "e-summary-3-high-contrast") })
        {
            AccessCalibrate.UseTheme(theme);
            yield return null; yield return null;
            BeginClip(name);
            yield return Wait(2.7f);
            yield return EndClip();
        }
        AccessCalibrate.UseTheme(ThemePreset.Dark);

        Done = true;
    }

    /// <summary>The design's example measurements (Docs/design-v2 screens 12 and 14).</summary>
    static Measurements DesignMeasurements() => new Measurements
    {
        Reaction = new ReactionMeasurement { MedianMs = 650, Confidence = Confidence.Ok, RawTrialsMs = { 700, 640, 650, 660, 610, 655 }, ValidTrialCount = 5, Device = InputDeviceKind.Gamepad },
        Hold = new HoldMeasurement { MaxHoldSeconds = 1.4, Confidence = Confidence.Ok, RawHoldsSeconds = { 1.4, 1.2 }, Device = InputDeviceKind.Gamepad },
        Taps = new TapMeasurement { PressCount = 4, DurationSeconds = 5, Confidence = Confidence.Ok, Device = InputDeviceKind.Gamepad },
        Stability = new StabilityMeasurement { Spread = 0.26, Confidence = Confidence.Ok, Device = InputDeviceKind.Gamepad },
        Buttons = new ButtonAccessMeasurement { RequestedButtons = { "A", "B", "X", "Y", "RB", "LB" }, UnpressedButtons = { "RB" }, Confidence = Confidence.Ok, Device = InputDeviceKind.Gamepad },
    };

    // ================================================================ input helpers

    static void Send<TValue>(InputControl<TValue> control, TValue value) where TValue : struct
    {
        using (DeltaStateEvent.From(control, out var eventPtr))
        {
            // game time, but never older than the device's last update (older events are dropped)
            eventPtr.time = Math.Max(T, control.device.lastUpdateTime + 0.0001);
            control.WriteValueIntoEvent(value, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
    }

    static IEnumerator Tap(ButtonControl key, float hold = 0.12f)
    {
        Send(key, 1f);
        yield return new WaitForSeconds(hold);
        Send(key, 0f);
        yield return null;
    }

    static IEnumerator Until(Func<bool> condition, float timeout)
    {
        double end = T + timeout;
        while (!condition() && T < end) yield return null;
        if (!condition()) Debug.LogWarning("[Clips] timed out waiting");
    }

    static IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }
}
#endif

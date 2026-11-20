// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development tool for THIS project only: plays the demo by itself (simulated input) so the
// presentation video can be recorded. Editor-only; never part of the package or a build.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AccessCalibration;
using AccessCalibration.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

public sealed class DemoVideoDirector : MonoBehaviour
{
    /// <summary>Set by the editor recorder before entering Play mode; nothing happens otherwise.</summary>
    public static bool Armed;
    public static bool Done;
    public static int RecordingStartFrame = -1;
    public static string MarkersPath;

    /// <summary>
    /// What the Game view would show — the demo camera plus every UI Toolkit panel (HUD and
    /// calibration screens) — drawn into one 1920×1080 texture. The recorder captures this, so it
    /// works without an editor window (batch mode) and never includes editor UI.
    /// </summary>
    public static RenderTexture Target;
    /// <summary>Preview: save three PNG frames (Welcome, Reaction, Suggested settings), then stop.</summary>
    public static bool PreviewOnly;
    public static string PreviewDir;
    /// <summary>Full recording: save every frame of <see cref="Target"/> as PNG (ffmpeg turns them into the video).</summary>
    public static bool CaptureFrames;
    public static string FramesDir;
    Texture2D _frame;

    static readonly List<(string name, int frame)> s_Markers = new List<(string, int)>();

    DemoGame _game;
    CalibrationRunner _runner;
    Keyboard _keyboard;
    Mouse _mouse;
    InputSettings.EditorInputBehaviorInPlayMode _oldEditorBehavior;
    InputSettings.BackgroundBehavior _oldBackground;
    readonly System.Random _rng = new System.Random(20261119);
    readonly List<InputDevice> _disabled = new List<InputDevice>();
    bool _reactionSaved;

    static double T => Time.timeAsDouble;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeScene()
    {
        if (!Armed) return;
        s_Markers.Clear();
        Done = false;
        // A fresh, private profile store: the video always starts with the first-launch calibration.
        string dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateVideo", Guid.NewGuid().ToString("N"));
        AccessCalibrate.UseStore(new ProfileStore(dir));
        StudyMode.Enabled = false;

        // Fixed 60 fps game time: frame n is always at n/60 s, however fast the machine renders.
        Time.captureFramerate = 60;
        if (Target == null)
        {
            Target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "AccessCalibrate Video Frame" };
            Target.Create();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterScene()
    {
        if (!Armed) return;
        new GameObject("Demo Video Director").AddComponent<DemoVideoDirector>();
    }

    void Awake()
    {
        // Runs after every Awake in the scene and before any Start: switch the calibration to
        // game time, so the recording (fixed 60 fps) and the measurements share one clock.
        _game = FindAnyObjectByType<DemoGame>();
        _runner = _game.Runner;
        _runner.Clock = () => Time.timeAsDouble;

        var s = InputSystem.settings;
        _oldEditorBehavior = s.editorInputBehaviorInPlayMode;
        _oldBackground = s.backgroundBehavior;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground = true;

        _keyboard = InputSystem.AddDevice<Keyboard>("VideoKeyboard");
        _mouse = InputSystem.AddDevice<Mouse>("VideoMouse");
        _keyboard.MakeCurrent();
        _mouse.MakeCurrent();

        // Real devices can't interfere (an accidental touch, a resting hand on the mouse):
        // disable them inside Unity for the recording; re-enabled in OnDestroy.
        foreach (var d in InputSystem.devices.ToArray())
        {
            if (d == _keyboard || d == _mouse || !d.enabled) continue;
            if (d is Keyboard || d is Pointer || d is Gamepad || d is Joystick)
            {
                InputSystem.DisableDevice(d);
                _disabled.Add(d);
            }
        }
        Debug.Log($"[DemoVideo] disabled {_disabled.Count} physical input device(s) for the recording");
    }

    /// <summary>Send the camera and every UI panel into the video frame (new panels appear later, e.g. the calibration screens).</summary>
    void LateUpdate()
    {
        foreach (var cam in Camera.allCameras)
            if (cam.targetTexture != Target) cam.targetTexture = Target;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            var ps = doc.panelSettings;
            if (ps != null && ps.targetTexture != Target)
            {
                ps.targetTexture = Target;
                ps.clearColor = false;    // draw over the camera image, like the Game view
            }
        }
    }

    /// <summary>Saves the current video frame as PNG (read on a later frame: batch mode never reaches end-of-frame).</summary>
    IEnumerator Snapshot(string name)
    {
        yield return null;
        yield return null;
        var prev = RenderTexture.active;
        RenderTexture.active = Target;
        var tex = new Texture2D(Target.width, Target.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        Directory.CreateDirectory(PreviewDir);
        string path = Path.Combine(PreviewDir, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[DemoVideo] saved {path}");
    }

    /// <summary>
    /// Saves the frame rendered in the previous frame (read in Update, before this frame
    /// renders: batch mode never reaches end-of-frame). File name = that frame's number, so
    /// markers map straight to files.
    /// </summary>
    void CaptureFrame()
    {
        if (_frame == null) _frame = new Texture2D(Target.width, Target.height, TextureFormat.RGB24, false);
        var prev = RenderTexture.active;
        RenderTexture.active = Target;
        _frame.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
        _frame.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(FramesDir, (Time.frameCount - 1).ToString("D5") + ".png"), _frame.EncodeToPNG());
    }

    void Update()
    {
        if (CaptureFrames && Time.frameCount > 2) CaptureFrame();

        // Heartbeat: proves frames and game time advance (one line per second of game time).
        if (Time.frameCount % 60 == 0)
            Debug.Log($"[DemoVideo] heartbeat frame {Time.frameCount} t={T:0.00} real={Time.realtimeSinceStartup:0.0}");
        if (Mouse.current != _mouse) _mouse.MakeCurrent();
        if (Keyboard.current != _keyboard) _keyboard.MakeCurrent();
    }

    void Start() => StartCoroutine(Scenario());

    void OnDestroy()
    {
        var s = InputSystem.settings;
        s.editorInputBehaviorInPlayMode = _oldEditorBehavior;
        s.backgroundBehavior = _oldBackground;
        foreach (var d in _disabled) if (d.added) InputSystem.EnableDevice(d);
        _disabled.Clear();
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
    }

    // ================================================================ input helpers

    static void Send<TValue>(InputControl<TValue> control, TValue value) where TValue : struct
    {
        using (DeltaStateEvent.From(control, out var eventPtr))
        {
            eventPtr.time = T;   // same axis as the calibration's clock
            control.WriteValueIntoEvent(value, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
    }

    static void Down(ButtonControl key) => Send(key, 1f);
    static void Up(ButtonControl key) => Send(key, 0f);

    IEnumerator Tap(ButtonControl key, float hold = 0.12f)
    {
        Down(key);
        yield return new WaitForSeconds(hold);
        Up(key);
        yield return null;
    }

    void MoveMouse(Vector2 to) => Send(_mouse.position, to);

    void Mark(string name)
    {
        s_Markers.Add((name, Time.frameCount));
        Debug.Log($"[DemoVideo] {name} @ frame {Time.frameCount} t={T:0.00}");
    }

    IEnumerator Until(Func<bool> condition, float timeout = 30f)
    {
        double end = T + timeout;
        while (!condition() && T < end) yield return null;
        if (!condition()) Debug.LogWarning("[DemoVideo] timed out waiting");
    }

    IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

    /// <summary>The suggestions list of the built-in screen (found by name through public UI Toolkit API).</summary>
    static ScrollView SuggestionsList()
    {
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            var list = doc.rootVisualElement?.Q<ScrollView>("ac-confirm-list");
            if (list != null) return list;
        }
        return null;
    }

    /// <summary>Scrolls the suggestions list smoothly to the top (0) or the bottom (1).</summary>
    IEnumerator ScrollList(float to, float seconds)
    {
        var list = SuggestionsList();
        if (list == null) yield break;
        float max = Mathf.Max(0, list.contentContainer.layout.height - list.contentViewport.layout.height);
        float from = list.scrollOffset.y, target = to * max;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            list.scrollOffset = new Vector2(0, Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / seconds)));
            yield return null;
        }
        list.scrollOffset = new Vector2(0, target);
    }

    IEnumerator NextIntro(CalibrationTest test, float readSeconds = 1.2f)
    {
        yield return Until(() => _runner.CurrentTest == test);
        Mark("intro-" + test.GetType().Name);
        yield return Wait(readSeconds);
        yield return Tap(_keyboard.spaceKey);                    // "press any button when you're ready"
        yield return Until(() => test.IsRunning && !test.IsWaitingForRelease);
        Mark("test-" + test.GetType().Name);
    }

    // ================================================================ the scenario

    IEnumerator Scenario()
    {
        // Park the cursor in the upper-left area; the steadiness circle will be placed away from it.
        MoveMouse(new Vector2(Screen.width * 0.18f, Screen.height * 0.78f));
        yield return Until(() => _runner.IsWelcoming);
        Mark("welcome");
        if (PreviewOnly) { yield return Wait(1.5f); yield return Snapshot("frame-1-welcome"); yield return Wait(2.0f); }
        else yield return Wait(3.6f);
        yield return Tap(_keyboard.enterKey);                    // Start

        // 1. Reaction: ~650 ms after each signal (a "slow" profile).
        yield return NextIntro(_runner.Reaction);
        while (_runner.Reaction.IsRunning)
        {
            yield return Until(() => _runner.Reaction.StimulusVisible || !_runner.Reaction.IsRunning);
            if (!_runner.Reaction.IsRunning) break;
            double shown = _runner.Reaction.StimulusShownAt;
            Mark("signal");
            if (PreviewOnly && !_reactionSaved) { _reactionSaved = true; yield return Snapshot("frame-2-reaction"); }
            double reactAt = shown + 0.60 + _rng.NextDouble() * 0.10;
            yield return Until(() => T >= reactAt);
            yield return Tap(_keyboard.spaceKey);
        }

        // 2. Hold: short, comfortable holds (1.2 s and 1.4 s).
        yield return NextIntro(_runner.Hold);
        yield return Wait(0.5f);
        Down(_keyboard.spaceKey); Mark("hold-press");
        yield return Wait(1.2f);
        Up(_keyboard.spaceKey);
        yield return Wait(1.3f);
        Down(_keyboard.spaceKey);
        yield return Wait(1.4f);
        Up(_keyboard.spaceKey);
        yield return Until(() => _runner.Hold.IsFinished);

        // 3. Repeated presses: only a few, slowly.
        yield return NextIntro(_runner.Taps);
        yield return Wait(0.4f);
        foreach (var gap in new[] { 0f, 1.3f, 1.6f, 1.5f })
        {
            yield return Wait(gap);
            yield return Tap(_keyboard.spaceKey);
        }
        yield return Until(() => _runner.Taps.IsFinished);

        // 4. Steadiness with an unsteady mouse.
        yield return NextIntro(_runner.Stability);
        var stab = _runner.Stability;
        Vector2 from = _mouse.position.ReadValue(), target = stab.Target;
        for (float t = 0; t < 0.9f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 0.9f);
            MoveMouse(Vector2.Lerp(from, target, k));
            yield return null;
        }
        float r = stab.Radius;
        bool measuringMarked = false;
        while (stab.IsRunning)
        {
            float tt = (float)T;
            var wobble = new Vector2(Mathf.Sin(tt * 7.3f) + 0.6f * Mathf.Sin(tt * 13.1f + 1f),
                                     Mathf.Cos(tt * 6.1f) + 0.5f * Mathf.Sin(tt * 11.7f + 2f));
            MoveMouse(target + wobble * (0.22f * r) + new Vector2((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f) * 0.1f * r);
            if (!measuringMarked && stab.CurrentPhase == StabilityTest.Phase.Measuring) { Mark("steadiness-measuring"); measuringMarked = true; }
            yield return null;
        }

        // 5. Button reach: three of the four buttons; the right bumper (R) isn't reached.
        yield return NextIntro(_runner.ButtonAccess);
        yield return Wait(0.8f);
        yield return Tap(_keyboard.spaceKey);
        yield return Wait(1.0f);
        yield return Tap(_keyboard.eKey);
        yield return Wait(1.1f);
        yield return Tap(_keyboard.qKey);
        yield return Until(() => _runner.ButtonAccess.IsFinished);

        // Suggestions: read, switch one row, apply.
        yield return Until(() => _runner.IsReviewing);
        Mark("confirm");
        if (PreviewOnly)
        {
            yield return Wait(1.5f);
            yield return Snapshot("frame-3-suggested-settings");
            yield return ScrollList(1f, 2.5f);
            yield return Wait(0.3f);
            yield return Snapshot("frame-4-suggested-settings-bottom");
            WriteMarkers();
            Done = true;
            yield break;
        }
        yield return Wait(2.0f);
        Mark("scroll-down");
        yield return ScrollList(1f, 3.0f);                       // show all six suggestions
        yield return Wait(1.0f);
        int aim = -1;
        for (int i = 0; i < _runner.Review.Items.Count; i++)
            if (_runner.Review.Items[i].Change.Setting == "aimAssist") aim = i;
        if (aim < 0) aim = _runner.Review.Items.Count - 1;
        int steps = _runner.AcceptIndex - aim;
        for (int i = 0; i < steps; i++) { yield return Tap(_keyboard.upArrowKey); yield return Wait(0.45f); }
        yield return Wait(0.5f);
        Mark("toggle");
        yield return Tap(_keyboard.spaceKey);                    // switch aim assist off
        yield return Wait(1.6f);
        for (int i = 0; i < steps; i++) { yield return Tap(_keyboard.downArrowKey); yield return Wait(0.35f); }
        yield return Wait(0.5f);
        Mark("scroll-up");
        yield return ScrollList(0f, 2.5f);                       // back to the top
        yield return Wait(0.8f);
        Mark("applied");                                          // the video cuts here
        yield return Tap(_keyboard.enterKey);                    // Apply
        yield return Wait(0.5f);
        // No gameplay in the presentation video: stop right after Apply.

        WriteMarkers();
        Done = true;
    }

    void WriteMarkers()
    {
        var sb = new StringBuilder("{\n  \"fps\": 60,\n  \"recordingStartFrame\": " + RecordingStartFrame + ",\n  \"markers\": [\n");
        for (int i = 0; i < s_Markers.Count; i++)
        {
            var (name, frame) = s_Markers[i];
            sb.Append($"    {{ \"name\": \"{name}\", \"frame\": {frame} }}{(i < s_Markers.Count - 1 ? "," : "")}\n");
        }
        sb.Append("  ]\n}\n");
        if (!string.IsNullOrEmpty(MarkersPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MarkersPath));
            File.WriteAllText(MarkersPath, sb.ToString());
        }
    }
}
#endif

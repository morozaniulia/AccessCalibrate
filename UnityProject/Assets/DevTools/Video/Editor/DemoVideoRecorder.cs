// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development tool for THIS project only: captures the demo while DemoVideoDirector plays it.
// Runs without an editor window (no focus throttling):
//   Unity.exe -batchmode -projectPath <project> -executeMethod DemoVideoRecorder.Preview   (4 PNG stills)
//   Unity.exe -batchmode -projectPath <project> -executeMethod DemoVideoRecorder.Record    (every frame, PNG)
// The frame is the demo camera + every UI Toolkit panel drawn into one 1920×1080 texture
// (DemoVideoDirector.Target): what the Game view shows, without any editor UI. Game time is
// fixed at 60 fps, so frame n is always at n/60 s. ffmpeg turns the frames into the video.
//
// Why not Unity Recorder: it pauses time (timeScale = 0) until WaitForEndOfFrame, which batch
// mode never reaches, and its Game View input needs a focused editor window (an unfocused
// editor slows to ~1 fps).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DemoVideoRecorder
{
    const string Scene = "Assets/Samples/AccessCalibrate/0.1.0/Demo/AccessCalibrateDemo.unity";
    const double TimeoutSeconds = 1800;

    static double s_Started;
    static bool s_Quitting;

    static string RecordingsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Recordings"));

    [MenuItem("Tools/AccessCalibrate/Video/Preview Stills")]
    public static void Preview() => Run(preview: true);

    [MenuItem("Tools/AccessCalibrate/Video/Capture Frames")]
    public static void Record() => Run(preview: false);

    static void Run(bool preview)
    {
        string raw = Path.Combine(RecordingsDir, "raw");
        EditorSceneManager.OpenScene(Scene);
        DemoVideoDirector.Armed = true;
        DemoVideoDirector.Done = false;
        DemoVideoDirector.PreviewOnly = preview;
        DemoVideoDirector.PreviewDir = Path.Combine(RecordingsDir, "preview");
        DemoVideoDirector.CaptureFrames = !preview;
        DemoVideoDirector.FramesDir = Path.Combine(raw, "frames");
        if (!preview)
        {
            if (Directory.Exists(DemoVideoDirector.FramesDir)) Directory.Delete(DemoVideoDirector.FramesDir, true);
            Directory.CreateDirectory(DemoVideoDirector.FramesDir);
        }
        DemoVideoDirector.MarkersPath = Path.Combine(preview ? DemoVideoDirector.PreviewDir : raw, "markers.json");
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            DemoVideoDirector.RecordingStartFrame = Time.frameCount;
            s_Started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Poll;
        }
        else if (state == PlayModeStateChange.EnteredEditMode && s_Quitting)
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            DemoVideoDirector.Armed = false;
            DemoVideoDirector.CaptureFrames = false;
            Debug.Log("[DemoVideo] done, quitting");
            EditorApplication.Exit(DemoVideoDirector.Done ? 0 : 1);
        }
    }

    static void Poll()
    {
        bool timedOut = EditorApplication.timeSinceStartup - s_Started > TimeoutSeconds;
        if (!DemoVideoDirector.Done && !timedOut) return;
        EditorApplication.update -= Poll;
        if (timedOut) Debug.LogError("[DemoVideo] timed out");
        s_Quitting = true;
        EditorApplication.ExitPlaymode();
    }
}

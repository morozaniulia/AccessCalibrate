// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development tool for THIS project only: records the portfolio clips' frames in an empty scene.
//   Unity.exe -batchmode -projectPath <project> -executeMethod PortfolioClipRecorder.Record
// Frames go to Recordings/raw-clips/<clip>/; tools/make_portfolio_clips.py makes the MP4 loops.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PortfolioClipRecorder
{
    const double TimeoutSeconds = 1800;
    static double s_Started;
    static bool s_Quitting;

    [MenuItem("Tools/AccessCalibrate/Video/Capture Portfolio Clips")]
    public static void Record()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Recordings", "raw-clips"));
        Directory.CreateDirectory(root);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        PortfolioClipDirector.Armed = true;
        PortfolioClipDirector.Done = false;
        PortfolioClipDirector.FramesRoot = root;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            s_Started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Poll;
        }
        else if (state == PlayModeStateChange.EnteredEditMode && s_Quitting)
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            PortfolioClipDirector.Armed = false;
            Debug.Log("[Clips] done, quitting");
            EditorApplication.Exit(PortfolioClipDirector.Done ? 0 : 1);
        }
    }

    static void Poll()
    {
        bool timedOut = EditorApplication.timeSinceStartup - s_Started > TimeoutSeconds;
        if (!PortfolioClipDirector.Done && !timedOut) return;
        EditorApplication.update -= Poll;
        if (timedOut) Debug.LogError("[Clips] timed out");
        s_Quitting = true;
        EditorApplication.ExitPlaymode();
    }
}

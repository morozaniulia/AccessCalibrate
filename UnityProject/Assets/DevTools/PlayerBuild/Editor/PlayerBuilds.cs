// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development tool: builds the playable feedback version (calibration + summary only).
//   Unity.exe -batchmode -projectPath <project> -buildTarget WebGL -executeMethod PlayerBuilds.WebGL     → Builds/WebGL
//   Unity.exe -batchmode -projectPath <project> -buildTarget Win64 -executeMethod PlayerBuilds.Windows   → Builds/Windows
// Only Assets/FeedbackApp/Feedback.unity goes into the build. The demo level's Resources folder is
// moved aside during the build (Unity would include it otherwise) and put back afterwards, and the
// player settings changed for the build are restored.
using System;
using System.IO;
using AccessCalibration;
using AccessCalibration.FeedbackApp;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerBuilds
{
    public const string Scene = "Assets/FeedbackApp/Feedback.unity";
    const string DemoResources = "Assets/Samples/AccessCalibrate/0.1.0/Demo/Resources";
    const string DemoResourcesParked = "Assets/Samples/AccessCalibrate/0.1.0/Demo/ResourcesParkedDuringBuild";
    const string Template = "PROJECT:AccessCalibrate";

    static string BuildsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds"));

    [MenuItem("Tools/AccessCalibrate/Feedback Build/Create Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var camera = cam.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.20f, 0.25f, 0.30f);

        var app = new GameObject("AccessCalibrate Feedback");
        app.AddComponent<CalibrationRunner>();
        app.AddComponent<FeedbackApp>();

        EditorSceneManager.SaveScene(scene, Scene);
        Debug.Log("[Build] scene saved: " + Scene);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    [MenuItem("Tools/AccessCalibrate/Feedback Build/WebGL")]
    public static void WebGL() => Build(BuildTarget.WebGL, Path.Combine(BuildsDir, "WebGL"));

    [MenuItem("Tools/AccessCalibrate/Feedback Build/Windows")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, Path.Combine(BuildsDir, "Windows", "AccessCalibrate.exe"));

    static void Build(BuildTarget target, string location)
    {
        var saved = new
        {
            PlayerSettings.productName,
            PlayerSettings.companyName,
            PlayerSettings.bundleVersion,
            PlayerSettings.fullScreenMode,
            PlayerSettings.defaultScreenWidth,
            PlayerSettings.defaultScreenHeight,
            PlayerSettings.resizableWindow,
            PlayerSettings.runInBackground,
            Compression = PlayerSettings.WebGL.compressionFormat,
            PlayerSettings.WebGL.decompressionFallback,
            PlayerSettings.WebGL.template,
            PlayerSettings.WebGL.dataCaching,
            Splash = PlayerSettings.SplashScreen.show,
        };
        bool parked = false;
        bool ok = false;
        try
        {
            PlayerSettings.productName = "AccessCalibrate";
            PlayerSettings.companyName = "AccessCalibrate";
            PlayerSettings.bundleVersion = ResultsText.Version;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            // GitHub Pages can't send Content-Encoding headers: gzip + the loader's own decompression.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = Template;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.SplashScreen.show = false;   // straight into the calibration

            if (AssetDatabase.IsValidFolder(DemoResources))
            {
                string error = AssetDatabase.MoveAsset(DemoResources, DemoResourcesParked);
                if (!string.IsNullOrEmpty(error)) throw new Exception("could not move the demo Resources aside: " + error);
                parked = true;
            }

            if (target == BuildTarget.WebGL && Directory.Exists(location)) Directory.Delete(location, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = location,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            ok = s.result == BuildResult.Succeeded;
            Debug.Log($"[Build] {target}: {s.result}, {s.totalSize / 1048576.0:0.0} MB, {s.totalErrors} errors → {location}");
        }
        catch (Exception e)
        {
            Debug.LogError("[Build] " + e);
        }
        finally
        {
            if (parked)
            {
                string error = AssetDatabase.MoveAsset(DemoResourcesParked, DemoResources);
                if (!string.IsNullOrEmpty(error)) Debug.LogError("[Build] could not move the demo Resources back: " + error);
            }
            PlayerSettings.productName = saved.productName;
            PlayerSettings.companyName = saved.companyName;
            PlayerSettings.bundleVersion = saved.bundleVersion;
            PlayerSettings.fullScreenMode = saved.fullScreenMode;
            PlayerSettings.defaultScreenWidth = saved.defaultScreenWidth;
            PlayerSettings.defaultScreenHeight = saved.defaultScreenHeight;
            PlayerSettings.resizableWindow = saved.resizableWindow;
            PlayerSettings.runInBackground = saved.runInBackground;
            PlayerSettings.WebGL.compressionFormat = saved.Compression;
            PlayerSettings.WebGL.decompressionFallback = saved.decompressionFallback;
            PlayerSettings.WebGL.template = saved.template;
            PlayerSettings.WebGL.dataCaching = saved.dataCaching;
            PlayerSettings.SplashScreen.show = saved.Splash;
            AssetDatabase.SaveAssets();
        }
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }
}

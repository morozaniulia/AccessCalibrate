// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Development helpers for THIS project only (not part of the package).

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class AccessCalibrateDevTools
{
    const string PackageDemo = "Packages/com.accesscalibrate/Samples~/Demo";
    const string ImportedDemo = "Assets/Samples/AccessCalibrate/0.1.0/Demo";   // where Package Manager imports it
    const string SceneName = "AccessCalibrateDemo.unity";
    const string Renderer3DPath = "Assets/Settings/Renderer3D.asset";

    static string Full(string projectPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectPath));

    /// <summary>
    /// Adds a Universal (3D) renderer to every URP asset in the project, AFTER the existing ones,
    /// so the default renderer (2D) and everything using it stay unchanged. The demo camera picks it.
    /// </summary>
    [MenuItem("Tools/AccessCalibrate/Set Up 3D Renderer For Demo")]
    public static void SetUp3DRenderer()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, Renderer3DPath);
            Debug.Log("[DevTools] Created " + Renderer3DPath);
        }

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            bool present = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == data);
            if (present) { Debug.Log("[DevTools] 3D renderer already in " + path); continue; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Debug.Log("[DevTools] Added 3D renderer to " + path + " at index " + (list.arraySize - 1));
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Copies the demo sample into Assets, like Package Manager's "Import" button.</summary>
    [MenuItem("Tools/AccessCalibrate/Import Demo Sample (overwrite)")]
    public static void ImportDemo()
    {
        string src = Full(PackageDemo), dst = Full(ImportedDemo);
        if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
        CopyDirectory(src, dst);
        Debug.Log("[DevTools] Imported demo sample to " + ImportedDemo);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Creates the demo scene (one GameObject with DemoGame — the level is built in code) in the
    /// imported copy, then copies the scene and all generated .meta files back into Samples~ so
    /// GUIDs stay stable for everyone who imports the sample.
    /// </summary>
    [MenuItem("Tools/AccessCalibrate/Build Demo Scene And Sync Back")]
    public static void BuildDemoSceneAndSyncBack()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("AccessCalibration.Demo.DemoGame"))
            .FirstOrDefault(t => t != null);
        if (gameType == null) throw new Exception("DemoGame not compiled — run ImportDemo first.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("AccessCalibrate Demo");
        go.AddComponent(gameType);
        string scenePath = ImportedDemo + "/" + SceneName;
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        Debug.Log("[DevTools] Saved " + scenePath);

        SyncMetasBack();
        File.Copy(Full(scenePath), Path.Combine(Full(PackageDemo), SceneName), overwrite: true);
        Debug.Log("[DevTools] Synced scene and .meta files back to " + PackageDemo);
    }

    /// <summary>Copies every .meta the import generated back to Samples~ (only ones that are missing there).</summary>
    public static void SyncMetasBack()
    {
        string src = Full(ImportedDemo), dst = Full(PackageDemo);
        int copied = 0;
        foreach (var meta in Directory.GetFiles(src, "*.meta", SearchOption.AllDirectories))
        {
            string rel = meta.Substring(src.Length + 1);
            string target = Path.Combine(dst, rel);
            if (File.Exists(target)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(meta, target);
            copied++;
        }
        Debug.Log($"[DevTools] Copied {copied} .meta files back to Samples~");
    }

    static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src)) File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(src)) CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    // Batch-mode entry points: Unity.exe -batchmode -quit -executeMethod AccessCalibrateDevTools.<Name>
    public static void BatchSetUpAndImport() { SetUp3DRenderer(); ImportDemo(); }
    public static void BatchBuildScene() => BuildDemoSceneAndSyncBack();
}

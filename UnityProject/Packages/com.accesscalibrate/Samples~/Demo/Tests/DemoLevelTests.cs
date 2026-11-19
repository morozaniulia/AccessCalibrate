// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AccessCalibration.Demo.Tests
{
    /// <summary>
    /// Runs the real demo level (no calibration: a saved profile is provided) and checks each
    /// station shows the behaviour of the active settings. Each station is also rendered to
    /// UnityProject/Logs/demo-*.png so it can be checked by eye.
    /// </summary>
    public class DemoLevelTests
    {
        string _dir;
        GameObject _host;
        DemoGame _game;
        RenderTexture _rt;

        static AccessibilityProfile Adjusted() => new AccessibilityProfile
        {
            timingMultiplier = 2f, qteMode = QteMode.Skippable, holdToToggle = true,
            mashingMode = MashingMode.HoldInstead, stickDeadzone = 0.25f, aimAssist = AimAssist.Medium,
        };

        IEnumerator StartLevel(AccessibilityProfile profile)
        {
            AccessCalibrate.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateDemoTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            AccessCalibrate.SetProfile(profile);          // saved → no calibration on start

            _host = new GameObject("Demo");
            _game = _host.AddComponent<DemoGame>();
            yield return null;
            yield return null;

            _rt = new RenderTexture(1280, 720, 24);
            _game.Camera.targetTexture = _rt;
            _game.Hud.Panel.targetTexture = _rt;
            _game.Hud.Panel.clearColor = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            if (_rt != null) _rt.Release();
            AccessCalibrate.ResetStaticState();
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        IEnumerator Snapshot(string name)
        {
            // (No WaitForEndOfFrame: it never resumes in batch mode.)
            for (int i = 0; i < 4; i++) yield return null;
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "demo-" + name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Level_WithAdjustedProfile_EveryStationShowsTheSetting()
        {
            yield return StartLevel(Adjusted());
            Assert.IsFalse(_game.Runner.IsCalibrating, "a saved profile means no calibration");
            Assert.IsTrue(_game.Has3DRenderer, "the URP asset needs a Universal (3D) renderer for the demo");
            yield return Snapshot("1-start");

            // 1. Quick-time: double window (1.6 s) and a skip option.
            _game.TeleportBefore(DemoGame.Phase.Qte);
            yield return WaitFor(() => _game.Hud.PromptText == "Jump! A / Space", 4f);
            Assert.AreEqual("Jump! A / Space", _game.Hud.PromptText);
            StringAssert.Contains("skip", _game.Hud.HintText.ToLowerInvariant());
            yield return Snapshot("2-quick-time");

            // 2. Lever: hold-to-toggle → a single press.
            _game.TeleportBefore(DemoGame.Phase.Lever);
            yield return WaitFor(() => _game.Hud.PromptText == "Press X / E to pull the lever", 2f);
            Assert.AreEqual("Press X / E to pull the lever", _game.Hud.PromptText);
            yield return Snapshot("3-lever");

            // 3. Bridge: hold instead of mashing.
            _game.TeleportBefore(DemoGame.Phase.Bridge);
            yield return WaitFor(() => _game.Hud.PromptText == "Hold A / Space to lower the bridge", 2f);
            Assert.AreEqual("Hold A / Space to lower the bridge", _game.Hud.PromptText);
            yield return Snapshot("4-bridge");

            // 4. Narrow path: the deadzone is shown.
            _game.TeleportBefore(DemoGame.Phase.Explore);
            yield return WaitFor(() => _game.Hud.PromptText == "Narrow path", 2f);
            StringAssert.Contains("0.25", _game.Hud.HintText);
            yield return Snapshot("5-narrow-path");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Level_WithDefaultProfile_AsksToHoldAndToTap()
        {
            yield return StartLevel(new AccessibilityProfile());

            _game.TeleportBefore(DemoGame.Phase.Lever);
            yield return WaitFor(() => _game.Hud.PromptText == "Hold X / E to pull the lever", 2f);
            Assert.AreEqual("Hold X / E to pull the lever", _game.Hud.PromptText);

            _game.TeleportBefore(DemoGame.Phase.Bridge);
            yield return WaitFor(() => _game.Hud.PromptText == "Tap A / Space quickly to lower the bridge", 2f);
            Assert.AreEqual("Tap A / Space quickly to lower the bridge", _game.Hud.PromptText);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Level_QteOff_JumpsAutomatically()
        {
            var p = new AccessibilityProfile { qteMode = QteMode.Off };
            yield return StartLevel(p);
            _game.TeleportBefore(DemoGame.Phase.Qte);
            yield return WaitFor(() => _game.CurrentPhase == DemoGame.Phase.Qte, 2f);
            yield return WaitFor(() => _game.CurrentPhase == DemoGame.Phase.Explore, 6f);
            Assert.AreEqual(DemoGame.Phase.Explore, _game.CurrentPhase, "the jump should happen by itself");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FirstLaunch_StartsTheCalibration()
        {
            AccessCalibrate.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateDemoTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));   // empty store = first launch
            _host = new GameObject("Demo");
            _game = _host.AddComponent<DemoGame>();
            yield return null;
            Assert.IsTrue(_game.Runner.IsCalibrating);
        }
    }
}

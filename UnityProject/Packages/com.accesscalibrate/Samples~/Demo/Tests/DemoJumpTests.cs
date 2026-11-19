// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AccessCalibration.Demo.Tests
{
    /// <summary>Free jump with A / Space while exploring, with simulated keyboard input.</summary>
    public class DemoJumpTests : InputTestFixture
    {
        string _dir;
        GameObject _host;

        public override void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            base.TearDown();
            AccessCalibrate.ResetStaticState();
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SpaceWhileExploring_Jumps_WithoutStartingAStation()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            AccessCalibrate.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateDemoTests", Path.GetRandomFileName());
            AccessCalibrate.UseStore(new ProfileStore(_dir));
            AccessCalibrate.SetProfile(new AccessibilityProfile());   // saved → no calibration

            _host = new GameObject("Demo");
            var game = _host.AddComponent<DemoGame>();
            for (int i = 0; i < 10; i++) yield return null;            // land on the ground
            Assert.IsFalse(game.IsAirborne, "should start on the ground");
            float groundY = game.Player.position.y;

            Press(keyboard.spaceKey);
            float highest = groundY;
            // Game time, not wall-clock time: in batch mode frames can be slow.
            float start = Time.time;
            int frames = 0;
            while (Time.time - start < 0.8f)   // the test Timeout bounds this
            {
                yield return null;
                frames++;
                highest = Mathf.Max(highest, game.Player.position.y);
            }
            Release(keyboard.spaceKey);
            Debug.Log($"[DemoJumpTests] jump height {highest - groundY:0.00} m over {frames} frames");

            // 1.2 m in theory; a frame-step integration lands a little lower.
            Assert.Greater(highest - groundY, 0.9f, "the player should leave the ground");
            Assert.AreEqual(DemoGame.Phase.Explore, game.CurrentPhase);

            start = Time.time;
            while (game.IsAirborne && Time.time - start < 2f) yield return null;
            Assert.IsFalse(game.IsAirborne, "and land again");
        }
    }
}

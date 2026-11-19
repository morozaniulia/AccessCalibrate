// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AccessCalibration.Tests
{
    public class AccessCalibrateApiTests
    {
        string _dir;
        ProfileStore _store;
        List<AccessibilityProfile> _changes;
        int _calibrationRequests;

        [SetUp]
        public void SetUp()
        {
            AccessCalibrate.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            _store = new ProfileStore(_dir);
            AccessCalibrate.UseStore(_store);

            _changes = new List<AccessibilityProfile>();
            _calibrationRequests = 0;
            AccessCalibrate.OnProfileChanged += p => _changes.Add(p);
            AccessCalibrate.OnCalibrationRequested += () => _calibrationRequests++;
        }

        [TearDown]
        public void TearDown()
        {
            AccessCalibrate.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void Profile_BeforeAnyCalibration_IsTheDefaultProfile()
        {
            var expected = new AccessibilityProfile { stickDeadzone = AccessCalibrate.Config.DefaultStickDeadzone };
            Assert.AreEqual(expected, AccessCalibrate.Profile);
            Assert.IsFalse(AccessCalibrate.HasSavedProfile);
        }

        [Test]
        public void RunIfFirstLaunch_NoSavedProfile_RequestsCalibration()
        {
            Assert.IsTrue(AccessCalibrate.RunIfFirstLaunch());
            Assert.AreEqual(1, _calibrationRequests);
        }

        [Test]
        public void RunIfFirstLaunch_WithSavedProfile_DoesNothing()
        {
            AccessCalibrate.SetProfile(new AccessibilityProfile());
            Assert.IsFalse(AccessCalibrate.RunIfFirstLaunch());
            Assert.AreEqual(0, _calibrationRequests);
        }

        [Test]
        public void RequestCalibration_WithNoRunnerListening_WarnsInsteadOfFailingSilently()
        {
            AccessCalibrate.ResetStaticState();
            AccessCalibrate.UseStore(_store);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no calibration runner"));
            AccessCalibrate.RequestCalibration();
        }

        [Test]
        public void SetProfile_SavesToStore_AndRaisesEvent()
        {
            var p = new AccessibilityProfile { timingMultiplier = 1.5f, holdToToggle = true };
            AccessCalibrate.SetProfile(p);

            Assert.AreEqual(p, _store.Load());
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual(p, _changes[0]);
            Assert.AreEqual(p, AccessCalibrate.Profile);
        }

        [Test]
        public void Profile_ReturnsACopy_SoTheGameCantChangeItBehindTheApisBack()
        {
            var p = AccessCalibrate.Profile;
            p.timingMultiplier = 9f;
            Assert.AreEqual(1f, AccessCalibrate.Profile.timingMultiplier);
        }

        [Test]
        public void SetProfile_KeepsACopy_OfTheCallersObject()
        {
            var p = new AccessibilityProfile { timingMultiplier = 1.5f };
            AccessCalibrate.SetProfile(p);
            p.timingMultiplier = 9f;
            Assert.AreEqual(1.5f, AccessCalibrate.Profile.timingMultiplier);
        }

        [Test]
        public void Profile_IsLoadedFromStore_OnNextLaunch()
        {
            _store.Save(new AccessibilityProfile { mashingMode = MashingMode.HoldInstead });
            AccessCalibrate.UseStore(_store); // simulates a fresh start of the game

            Assert.AreEqual(MashingMode.HoldInstead, AccessCalibrate.Profile.mashingMode);
            Assert.IsFalse(AccessCalibrate.RunIfFirstLaunch());
        }

        [Test]
        public void SkipCalibration_SavesDefaults_SoItIsNotOfferedAgain()
        {
            AccessCalibrate.SkipCalibration();
            Assert.IsTrue(AccessCalibrate.HasSavedProfile);
            Assert.IsFalse(AccessCalibrate.RunIfFirstLaunch());
            Assert.AreEqual(1f, AccessCalibrate.Profile.timingMultiplier);
        }

        [Test]
        public void ResetToDefaults_UndoesChanges()
        {
            AccessCalibrate.SetProfile(new AccessibilityProfile { timingMultiplier = 2f, aimAssist = AimAssist.High });
            AccessCalibrate.ResetToDefaults();
            Assert.AreEqual(1f, AccessCalibrate.Profile.timingMultiplier);
            Assert.AreEqual(AimAssist.Off, AccessCalibrate.Profile.aimAssist);
            Assert.AreEqual(2, _changes.Count);
        }

        [Test]
        public void ExportThenImport_RoundTrips()
        {
            AccessCalibrate.SetProfile(new AccessibilityProfile { qteMode = QteMode.Off, stickDeadzone = 0.3f });
            string json = AccessCalibrate.ExportJson();
            AccessCalibrate.ResetToDefaults();

            Assert.IsTrue(AccessCalibrate.TryImportJson(json, out var error), error);
            Assert.AreEqual(QteMode.Off, AccessCalibrate.Profile.qteMode);
            Assert.AreEqual(0.3f, AccessCalibrate.Profile.stickDeadzone);
        }

        [Test]
        public void ImportInvalidJson_ChangesNothing_AndExplainsWhy()
        {
            AccessCalibrate.SetProfile(new AccessibilityProfile { holdToToggle = true });
            _changes.Clear();

            Assert.IsFalse(AccessCalibrate.TryImportJson("garbage", out var error));
            Assert.IsNotEmpty(error);
            Assert.IsTrue(AccessCalibrate.Profile.holdToToggle);
            Assert.IsEmpty(_changes);
        }
    }
}

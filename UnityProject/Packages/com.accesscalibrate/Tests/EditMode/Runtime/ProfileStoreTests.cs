// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AccessCalibration.Tests
{
    public class ProfileStoreTests
    {
        string _dir;
        ProfileStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            _store = new ProfileStore(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void NoFile_DoesNotExist_AndLoadReturnsNull()
        {
            Assert.IsFalse(_store.Exists);
            Assert.IsNull(_store.Load());
        }

        [Test]
        public void Save_CreatesFolderAndFile()
        {
            _store.Save(new AccessibilityProfile());
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "profile.json")));
            Assert.IsTrue(_store.Exists);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var p = new AccessibilityProfile
            {
                timingMultiplier = 2f,
                holdToToggle = true,
                remapSuggestions = new List<string> { "Jump" },
            };
            _store.Save(p);
            Assert.AreEqual(p, _store.Load());
        }

        [Test]
        public void Save_OverwritesPreviousProfile_AndLeavesNoTempFile()
        {
            _store.Save(new AccessibilityProfile { timingMultiplier = 1.5f });
            _store.Save(new AccessibilityProfile { timingMultiplier = 2f });
            Assert.AreEqual(2f, _store.Load().timingMultiplier);
            Assert.IsFalse(File.Exists(_store.FilePath + ".tmp"));
        }

        [Test]
        public void Save_CleansUpALeftoverTempFile_FromAnEarlierCrash()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_store.FilePath + ".tmp", "half-written garbage");
            _store.Save(new AccessibilityProfile { timingMultiplier = 1.5f });

            Assert.IsFalse(File.Exists(_store.FilePath + ".tmp"));
            Assert.AreEqual(1.5f, _store.Load().timingMultiplier);
        }

        [Test]
        public void Save_WhenWritingFails_TheOldProfileIsStillThere()
        {
            _store.Save(new AccessibilityProfile { timingMultiplier = 1.5f });
            // Block the temp file path with a folder so the new write cannot happen.
            Directory.CreateDirectory(_store.FilePath + ".tmp");

            Assert.That(() => _store.Save(new AccessibilityProfile { timingMultiplier = 2f }),
                        Throws.InstanceOf<IOException>().Or.InstanceOf<System.UnauthorizedAccessException>());
            Assert.AreEqual(1.5f, _store.Load().timingMultiplier);
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull_AndWarns()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_store.FilePath, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Could not read"));
            Assert.IsNull(_store.Load());
        }

        [Test]
        public void Delete_RemovesTheFile()
        {
            _store.Save(new AccessibilityProfile());
            _store.Delete();
            Assert.IsFalse(_store.Exists);
            Assert.DoesNotThrow(() => _store.Delete()); // deleting twice is fine
        }

        [Test]
        public void DefaultStore_LivesUnderPersistentDataPath()
        {
            var expected = Path.Combine(Application.persistentDataPath, "AccessCalibrate", "profile.json");
            Assert.AreEqual(expected, new ProfileStore().FilePath);
        }
    }
}

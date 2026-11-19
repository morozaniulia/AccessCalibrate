// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using AccessCalibration.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AccessCalibration.Tests
{
    public class StudyModeTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            StudyMode.ResetStaticState();
            _dir = Path.Combine(Path.GetTempPath(), "AccessCalibrateTests", Path.GetRandomFileName());
            StudyMode.FilePath = Path.Combine(_dir, "study", "study.csv");
        }

        [TearDown]
        public void TearDown()
        {
            StudyMode.ResetStaticState();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void IsOffByDefault_AndDefaultsToALocalFile()
        {
            StudyMode.ResetStaticState();
            Assert.IsFalse(StudyMode.Enabled);
            StringAssert.StartsWith(Application.persistentDataPath, StudyMode.FilePath);
            StringAssert.EndsWith("study.csv", StudyMode.FilePath);
        }

        [Test]
        public void Record_WritesTheHeaderOnce_ThenOneRowPerCalibration()
        {
            StudyMode.Label = "normal";
            Assert.IsTrue(StudyMode.Record(new Measurements(), new AccessibilityProfile()));
            StudyMode.Label = "gloves";
            Assert.IsTrue(StudyMode.Record(new Measurements(), new AccessibilityProfile()));

            var lines = File.ReadAllLines(StudyMode.FilePath);
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual(StudyCsv.Header, lines[0]);
            StringAssert.Contains(",normal,", lines[1]);
            StringAssert.Contains(",gloves,", lines[2]);
        }

        [Test]
        public void Record_RaisesRecordedWithThePath()
        {
            string recorded = null;
            StudyMode.Recorded += p => recorded = p;
            StudyMode.Record(new Measurements(), new AccessibilityProfile());
            Assert.AreEqual(StudyMode.FilePath, recorded);
        }

        [Test]
        public void Record_WhenTheFileCantBeWritten_WarnsAndReturnsFalse()
        {
            // A folder where the file should be makes the write fail.
            Directory.CreateDirectory(StudyMode.FilePath);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Study mode could not write"));
            Assert.IsFalse(StudyMode.Record(new Measurements(), new AccessibilityProfile()));
        }
    }
}

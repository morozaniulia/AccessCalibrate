// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using AccessCalibration.Core;
using UnityEngine;

namespace AccessCalibration
{
    /// <summary>
    /// Study mode: when enabled, every finished calibration appends one row to a local CSV file
    /// (raw measurements, proposed profile, and <see cref="Label"/>). Off by default.
    /// The file never leaves the device; compare files with tools/plot_study.py.
    /// </summary>
    public static class StudyMode
    {
        public const string FolderName = "study";
        public const string FileName = "study.csv";

        /// <summary>Record calibrations to the CSV file.</summary>
        public static bool Enabled { get; set; }

        /// <summary>Free text describing the session, e.g. "normal", "non-dominant hand", "gloves".</summary>
        public static string Label { get; set; } = "";

        /// <summary>The CSV file. Defaults to persistentDataPath/AccessCalibrate/study/study.csv.</summary>
        public static string FilePath
        {
            get => s_FilePath ?? Path.Combine(Application.persistentDataPath, ProfileStore.FolderName, FolderName, FileName);
            set => s_FilePath = value;
        }

        static string s_FilePath;

        /// <summary>Raised after a row was written, with the file path.</summary>
        public static event Action<string> Recorded;

        /// <summary>
        /// Appends one row (writes the header first if the file is new). Returns false and logs a
        /// warning if the file can't be written — study mode must never break the game.
        /// </summary>
        public static bool Record(Measurements measurements, AccessibilityProfile proposed, DateTime? utcNow = null)
        {
            string path = FilePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                bool isNew = !File.Exists(path) || new FileInfo(path).Length == 0;
                using (var w = new StreamWriter(path, append: true, encoding: new System.Text.UTF8Encoding(false)))
                {
                    if (isNew) w.WriteLine(StudyCsv.Header);
                    w.WriteLine(StudyCsv.Row(utcNow ?? DateTime.UtcNow, Label, measurements, proposed));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[AccessCalibrate] Study mode could not write {path}: {e.Message}");
                return false;
            }
            Recorded?.Invoke(path);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            Enabled = false;
            Label = "";
            s_FilePath = null;
            Recorded = null;
        }
    }
}

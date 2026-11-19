// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using AccessCalibration.Core;
using UnityEngine;

namespace AccessCalibration
{
    /// <summary>
    /// Where the profile is kept. The default is a local JSON file; a game can plug in its own
    /// (e.g. its save system) via <see cref="AccessCalibrate.UseStore"/>.
    /// </summary>
    public interface IProfileStore
    {
        bool Exists { get; }
        /// <summary>Returns the saved profile, or null if there is none or it can't be read.</summary>
        AccessibilityProfile Load();
        void Save(AccessibilityProfile profile);
        void Delete();
    }

    /// <summary>
    /// Saves the profile as a JSON file on this device only (no network, no telemetry).
    /// Default location: <c>Application.persistentDataPath/AccessCalibrate/profile.json</c>.
    /// </summary>
    public sealed class ProfileStore : IProfileStore
    {
        public const string FolderName = "AccessCalibrate";
        public const string FileName = "profile.json";

        public string FilePath { get; }

        /// <summary>Uses the default location under <c>Application.persistentDataPath</c>.</summary>
        public ProfileStore() : this(Path.Combine(Application.persistentDataPath, FolderName)) { }

        /// <summary>Uses <paramref name="directory"/>/profile.json (handy for tests or custom setups).</summary>
        public ProfileStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            FilePath = Path.Combine(directory, FileName);
        }

        public bool Exists => File.Exists(FilePath);

        public AccessibilityProfile Load()
        {
            if (!Exists) return null;
            try
            {
                return AccessibilityProfile.FromJson(File.ReadAllText(FilePath));
            }
            catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException)
            {
                // A damaged file must never crash the game: fall back to "no profile yet".
                Debug.LogWarning($"[AccessCalibrate] Could not read {FilePath}: {e.Message}. Using default settings.");
                return null;
            }
        }

        public void Save(AccessibilityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            // Write to a temporary file first, then swap it in, so a crash mid-write
            // can't leave a half-written profile behind.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, profile.ToJson());
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        public void Delete()
        {
            if (Exists) File.Delete(FilePath);
        }
    }
}

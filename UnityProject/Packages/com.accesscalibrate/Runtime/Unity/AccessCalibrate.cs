// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using AccessCalibration.Core;
using UnityEngine;

namespace AccessCalibration
{
    /// <summary>
    /// The public API a game uses.
    /// <code>
    /// using AccessCalibration;
    ///
    /// AccessCalibrate.RunIfFirstLaunch();
    /// var p = AccessCalibrate.Profile;
    /// float window = baseWindow * p.timingMultiplier;
    /// AccessCalibrate.OnProfileChanged += Apply;
    /// </code>
    /// The player always has the final say: the calibration only proposes, and
    /// <see cref="SetProfile"/> is called with what the player confirmed.
    /// </summary>
    public static class AccessCalibrate
    {
        /// <summary>Raised after the profile changes (calibration accepted, settings menu, import…).</summary>
        public static event Action<AccessibilityProfile> OnProfileChanged;

        /// <summary>
        /// Raised when a calibration should start (first launch, or "redo calibration").
        /// The calibration runner listens to this.
        /// </summary>
        public static event Action OnCalibrationRequested;

        /// <summary>The thresholds used by the rules engine. Games may replace or edit it before calibrating.</summary>
        public static RulesConfig Config { get; set; } = new RulesConfig();

        static IProfileStore s_Store;
        static AccessibilityProfile s_Current;

        static IProfileStore Store => s_Store ?? (s_Store = new ProfileStore());

        /// <summary>
        /// True once the player has accepted or skipped a calibration and that profile can actually
        /// be read back. A damaged file counts as "no profile", so the calibration is offered again.
        /// </summary>
        public static bool HasSavedProfile => Store.Load() != null;

        /// <summary>
        /// The current settings. Returns a copy: change settings with <see cref="SetProfile"/>
        /// so the profile is saved and <see cref="OnProfileChanged"/> fires.
        /// Before any calibration this is the default profile (nothing changed).
        /// </summary>
        public static AccessibilityProfile Profile => Current.Clone();

        static AccessibilityProfile Current
        {
            get
            {
                if (s_Current == null)
                    s_Current = Store.Load() ?? DefaultProfile();
                return s_Current;
            }
        }

        /// <summary>The settings before any change: what "nothing proposed" means for this game.</summary>
        public static AccessibilityProfile DefaultProfile() =>
            new AccessibilityProfile { stickDeadzone = Config.DefaultStickDeadzone };

        /// <summary>
        /// Starts the calibration if the player has never accepted or skipped one.
        /// Returns true if a calibration was requested.
        /// </summary>
        public static bool RunIfFirstLaunch()
        {
            if (HasSavedProfile) return false;
            RequestCalibration();
            return true;
        }

        /// <summary>Starts the calibration (e.g. "Redo calibration" in the options menu).</summary>
        public static void RequestCalibration()
        {
            if (OnCalibrationRequested == null)
                Debug.LogWarning("[AccessCalibrate] Calibration requested, but no calibration runner is listening.");
            OnCalibrationRequested?.Invoke();
        }

        /// <summary>
        /// Saves the settings the player confirmed and notifies the game.
        /// If the disk can't be written (full, read-only…), the settings still apply for this
        /// session and a warning is logged — the game never crashes because of it.
        /// </summary>
        public static void SetProfile(AccessibilityProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            s_Current = profile.Clone();
            try
            {
                Store.Save(s_Current);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[AccessCalibrate] Could not save the profile: {e.Message}. " +
                                 "The settings apply for this session only.");
            }
            OnProfileChanged?.Invoke(s_Current.Clone());
        }

        /// <summary>
        /// "Skip": the player doesn't want to calibrate. Saves the default profile so the
        /// calibration isn't offered again on the next launch (it stays available from options).
        /// </summary>
        public static void SkipCalibration() => SetProfile(DefaultProfile());

        /// <summary>Back to default settings (saved, event raised).</summary>
        public static void ResetToDefaults() => SetProfile(DefaultProfile());

        /// <summary>The current profile as JSON text (to show, copy, or share between devices).</summary>
        public static string ExportJson() => Current.ToJson();

        /// <summary>
        /// Replaces the profile with one from JSON text. On bad input nothing changes,
        /// it returns false and <paramref name="error"/> explains why.
        /// </summary>
        public static bool TryImportJson(string json, out string error)
        {
            AccessibilityProfile imported;
            try
            {
                imported = AccessibilityProfile.FromJson(json);
            }
            catch (FormatException e)
            {
                error = e.Message;
                return false;
            }
            SetProfile(imported);
            error = null;
            return true;
        }

        /// <summary>
        /// Use a different place to keep the profile (e.g. the game's own save system).
        /// Call it before reading <see cref="Profile"/>. The profile is re-read from the new store.
        /// </summary>
        public static void UseStore(IProfileStore store)
        {
            s_Store = store ?? throw new ArgumentNullException(nameof(store));
            s_Current = null;
        }

        // Static fields survive between Play sessions when "Enter Play Mode Options" disables
        // domain reload, so start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            OnProfileChanged = null;
            OnCalibrationRequested = null;
            Config = new RulesConfig();
            s_Store = null;
            s_Current = null;
        }
    }
}

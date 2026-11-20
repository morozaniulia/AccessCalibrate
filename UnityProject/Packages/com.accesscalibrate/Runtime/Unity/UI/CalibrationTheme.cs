// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace AccessCalibration
{
    /// <summary>The three built-in looks (Docs/design-v2/DESIGN_SPEC_v2.md).</summary>
    public enum ThemePreset
    {
        /// <summary>Galaxy background, light text, warm off-white accent (default).</summary>
        Dark = 0,
        /// <summary>Light grey gradient, dark text, dark accent.</summary>
        Light = 1,
        /// <summary>Black background, white text and lines, yellow accent, nothing translucent.</summary>
        HighContrast = 2,
    }

    /// <summary>
    /// How the calibration screens look. Create one with Assets → Create → AccessCalibrate → Theme.
    /// The preset's USS is applied first, then <see cref="themeOverrides"/> (a USS file that only
    /// overrides --ac-… variables), so a theme changes exactly what it overrides. Changes are live
    /// in Play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "AccessCalibrate/Theme", fileName = "CalibrationTheme")]
    public sealed class CalibrationTheme : ScriptableObject
    {
        public ThemePreset preset = ThemePreset.Dark;

        [Tooltip("Optional USS that overrides --ac-… variables on .ac-root (colours, radii, fonts).")]
        public StyleSheet themeOverrides;

        [Tooltip("Optional font for every text. Empty = Manrope (included, SIL Open Font License).")]
        public FontAsset font;

        /// <summary>Raised when any theme asset is edited (Inspector), so open screens restyle at once.</summary>
        public static event Action<CalibrationTheme> Edited;

        void OnValidate() => Edited?.Invoke(this);

        /// <summary>A theme that is just one of the presets.</summary>
        public static CalibrationTheme FromPreset(ThemePreset preset)
        {
            var t = CreateInstance<CalibrationTheme>();
            t.name = preset.ToString();
            t.preset = preset;
            return t;
        }

        /// <summary>The preset's override USS (null for Dark — it is the default stylesheet).</summary>
        public static StyleSheet LoadPresetSheet(ThemePreset preset)
        {
            switch (preset)
            {
                case ThemePreset.Light: return Resources.Load<StyleSheet>("AccessCalibrate/Themes/Light");
                case ThemePreset.HighContrast: return Resources.Load<StyleSheet>("AccessCalibrate/Themes/HighContrast");
                default: return null;
            }
        }
    }
}

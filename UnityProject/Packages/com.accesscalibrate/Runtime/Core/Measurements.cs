// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System.Collections.Generic;

namespace AccessCalibration.Core
{
    /// <summary>The kind of input device a measurement was taken with.</summary>
    public enum InputDeviceKind
    {
        Gamepad,
        Keyboard,
        Mouse,
        Touch,
    }

    /// <summary>How much a measurement can be trusted.</summary>
    public enum Confidence
    {
        /// <summary>Enough valid trials; rules may act on it.</summary>
        Ok,
        /// <summary>Too few valid trials (even after one retry). Rules still act, but the reason is flagged.</summary>
        Uncertain,
        /// <summary>The player skipped the test, or the device can't do it. Rules never act on it.</summary>
        Skipped,
    }

    /// <summary>Fields shared by every calibration result.</summary>
    public abstract class MeasurementBase
    {
        public InputDeviceKind Device = InputDeviceKind.Gamepad;
        public Confidence Confidence = Confidence.Skipped;

        /// <summary>True when rules are allowed to use this measurement.</summary>
        public bool IsUsable => Confidence != Confidence.Skipped;
    }

    /// <summary>Test 1 — reaction time.</summary>
    public sealed class ReactionMeasurement : MeasurementBase
    {
        /// <summary>Every trial as measured, in milliseconds, in order (first trial included).</summary>
        public List<double> RawTrialsMs = new List<double>();
        /// <summary>Median of the valid trials (first dropped, outliers removed), in milliseconds.</summary>
        public double MedianMs;
        /// <summary>How many trials survived filtering.</summary>
        public int ValidTrialCount;
    }

    /// <summary>Test 2 — comfortable hold duration.</summary>
    public sealed class HoldMeasurement : MeasurementBase
    {
        public List<double> RawHoldsSeconds = new List<double>();
        /// <summary>Longest hold across trials, capped at the test maximum (5 s).</summary>
        public double MaxHoldSeconds;
    }

    /// <summary>Test 3 — repeated presses.</summary>
    public sealed class TapMeasurement : MeasurementBase
    {
        public int PressCount;
        public double DurationSeconds;
        public double PressesPerSecond => DurationSeconds > 0 ? PressCount / DurationSeconds : 0;
    }

    /// <summary>Test 4 — keeping a cursor inside a circle.</summary>
    public sealed class StabilityMeasurement : MeasurementBase
    {
        /// <summary>RMS distance from the circle centre, as a fraction of the circle radius (0 = perfectly centred).</summary>
        public double RmsDeviation;
    }

    /// <summary>Test 5 — can each main game button be pressed?</summary>
    public sealed class ButtonAccessMeasurement : MeasurementBase
    {
        /// <summary>The buttons the game asked about (configured by the developer).</summary>
        public List<string> RequestedButtons = new List<string>();
        /// <summary>The buttons that were not pressed within the time limit.</summary>
        public List<string> UnpressedButtons = new List<string>();
    }

    /// <summary>All five calibration results together. Any of them may be Skipped.</summary>
    public sealed class Measurements
    {
        public ReactionMeasurement Reaction = new ReactionMeasurement();
        public HoldMeasurement Hold = new HoldMeasurement();
        public TapMeasurement Taps = new TapMeasurement();
        public StabilityMeasurement Stability = new StabilityMeasurement();
        public ButtonAccessMeasurement Buttons = new ButtonAccessMeasurement();
    }
}

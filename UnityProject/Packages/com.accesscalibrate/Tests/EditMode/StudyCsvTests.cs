// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace AccessCalibration.Core.Tests
{
    public class StudyCsvTests
    {
        static readonly DateTime When = new DateTime(2026, 11, 19, 14, 30, 5, 250, DateTimeKind.Utc);

        static Measurements Sample() => new Measurements
        {
            Reaction = new ReactionMeasurement
            {
                Device = InputDeviceKind.Gamepad, Confidence = Confidence.Ok,
                RawTrialsMs = { 900, 412.5, 380 }, MedianMs = 396.25, ValidTrialCount = 2,
            },
            Hold = new HoldMeasurement { Confidence = Confidence.Uncertain, RawHoldsSeconds = { 0, 1.5 }, MaxHoldSeconds = 1.5 },
            Taps = new TapMeasurement { Confidence = Confidence.Ok, PressCount = 21, DurationSeconds = 5 },
            Stability = new StabilityMeasurement { Confidence = Confidence.Skipped },
            Buttons = new ButtonAccessMeasurement
            {
                Confidence = Confidence.Ok, RequestedButtons = { "A", "B" }, UnpressedButtons = { "B" },
            },
        };

        static string Cell(string row, string column) =>
            StudyCsv.ParseLine(row)[Array.IndexOf(StudyCsv.Columns, column)];

        [Test]
        public void Row_HasOneCellPerColumn()
        {
            var row = StudyCsv.Row(When, "normal", Sample(), new AccessibilityProfile());
            Assert.AreEqual(StudyCsv.Columns.Length, StudyCsv.ParseLine(row).Count);
            Assert.AreEqual(StudyCsv.Columns.Length, StudyCsv.ParseLine(StudyCsv.Header).Count);
        }

        [Test]
        public void Row_ContainsRawDataProposalAndLabel()
        {
            var proposed = new AccessibilityProfile { timingMultiplier = 1.5f, holdToToggle = true, remapSuggestions = { "B" } };
            var row = StudyCsv.Row(When, "gloves", Sample(), proposed);

            Assert.AreEqual("2026-11-19T14:30:05.250Z", Cell(row, "timestamp_utc"));
            Assert.AreEqual("gloves", Cell(row, "label"));
            Assert.AreEqual("Gamepad", Cell(row, "reaction_device"));
            Assert.AreEqual("396.25", Cell(row, "reaction_median_ms"));
            Assert.AreEqual("900;412.5;380", Cell(row, "reaction_raw_ms"));
            Assert.AreEqual("Uncertain", Cell(row, "hold_confidence"));
            Assert.AreEqual("0;1.5", Cell(row, "hold_raw_s"));
            Assert.AreEqual("4.2", Cell(row, "taps_per_s"));
            Assert.AreEqual("A;B", Cell(row, "buttons_requested"));
            Assert.AreEqual("B", Cell(row, "buttons_unpressed"));
            Assert.AreEqual("1.5", Cell(row, "proposed_timingMultiplier"));
            Assert.AreEqual("true", Cell(row, "proposed_holdToToggle"));
            Assert.AreEqual("B", Cell(row, "proposed_remapSuggestions"));
        }

        [Test]
        public void SkippedMeasurement_LeavesItsValuesEmpty()
        {
            var row = StudyCsv.Row(When, "x", Sample(), new AccessibilityProfile());
            Assert.AreEqual("Skipped", Cell(row, "stability_confidence"));
            Assert.AreEqual("", Cell(row, "stability_spread"));
            Assert.AreEqual("", Cell(row, "stability_mean_offset"));
        }

        [Test]
        public void LabelWithCommasQuotesAndLineBreaks_StaysOneCellOnOneLine()
        {
            var row = StudyCsv.Row(When, "left hand, \"tired\"\nafter lunch", Sample(), new AccessibilityProfile());
            Assert.IsFalse(row.Contains("\n"));
            Assert.AreEqual("left hand, \"tired\" after lunch", Cell(row, "label"));
        }

        [Test]
        public void Numbers_UseADot_EvenOnACommaDecimalSystem()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ro-RO");
                var row = StudyCsv.Row(When, "x", Sample(), new AccessibilityProfile());
                Assert.AreEqual("396.25", Cell(row, "reaction_median_ms"));
                Assert.AreEqual(StudyCsv.Columns.Length, StudyCsv.ParseLine(row).Count);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}

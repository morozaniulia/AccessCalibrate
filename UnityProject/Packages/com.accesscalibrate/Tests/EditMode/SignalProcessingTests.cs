// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AccessCalibration.Core.Tests
{
    public class SignalProcessingTests
    {
        [Test]
        public void Median_OddCount_ReturnsMiddleValue()
        {
            Assert.AreEqual(300, SignalProcessing.Median(new double[] { 500, 300, 100 }));
        }

        [Test]
        public void Median_EvenCount_ReturnsAverageOfTwoMiddleValues()
        {
            Assert.AreEqual(350, SignalProcessing.Median(new double[] { 400, 100, 300, 900 }));
        }

        [Test]
        public void Median_IsNotPulledByOneExtremeValue()
        {
            // The mean would be 1180; the median ignores the single slow trial.
            Assert.AreEqual(300, SignalProcessing.Median(new double[] { 300, 300, 300, 300, 4700 }));
        }

        [Test]
        public void Median_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => SignalProcessing.Median(new double[0]));
        }

        [Test]
        public void DropFirst_RemovesOnlyTheFirstTrial()
        {
            CollectionAssert.AreEqual(new[] { 2.0, 3.0 }, SignalProcessing.DropFirst(new[] { 1.0, 2.0, 3.0 }));
        }

        [Test]
        public void DropFirst_EmptyOrNull_ReturnsEmpty()
        {
            Assert.IsEmpty(SignalProcessing.DropFirst(new double[0]));
            Assert.IsEmpty(SignalProcessing.DropFirst(null));
        }

        [Test]
        public void FilterOutliers_KeepsBoundsInclusive()
        {
            var kept = SignalProcessing.FilterOutliers(new[] { 119.0, 120.0, 500.0, 2000.0, 2001.0 }, 120, 2000);
            CollectionAssert.AreEqual(new[] { 120.0, 500.0, 2000.0 }, kept);
        }

        [Test]
        public void SummarizeReaction_DropsFirstTrialAndOutliers_ThenTakesMedian()
        {
            // 900 = first trial (learning effect), 80 = anticipation, 2500 = inattention.
            var raw = new List<double> { 900, 400, 80, 500, 2500, 600, 450 };
            var s = SignalProcessing.SummarizeReaction(raw);

            Assert.AreEqual(4, s.ValidTrialCount);           // 400, 500, 600, 450
            Assert.AreEqual(475, s.MedianMs, 1e-9);           // median of 400,450,500,600
            Assert.AreEqual(Confidence.Ok, s.Confidence);
        }

        [Test]
        public void SummarizeReaction_TooFewValidTrials_IsUncertain()
        {
            var s = SignalProcessing.SummarizeReaction(new double[] { 500, 50, 60, 400, 3000, 450 });
            Assert.AreEqual(2, s.ValidTrialCount);
            Assert.AreEqual(Confidence.Uncertain, s.Confidence);
            Assert.AreEqual(425, s.MedianMs, 1e-9);
        }

        [Test]
        public void SummarizeReaction_NoValidTrials_IsSkipped()
        {
            var s = SignalProcessing.SummarizeReaction(new double[] { 500, 50, 60, 3000 });
            Assert.AreEqual(0, s.ValidTrialCount);
            Assert.AreEqual(Confidence.Skipped, s.Confidence);
        }

        [Test]
        public void RmsDeviation_AllAtCentre_IsZero()
        {
            var samples = new List<(double, double)> { (5, 5), (5, 5) };
            Assert.AreEqual(0, SignalProcessing.RmsDeviation(samples, 5, 5, 1), 1e-12);
        }

        [Test]
        public void RmsDeviation_IsNormalisedByRadius()
        {
            // Distances 3 and 4 → RMS = sqrt((9 + 16) / 2) = 3.5355…; radius 10 → 0.35355…
            var samples = new List<(double, double)> { (3, 0), (0, 4) };
            Assert.AreEqual(Math.Sqrt(12.5) / 10, SignalProcessing.RmsDeviation(samples, 0, 0, 10), 1e-12);
        }

        [Test]
        public void RmsDeviation_InvalidInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => SignalProcessing.RmsDeviation(new List<(double, double)>(), 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SignalProcessing.RmsDeviation(new List<(double, double)> { (1, 1) }, 0, 0, 0));
        }
    }
}

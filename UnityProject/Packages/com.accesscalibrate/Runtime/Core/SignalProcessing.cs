// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.
// Pure C# — no UnityEngine dependency (portable to other engines).

using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessCalibration.Core
{
    /// <summary>Small, pure helper functions for turning raw trials into one number.</summary>
    public static class SignalProcessing
    {
        /// <summary>Reactions faster than this are treated as anticipation (pressed before seeing the signal).</summary>
        public const double MinValidReactionMs = 120;
        /// <summary>Reactions slower than this are treated as inattention.</summary>
        public const double MaxValidReactionMs = 2000;
        /// <summary>Fewer valid reaction trials than this → the result is Uncertain.</summary>
        public const int MinValidReactionTrials = 3;

        /// <summary>Median of the values. Throws if the list is empty.</summary>
        public static double Median(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("Median needs at least one value.", nameof(values));

            var sorted = values.OrderBy(v => v).ToArray();
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        /// <summary>Removes the first trial (learning effect). Returns a new list.</summary>
        public static List<double> DropFirst(IReadOnlyList<double> trials)
        {
            return trials == null ? new List<double>() : trials.Skip(1).ToList();
        }

        /// <summary>Keeps only values inside [min, max] (inclusive). Returns a new list.</summary>
        public static List<double> FilterOutliers(IReadOnlyList<double> values, double min, double max)
        {
            return values == null ? new List<double>() : values.Where(v => v >= min && v <= max).ToList();
        }

        /// <summary>
        /// Full reaction pipeline: drop the first trial, drop anticipations (&lt;120 ms) and
        /// inattention (&gt;2000 ms), then take the median of what's left.
        /// Confidence is Uncertain if fewer than <see cref="MinValidReactionTrials"/> remain,
        /// and Skipped if none remain.
        /// </summary>
        public static ReactionSummary SummarizeReaction(IReadOnlyList<double> rawTrialsMs)
        {
            var valid = FilterOutliers(DropFirst(rawTrialsMs), MinValidReactionMs, MaxValidReactionMs);

            if (valid.Count == 0)
                return new ReactionSummary(0, 0, Confidence.Skipped);

            var confidence = valid.Count >= MinValidReactionTrials ? Confidence.Ok : Confidence.Uncertain;
            return new ReactionSummary(Median(valid), valid.Count, confidence);
        }

        /// <summary>
        /// 2D standard deviation of the samples around THEIR OWN mean point, divided by the radius:
        /// sqrt(mean(|p − mean|²)) / radius. A player who holds perfectly still anywhere gets 0.
        /// </summary>
        public static double Spread2D(IReadOnlyList<(double x, double y)> samples, double radius)
        {
            var (mx, my) = MeanPoint(samples);
            CheckRadius(radius);
            double sumSquares = 0;
            foreach (var (x, y) in samples)
            {
                double dx = x - mx, dy = y - my;
                sumSquares += dx * dx + dy * dy;
            }
            return Math.Sqrt(sumSquares / samples.Count) / radius;
        }

        /// <summary>Distance from (centreX, centreY) to the samples' mean point, divided by the radius.</summary>
        public static double MeanOffset(IReadOnlyList<(double x, double y)> samples,
                                        double centreX, double centreY, double radius)
        {
            var (mx, my) = MeanPoint(samples);
            CheckRadius(radius);
            double dx = mx - centreX, dy = my - centreY;
            return Math.Sqrt(dx * dx + dy * dy) / radius;
        }

        /// <summary>Average position of the samples. Throws if there are none.</summary>
        public static (double x, double y) MeanPoint(IReadOnlyList<(double x, double y)> samples)
        {
            if (samples == null || samples.Count == 0)
                throw new ArgumentException("Need at least one sample.", nameof(samples));
            double sx = 0, sy = 0;
            foreach (var (x, y) in samples) { sx += x; sy += y; }
            return (sx / samples.Count, sy / samples.Count);
        }

        static void CheckRadius(double radius)
        {
            if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");
        }
    }

    /// <summary>Result of <see cref="SignalProcessing.SummarizeReaction"/>.</summary>
    public readonly struct ReactionSummary
    {
        public readonly double MedianMs;
        public readonly int ValidTrialCount;
        public readonly Confidence Confidence;

        public ReactionSummary(double medianMs, int validTrialCount, Confidence confidence)
        {
            MedianMs = medianMs;
            ValidTrialCount = validTrialCount;
            Confidence = confidence;
        }
    }
}

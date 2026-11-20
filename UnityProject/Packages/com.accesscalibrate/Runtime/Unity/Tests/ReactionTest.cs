// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using AccessCalibration.Core;
using UnityEngine.InputSystem;

namespace AccessCalibration
{
    /// <summary>
    /// Test 1 — reaction. After a random pause of 1–3 s a signal appears; press any button.
    /// 6 trials; the first is dropped (learning effect); anticipations (&lt;120 ms) and
    /// inattention (&gt;2000 ms) are ignored; result = median. If too few trials are valid,
    /// the round is repeated once; after that the result is marked Uncertain.
    /// </summary>
    public sealed class ReactionTest : CalibrationTest
    {
        public int Trials = 6;
        public double MinWaitSeconds = 1.0;
        public double MaxWaitSeconds = 3.0;
        /// <summary>No press this long after the signal → the trial counts as inattention.</summary>
        public double TrialTimeoutSeconds = 2.5;
        /// <summary>Random source for the pauses. Replace with a seeded one for reproducible runs.</summary>
        public Random Random = new Random();

        enum State { WaitingForRelease, Waiting, Showing }

        State _state;
        bool _retried;
        readonly List<double> _round = new List<double>();
        readonly List<double> _allTrials = new List<double>();
        ReactionSummary _summary;
        InputAction _press;

        /// <summary>When the next signal is due (Clock time). Exposed for tests.</summary>
        public double NextStimulusAt { get; private set; }
        /// <summary>When the current signal appeared (Clock time).</summary>
        public double StimulusShownAt { get; private set; }
        public bool StimulusVisible => IsRunning && _state == State.Showing;

        // Read-only progress for screens (no effect on the measurement).
        /// <summary>The trial in progress, 1-based, within the current round.</summary>
        public int TrialNumber => Math.Min(_round.Count + 1, Trials);

        public override string Title => CalibrationTexts.ReactionTitle;
        public override string Instructions => CalibrationTexts.ReactionInstructions;

        protected override void OnBegin(double now)
        {
            _retried = false;
            _round.Clear();
            _allTrials.Clear();
            _press = InputDevices.CreateAnyButton("AccessCalibrate/ReactionPress");
            _press.performed += OnPress;
            _press.Enable();
            StartTrial(now);
        }

        void StartTrial(double now)
        {
            _state = State.Waiting;
            NextStimulusAt = now + MinWaitSeconds + Random.NextDouble() * (MaxWaitSeconds - MinWaitSeconds);
            View.ShowStimulus(false);
            View.SetStatus(string.Format(CalibrationTexts.ReactionTrial, _round.Count + 1, Trials)
                           + " · " + CalibrationTexts.ReactionWait);
        }

        protected override void OnTick(double now)
        {
            if (_state == State.WaitingForRelease)
            {
                if (!InputDevices.AnyButtonHeld()) StartTrial(now);   // new random pause from the release
            }
            else if (_state == State.Waiting && now >= NextStimulusAt && InputDevices.AnyButtonHeld())
            {
                // The signal is due but a button is still down: a new press couldn't be seen.
                _state = State.WaitingForRelease;
                View.SetStatus(CalibrationTexts.ReactionLetGo);
            }
            else if (_state == State.Waiting && now >= NextStimulusAt)
            {
                _state = State.Showing;
                StimulusShownAt = now;
                View.ShowStimulus(true);
                View.PlayCue();
            }
            else if (_state == State.Showing && now - StimulusShownAt > TrialTimeoutSeconds)
            {
                Record((now - StimulusShownAt) * 1000.0); // > 2000 ms → filtered as inattention
            }
        }

        void OnPress(InputAction.CallbackContext ctx)
        {
            if (!IsRunning || Ignored(ctx) || _state == State.WaitingForRelease) return;
            Remember(ctx);

            if (_state == State.Showing)
            {
                // Event time minus stimulus time — both on the realtimeSinceStartup axis.
                Record((ctx.time - StimulusShownAt) * 1000.0);
            }
            else
            {
                // Pressed before the signal: an anticipation. Negative → filtered out later.
                View.SetStatus(CalibrationTexts.ReactionTooEarly);
                Record((ctx.time - NextStimulusAt) * 1000.0);
            }
        }

        void Record(double ms)
        {
            _round.Add(ms);
            _allTrials.Add(ms);
            View.ShowStimulus(false);

            if (_round.Count < Trials)
            {
                StartTrial(Clock());
                return;
            }

            _summary = SignalProcessing.SummarizeReaction(_round);
            if (_summary.Confidence != Confidence.Ok && !_retried)
            {
                _retried = true;
                _round.Clear();
                View.SetStatus(CalibrationTexts.ReactionRetry);
                StartTrial(Clock());
                return;
            }
            Finish();
        }

        protected override void OnStop()
        {
            if (_press == null) return;
            _press.performed -= OnPress;
            Retire(_press);
            _press = null;
        }

        public override void WriteResult(Measurements m)
        {
            m.Reaction = new ReactionMeasurement
            {
                Device = Device,
                RawTrialsMs = new List<double>(_allTrials),
                MedianMs = WasSkipped ? 0 : _summary.MedianMs,
                ValidTrialCount = WasSkipped ? 0 : _summary.ValidTrialCount,
                Confidence = WasSkipped ? Confidence.Skipped : _summary.Confidence,
            };
        }
    }
}

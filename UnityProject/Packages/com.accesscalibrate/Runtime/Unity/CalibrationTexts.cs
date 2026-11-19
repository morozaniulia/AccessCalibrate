// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

namespace AccessCalibration
{
    /// <summary>
    /// Every text the player sees, in one place.
    ///
    /// Wording is neutral on purpose: no test can be "failed", nothing labels the player,
    /// every proposal is just a suggestion.
    ///
    /// <para><b>Translating.</b> All fields are plain static strings. Assign your own values once
    /// at startup, before the calibration runs — for example from your localization system:</para>
    /// <code>
    /// CalibrationTexts.ReactionTitle = "Reacție";
    /// CalibrationTexts.ButtonJump    = "Sărit (A / Spațiu)";
    /// </code>
    /// Strings containing {0}, {1} are passed to string.Format; keep the placeholders.
    /// Button names shown in the button-access test come from <see cref="ButtonJump"/> etc. when
    /// the default buttons are used; games that set their own <c>CalibrationRunner.Buttons</c>
    /// provide their own (already translated) names.
    /// </summary>
    public static class CalibrationTexts
    {
        // ------------------------------------------------------------- general
        public static string IntroPressToStart = "Press any button when you're ready";
        public static string WaitingForInput = "Waiting for input";
        public static string SkipHint = "Skip";
        public static string SkipCheckHint = "Skip this check";
        public static string MoveHint = "Move";
        public static string SkipButton = "Skip";
        public static string TestCounter = "Test {0} of {1}";
        /// <summary>Chip in the top bar of a running check: "01 / 05".</summary>
        public static string Chip = "{0:00} / {1:00}";
        public static string StepOf = "Step {0} of {1}";
        /// <summary>A button is still held when a test starts (e.g. from the intro card).</summary>
        public static string LetGoToBegin = "Let go of the button to begin.";

        // ------------------------------------------------------------- welcome + rail
        public static string RailEyebrow = "CONTROL SETUP";
        public static string RailNoteWelcome = "About 1 minute. You can redo it anytime from Options.";
        public static string RailNoteIntro = "There's no right or wrong result.";
        public static string WelcomeTitle = "Set up the controls around how you play";
        public static string WelcomeBody = "Five quick checks, no score. We'll suggest settings, and you choose which ones to keep.";
        public static string WelcomeStart = "Start";
        public static string WelcomeNotNow = "Not now";
        public static string WelcomeNotNowNote = "Uses default settings";

        // ------------------------------------------------------------- 1. reaction
        public static string ReactionTitle = "Reaction";
        public static string ReactionInstructions = "When the circle appears and you hear the sound, press any button.";
        public static string ReactionWait = "Wait for the signal…";
        public static string ReactionTrial = "Try {0} of {1}";
        public static string ReactionTooEarly = "A little early — no problem, let's continue.";
        public static string ReactionRetry = "Let's do one more short round.";
        public static string ReactionLetGo = "Let go of the button — the next signal comes after that.";
        public static string ReactionSignal = "Press";
        public static string ReactionLineBelow = "Any button, as soon as the circle appears";
        public static string ToneNote = "A soft tone plays with each signal";

        // ------------------------------------------------------------- 2. hold
        public static string HoldTitle = "Hold";
        public static string HoldInstructions = "Hold a button for as long as is comfortable, then let go. 5 seconds at most.";
        public static string HoldWaiting = "Press and hold whenever you like.";
        public static string HoldHolding = "Holding… let go whenever you like.";
        public static string HoldMaxReached = "That's it, you can let go.";
        public static string HoldTrial = "Try {0} of {1}";
        public static string HoldLetGo = "Let go, then press and hold.";
        public static string HoldSecondsOf = "seconds of {0:0}";
        public static string HoldKeepHolding = "Keep holding";
        public static string HoldLetGoWhen = "Let go when it stops feeling comfortable";

        // ------------------------------------------------------------- 3. repeated presses
        public static string TapsTitle = "Repeated presses";
        public static string TapsInstructions = "Press a button as many times as is comfortable, for 5 seconds. The timer starts at your first press.";
        public static string TapsWaiting = "Start whenever you like.";
        public static string TapsCounting = "Presses: {0}";
        public static string TapsPresses = "presses";
        public static string SecondsLeft = "{0:0.0} s left";

        // ------------------------------------------------------------- 4. stability
        public static string StabilityTitle = "Steadiness";
        public static string StabilityInstructionsStick = "Use the left stick to move the dot into the circle, then keep it there as still as is comfortable.";
        public static string StabilityInstructionsMouse = "Move the cursor into the circle, then keep it there as still as is comfortable.";
        public static string StabilityBringIn = "Bring the dot into the circle.";
        public static string StabilityGetReady = "Good — get settled…";
        public static string StabilityMeasuring = "Measuring";
        public static string StabilityHeadline = "Keep the dot in the circle";
        public static string StabilityBody = "Hold it as still as is comfortable. It doesn't need to be centred: we only look at how much it moves.";
        public static string DeviceLeftStick = "Left stick";
        public static string DeviceMouse = "Mouse";

        // ------------------------------------------------------------- 5. button access
        public static string ButtonsTitle = "Button reach";
        public static string ButtonsInstructions = "Press the button shown. If one is hard to reach, just wait — we'll move on by ourselves.";
        public static string ButtonsPrompt = "Press: {0}";
        public static string ButtonsSub = "Press it if you can reach it — or just wait";
        public static string ButtonsCount = "{0} of {1}";
        public static string ButtonJump = "Jump (A / Space)";
        public static string ButtonAction = "Action (X / E)";
        public static string ButtonLeftBumper = "Left bumper (LB / Q)";
        public static string ButtonRightBumper = "Right bumper (RB / R)";

        // ------------------------------------------------------------- confirmation screen
        public static string ConfirmEyebrow = "CONTROL SETUP · DONE";
        public static string ConfirmTitle = "Here's what we suggest";
        public static string ConfirmNote = "Keep what helps. Change it anytime in Options.";
        public static string ConfirmNothing = "No changes suggested. You can still adjust everything in Options.";
        /// <summary>Primary button, live: N switched on / 1 / none.</summary>
        public static string ConfirmApplyMany = "Apply {0} settings";
        public static string ConfirmApplyOne = "Apply 1 setting";
        public static string ConfirmApplyNone = "Continue without changes";
        public static string ConfirmRedo = "Redo setup";
        public static string ConfirmSkip = "Not now";
        public static string ToggleHint = "Turn on / off";
        /// <summary>Appended to the measurement of an uncertain proposal (those start switched off).</summary>
        public static string ConfirmUncertain = "not measured reliably, so it starts off";

        // One title per setting, in plain words.
        public static string SettingTimingMultiplier = "More time for quick moments";
        public static string SettingQteSkippable = "Quick-time events can be skipped";
        public static string SettingHoldToToggle = "Press once instead of holding";
        public static string SettingMashingHoldInstead = "Hold instead of pressing rapidly";
        public static string SettingStickDeadzone = "Ignore small stick movements";
        public static string SettingAimAssist = "Aim assist";
        public static string SettingRemap = "Remap hard-to-reach buttons";
        public static string SettingOneHanded = "One-handed control layout";
        public static string SettingMouseSmoothing = "Smooth small mouse movements";

        // Why: "<measurement> · <rule> → <effect>". {0} = measured value, {1} = rule threshold.
        public static string MeasureReaction = "Median reaction {0:0} ms";
        public static string MeasureHold = "Longest hold {0:0.0} s";
        public static string MeasureTaps = "{0:0.0} presses per second";
        public static string MeasureStability = "Cursor spread {0:0.00} of the circle";
        /// <summary>{0} = buttons not reached, {1} = buttons asked for.</summary>
        public static string MeasureButtonsMissed = "{0:0} of {1:0} buttons not reached in time";
        public static string MeasureButtonsHalf = "Half of the buttons not reached in time";
        public static string RuleAboveMs = "rule: above {0:0} ms";
        public static string RuleBelowSeconds = "rule: below {0:0.#} s";
        public static string RuleBelowPerSecond = "rule: below {0:0.#} per second";
        public static string RuleAbove = "rule: above {0:0.00}";
        public static string EffectTiming = "timing windows ×{0}";
        public static string EffectDeadzone = "deadzone {0}";
        public static string EffectAimAssist = "{0}";
        public static string EffectRemap = "{0}";
    }
}

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
        public static string IntroPressToStart = "Press any button when you're ready.";
        public static string SkipHint = "You can skip at any time (Esc / Select).";
        public static string SkipButton = "Skip";
        public static string TestCounter = "Test {0} of {1}";
        /// <summary>A button is still held when a test starts (e.g. from the intro card).</summary>
        public static string LetGoToBegin = "Let go of the button to begin.";

        // ------------------------------------------------------------- 1. reaction
        public static string ReactionTitle = "Reaction";
        public static string ReactionInstructions = "When the circle appears and you hear the sound, press any button.";
        public static string ReactionWait = "Wait for the signal…";
        public static string ReactionTrial = "Try {0} of {1}";
        public static string ReactionTooEarly = "A little early — no problem, let's continue.";
        public static string ReactionRetry = "Let's do one more short round.";
        public static string ReactionLetGo = "Let go of the button — the next signal comes after that.";

        // ------------------------------------------------------------- 2. hold
        public static string HoldTitle = "Hold";
        public static string HoldInstructions = "Hold a button for as long as is comfortable, then let go. 5 seconds at most.";
        public static string HoldWaiting = "Press and hold whenever you like.";
        public static string HoldHolding = "Holding… let go whenever you like.";
        public static string HoldMaxReached = "That's it, you can let go.";
        public static string HoldTrial = "Try {0} of {1}";
        public static string HoldLetGo = "Let go, then press and hold.";

        // ------------------------------------------------------------- 3. repeated presses
        public static string TapsTitle = "Repeated presses";
        public static string TapsInstructions = "Press a button as many times as is comfortable, for 5 seconds. The timer starts at your first press.";
        public static string TapsWaiting = "Start whenever you like.";
        public static string TapsCounting = "Presses: {0}";

        // ------------------------------------------------------------- 4. stability
        public static string StabilityTitle = "Steadiness";
        public static string StabilityInstructionsStick = "Use the left stick to move the dot into the circle, then keep it there as still as is comfortable.";
        public static string StabilityInstructionsMouse = "Move the cursor into the circle, then keep it there as still as is comfortable.";
        public static string StabilityBringIn = "Bring the dot into the circle.";
        public static string StabilityGetReady = "Good — get settled…";
        public static string StabilityMeasuring = "Keep it there…";

        // ------------------------------------------------------------- 5. button access
        public static string ButtonsTitle = "Button reach";
        public static string ButtonsInstructions = "Press the button shown. If one is hard to reach, just wait — we'll move on by ourselves.";
        public static string ButtonsPrompt = "Press: {0}";
        public static string ButtonJump = "Jump (A / Space)";
        public static string ButtonAction = "Action (X / E)";
        public static string ButtonLeftBumper = "Left bumper (LB / Q)";
        public static string ButtonRightBumper = "Right bumper (RB / R)";

        // ------------------------------------------------------------- confirmation screen
        public static string ConfirmTitle = "Here's what we suggest";
        public static string ConfirmIntro = "Turn on what you want. You can change these settings any time from the options menu.";
        public static string ConfirmNothing = "No changes suggested. You can still adjust everything from the options menu.";
        public static string ConfirmAccept = "Accept";
        public static string ConfirmRedo = "Redo calibration";
        public static string ConfirmSkip = "Skip";
        /// <summary>Shown under a proposal whose measurement was uncertain (those start switched off).</summary>
        public static string ConfirmUncertain = "Check this: we couldn't measure this reliably.";

        // One line per setting. {0} = proposed value.
        public static string SettingTimingMultiplier = "More time for quick moments (timing windows ×{0})";
        public static string SettingQteSkippable = "Quick-time events can be skipped";
        public static string SettingHoldToToggle = "Press once instead of holding";
        public static string SettingMashingHoldInstead = "Hold a button instead of pressing it rapidly";
        public static string SettingStickDeadzone = "Ignore small stick movements (deadzone {0})";
        public static string SettingAimAssist = "Aim assist: {0}";
        public static string SettingRemap = "Suggest remapping: {0}";
        public static string SettingOneHanded = "Suggest a one-handed control layout";

        // Why each setting was suggested. {0} = measured value, {1} = rule threshold.
        public static string ReasonReaction = "Median reaction time {0:0} ms (rule: above {1:0} ms)";
        public static string ReasonHold = "Longest comfortable hold {0:0.0} s (rule: below {1:0.0} s)";
        public static string ReasonTaps = "{0:0.0} presses per second (rule: below {1:0.0})";
        public static string ReasonStability = "Cursor spread {0:0.00} of the circle radius (rule: above {1:0.00})";
        public static string ReasonButtonsMissed = "{0:0} button(s) not pressed in time";
        /// <summary>{0} = buttons not pressed, {1} = buttons asked for.</summary>
        public static string ReasonButtonsHalf = "{0:0} of {1:0} buttons not pressed in time (rule: at least half)";
    }
}

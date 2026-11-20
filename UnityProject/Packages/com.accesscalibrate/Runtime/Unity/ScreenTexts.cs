// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Globalization;
using AccessCalibration.Core;

namespace AccessCalibration
{
    /// <summary>
    /// Every text of the built-in screens (UI v2, Docs/design-v2). Change these to localise.
    /// Texts shown on several lines are arrays, one entry per line: the line breaks are chosen by
    /// hand (no line ends on a single short word) and the screens keep them as they are.
    /// " " is a non-breaking space, "‑" a non-breaking hyphen.
    /// </summary>
    public static class ScreenTexts
    {
        // ------------------------------------------------------------ shared
        public static string TopTitle = "Control setup";
        public static string Change = "Change";
        public static string NotNow = "Not now";
        public static string YourOptions = "Your options";
        public static string SkipCheck = "Skip this check";
        public static string Back = "Back";
        public static string SkipAll = "Skip the whole setup";
        public static string HoldWord = "hold";
        public static string[] SkipNote = { "Skipping is fine. We won't suggest", "anything for a skipped check." };
        public static string WhatWeMeasure = "What we measure";
        public static string ButtonsMatch = "Buttons on screen match your device";
        public static string ReplayIntro = "Replay the intro (R)";
        public static string TryOf = "Try {0} of {1}";
        public static string SecondsLeft = "{0:0.0} s left";

        // ------------------------------------------------------------ bottom-bar hints
        public static string HintSelect = "Select";
        public static string HintConfirm = "Confirm";
        public static string HintCancel = "Cancel";
        public static string HintToggle = "Turn on / off";
        public static string HintAdjust = "Adjust";
        public static string HintSkipCheck = "Skip this check";
        public static string HintBack = "Back";
        public static string HintSkipAllHold = "Skip the whole setup (hold)";
        public static string HintSkipAllKeys = "Skip the whole setup";

        // ------------------------------------------------------------ 01 welcome
        public static string[] WelcomeTitle = { "Set up the controls", "around how you play" };
        public static string[] WelcomeSub = { "Five short checks, about a minute. No score. We suggest settings", "and you choose which ones to keep." };
        public static string WelcomePlayingWith = "Playing with";
        public static string WelcomeDetected = "· detected from your last input.";
        public static string WelcomeStart = "Start";
        public static string WelcomeNote = "About 1 minute · any check can be skipped · nothing leaves your device";
        /// <summary>The five checks, as the Welcome steps show them (one entry per line).</summary>
        public static string[][] CheckNames =
        {
            new[] { "Reaction" }, new[] { "Hold" }, new[] { "Repeated", "presses" }, new[] { "Steadiness" }, new[] { "Button reach" },
        };

        // ------------------------------------------------------------ 02 device picker
        public static string DeviceTitle = "What are you playing with?";
        public static string[] DeviceText = { "We picked this from the last button you pressed. The buttons", "shown on screen and the rules we use will match your choice." };
        public static string DeviceXbox = "Xbox controller";
        public static string DevicePlayStation = "PlayStation controller";
        public static string DeviceKeyboard = "Keyboard & mouse";
        public static string DeviceTouch = "Touch";
        public static string DeviceNintendo = "Switch controller";
        public static string DeviceXboxDetail = "A B X Y buttons";
        public static string DevicePlayStationDetail = "Cross, circle, square, triangle buttons";
        public static string DeviceKeyboardDetail = "Keys and mouse movement";
        public static string DeviceTouchDetail = "Taps and swipes on screen";
        public static string DeviceDetected = "Detected · {0}";
        public static string DeviceSelected = "Selected";
        public static string DeviceUse = "Use this device";
        public static string DeviceNote = "The current check restarts with the new device";

        // ------------------------------------------------------------ 03 / 04 reaction
        public static string ReactionTitle = "Reaction";
        public static string[] ReactionInstruction = { "Press any button as soon", "as the circle lights up." };
        public static string[] ReactionMeasure = { "How fast you respond.", "Six tries, one warm-up." };
        public static string ReactionTipTitle = "Sound";
        public static string[] ReactionTip = { "Each signal also plays a soft tone,", "so you can react to sound." };
        public static string ReactionPlayTone = "Play the tone";
        public static string ReactionWait = "Wait for the signal";
        public static string ReactionGetReady = "Get ready";
        public static string ReactionSignal = "Press";
        public static string ReactionPress = "Press any button";

        // ------------------------------------------------------------ 05 / 06 hold
        public static string HoldTitle = "Hold";
        public static string[] HoldInstruction = { "Hold the button for as long", "as it feels comfortable." };
        public static string[] HoldInstructionKey = { "Hold the key for as long", "as it feels comfortable." };
        public static string[] HoldMeasure = { "Your longest comfortable hold,", "up to 5 seconds. Two tries." };
        public static string HoldTipTitle = "Grip";
        public static string[] HoldTip = { "Use the grip you normally play with.", "Any length is a valid answer." };
        public static string HoldReady = "Press and hold {0} to begin";
        public static string HoldKeep = "Keep holding {0}";
        public static string HoldLetGo = "Let go when it stops feeling comfortable";
        public static string HoldSecondsOf = "seconds of {0:0}";
        public static string HoldCallout = "Hold {0}";

        // ------------------------------------------------------------ 07 / 08 repeated presses
        public static string PressesTitle = "Repeated presses";
        public static string[] PressesInstruction = { "Press the button as fast", "as you comfortably can." };
        public static string[] PressesInstructionKey = { "Press the key as fast", "as you comfortably can." };
        public static string[] PressesMeasure = { "Presses per second for 5 s,", "from your first press." };
        public static string PressesTipTitle = "Pace";
        public static string[] PressesTip = { "Comfort matters more than speed.", "You can stop at any time." };
        public static string PressesReady = "Press {0} to start";
        public static string PressesReadySub = "5 seconds, from your first press";
        public static string PressesKeepGoing = "Keep going";
        public static string PressesUnit = "presses";
        public static string PressesCallout = "Press {0}";

        // ------------------------------------------------------------ 09 / 10 steadiness
        public static string SteadinessTitle = "Steadiness";
        public static string[] SteadinessInstructionStick = { "Use the left stick to bring", "the dot into the circle." };
        public static string[] SteadinessInstructionMouse = { "Use the mouse to bring", "the dot into the circle." };
        public static string[] SteadinessMeasure = { "How much the dot drifts.", "It doesn't need to be centred." };
        public static string SteadinessTipTitle = "Hand position";
        public static string[] SteadinessTip = { "Rest your hand the way you do", "when you play, then keep still." };
        public static string SteadinessReady = "Move the dot into the circle";
        public static string SteadinessReadySub = "Measuring starts once it is inside";
        public static string SteadinessKeep = "Keep the dot in the circle";
        public static string SteadinessKeepSub = "Hold it as still as is comfortable";
        public static string SteadinessMeasuring = "Measuring";

        // ------------------------------------------------------------ 11 button reach
        public static string ReachTitle = "Button reach";
        public static string[] ReachInstruction = { "Press each button when", "it lights up, 5 seconds each." };
        public static string ReachReached = "Reached";
        public static string ReachPressNow = "Press now";
        public static string ReachNext = "Next";
        public static string ReachMissed = "Not reached";
        public static string ReachTime = "{0:0.0} s";
        public static string ReachCount = "Button {0} of {1}";
        public static string ReachSkipButton = "Skip this button";
        public static string ReachWait = "wait {0:0} s";
        public static string ReachTipTitle = "Remapping tip";
        public static string[] ReachTip = { "Missing a button isn't a mistake.", "We'll suggest moving its action", "to an easier one." };
        public static string ReachCallout = "Press {0}";

        // ------------------------------------------------------------ 12 suggestions
        public static string SuggestTitle = "Here's what we suggest";
        public static string SuggestSub = "Keep what helps. Change it anytime in Options.";
        public static string SuggestCount = "{0} suggestions · {1} on";
        public static string SuggestCountOne = "1 suggestion · {0} on";
        public static string Why = "Why";
        public static string InTheGame = "In the game";
        public static string Default = "Default";
        public static string WithThis = "With this";
        public static string Off = "Off";
        public static string On = "On";
        public static string Advanced = "Advanced · adjust";
        public static string Suggested = "{0} suggested";
        public static string ApplyMany = "Apply {0} settings";
        public static string ApplyOne = "Apply 1 setting";
        public static string ApplyNone = "Continue without changes";
        public static string Redo = "Redo setup";
        public static string RowOffByPlayer = "Off · you turned it off";
        public static string RowOffUncertain = "Off · not measured reliably";

        // ------------------------------------------------------------ 13 no changes
        public static string NoChangesTitle = "No changes needed";
        public static string[] NoChangesText = { "Every check came back within the default range, so the game keeps", "its standard settings. You can still adjust anything in Options." };
        public static string[] NoChangesChecks = { "Reaction", "Hold", "Repeated presses", "Steadiness", "Button reach" };
        public static string Continue = "Continue";
        public static string NoChangesNote = "Your results stay on this device";

        // ------------------------------------------------------------ 14 summary
        public static string SummaryTitle = "Your results";
        public static string SummarySub = "What each check measured, the rule it was compared with, and what changed.";
        public static string SummaryApplied = "{0} of {1} suggestions applied";
        public static string SummaryColCheck = "Check";
        public static string SummaryColResult = "Your result · marker shows the rule";
        public static string SummaryColSetting = "Setting";
        public static string StatusApplied = "Applied";
        public static string StatusNotApplied = "Not applied";
        public static string StatusNoChange = "No change";
        public static string StatusSkipped = "Skipped";
        public static string SummaryContinue = "Continue to game";
        public static string SummaryCopy = "Copy results";
        public static string SummaryRedo = "Redo";
        public static string SummaryNote = "Copy results puts a plain-text summary on your clipboard. Nothing is sent anywhere.";
        public static string[] SummaryMeasure = { "median of {0} tries", "longest comfortable hold", "presses per second", "spread, share of the circle", "buttons not reached in time" };
        public static string SummaryReaction = "{0:0} ms";
        public static string SummaryHold = "{0:0.0} s";
        public static string SummaryTaps = "{0:0.0} / s";
        public static string SummaryStability = "{0:0.00}";
        public static string SummaryButtons = "{0:0} of {1:0}";
        public static string RuleAboveMs = "rule: above {0:0} ms";
        public static string RuleBelowSeconds = "rule: below {0:0.#} s";
        public static string RuleBelowPerSecond = "rule: below {0:0.#} / s";
        public static string RuleAbove = "rule: above {0:0.00}";
        public static string RuleButtons = "rule: {0:0} or more";

        // ------------------------------------------------------------ per suggestion

        /// <summary>Second line of a suggestion row, e.g. "Timing windows ×1.5".</summary>
        public static string RowDetail(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "timingMultiplier": return F("Timing windows ×{0}", c.ProposedValue);
                case "qteMode": return "Quick-time events get a skip button";
                case "holdToToggle": return "Hold-to-toggle";
                case "mashingMode": return "Mashing becomes a hold";
                case "stickDeadzone": return F("Stick deadzone {0}", c.ProposedValue);
                case "aimAssist": return F("Aim assist {0}", c.ProposedValue.ToLowerInvariant());
                case "remapSuggestions": return RemapDetail(c.ProposedValue);
                case "suggestOneHandedLayout": return "One-handed layout";
                case "mouseSmoothing": return "Mouse smoothing";
                default: return c.ProposedValue;
            }
        }

        static string RemapDetail(string buttons) =>
            buttons.Contains(",") ? F("{0} move to easier buttons", buttons) : F("{0} moves to an easier button", buttons);

        /// <summary>"Why" in the detail panel: the measurement, then the rule (two lines).</summary>
        public static string[] WhyLines(ProposedChange c)
        {
            string second = c.IsUncertain ? "Not measured reliably." : null;
            switch (c.Source)
            {
                case MeasurementKind.Reaction: return new[] { F("Median reaction {0:0} ms.", c.MeasuredValue), second ?? F("Suggested above {0:0} ms.", c.Threshold) };
                case MeasurementKind.Hold: return new[] { F("Longest hold {0:0.0} s.", c.MeasuredValue), second ?? F("Suggested below {0:0.#} s.", c.Threshold) };
                case MeasurementKind.Taps: return new[] { F("{0:0.0} presses per second.", c.MeasuredValue), second ?? F("Suggested below {0:0.#} per second.", c.Threshold) };
                case MeasurementKind.Stability: return new[] { F("Spread {0:0.00} of the circle.", c.MeasuredValue), second ?? F("Suggested above {0:0.00}.", c.Threshold) };
                case MeasurementKind.Buttons:
                    return c.Setting == "suggestOneHandedLayout"
                        ? new[] { "Half of the buttons", "not reached in time." }
                        : new[] { F("{0:0} of {1:0} buttons", c.MeasuredValue, c.Total), "not reached in time." };
                default: return new[] { c.Summary };
            }
        }

        /// <summary>"In the game" in the detail panel (two lines).</summary>
        public static string[] InGameLines(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "timingMultiplier": return new[] { "More time to press during", "quick‑time prompts." };
                case "qteMode": return new[] { "Quick-time events show", "a skip button." };
                case "holdToToggle": return new[] { "Press once to start a hold,", "press again to stop." };
                case "mashingMode": return new[] { "Hold the button instead", "of pressing it rapidly." };
                case "stickDeadzone": return new[] { "Small stick movements", "are ignored." };
                case "aimAssist": return new[] { "The aim moves a little", "towards targets." };
                case "remapSuggestions": return new[] { F("{0} moves to a button", c.ProposedValue), "that is easier to reach." };
                case "suggestOneHandedLayout": return new[] { "Controls are grouped", "for one hand." };
                case "mouseSmoothing": return new[] { "Small mouse movements", "are smoothed." };
                default: return new[] { "" };
            }
        }

        /// <summary>The setting as the Summary names it, e.g. "Remap RB to an easier button".</summary>
        public static string SummarySetting(ProposedChange c)
        {
            switch (c.Setting)
            {
                case "mouseSmoothing": return "Smooth small movements";
                case "remapSuggestions": return F("Remap {0} to an easier button", c.ProposedValue);
                default: return ProposalTexts.Title(c);
            }
        }

        static string F(string pattern, params object[] args) => string.Format(CultureInfo.InvariantCulture, pattern, args);
    }
}

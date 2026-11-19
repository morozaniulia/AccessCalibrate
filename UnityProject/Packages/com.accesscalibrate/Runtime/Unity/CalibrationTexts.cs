// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

namespace AccessCalibration
{
    /// <summary>
    /// Every text the calibration shows, in one place so a game can translate it.
    /// Wording is neutral on purpose: no test can be "failed", nothing labels the player.
    /// </summary>
    public static class CalibrationTexts
    {
        public static string IntroPressToStart = "Apasă orice buton când ești gata.";
        public static string SkipHint = "Poți sări peste oricând (Esc / Select).";
        public static string SkipButton = "Sari peste";
        public static string TestCounter = "Testul {0} din {1}";

        public static string ReactionTitle = "Reacție";
        public static string ReactionInstructions = "Când apare cercul și auzi sunetul, apasă orice buton.";
        public static string ReactionWait = "Așteaptă semnalul…";
        public static string ReactionTrial = "Încercarea {0} din {1}";
        public static string ReactionTooEarly = "Puțin prea devreme — nicio problemă, continuăm.";
        public static string ReactionRetry = "Mai facem o rundă scurtă.";

        public static string HoldTitle = "Ținut apăsat";
        public static string HoldInstructions = "Ține apăsat un buton cât îți e comod, apoi dă-i drumul. Cel mult 5 secunde.";
        public static string HoldWaiting = "Apasă și ține când vrei.";
        public static string HoldHolding = "Ții apăsat… dă-i drumul când vrei.";
        public static string HoldMaxReached = "Gata, poți da drumul.";
        public static string HoldTrial = "Încercarea {0} din {1}";

        public static string TapsTitle = "Apăsări repetate";
        public static string TapsInstructions = "Apasă un buton de câte ori îți e comod, timp de 5 secunde. Timpul pornește la prima apăsare.";
        public static string TapsWaiting = "Începe când vrei.";
        public static string TapsCounting = "Apăsări: {0}";

        public static string StabilityTitle = "Stabilitate";
        public static string StabilityInstructionsStick = "Mută punctul în cerc cu stick-ul stâng și ține-l acolo cât poți de liniștit.";
        public static string StabilityInstructionsMouse = "Ține cursorul în cerc cât poți de liniștit.";
        public static string StabilityGetReady = "Pregătește-te…";
        public static string StabilityMeasuring = "Ține-l acolo…";

        public static string ButtonsTitle = "Acces la butoane";
        public static string ButtonsInstructions = "Apasă butonul afișat. Dacă unul e greu de ajuns, așteaptă — trecem singuri mai departe.";
        public static string ButtonsPrompt = "Apasă: {0}";

        public static string SummaryTitle = "Gata! Iată ce îți propunem:";
        public static string SummaryNothing = "Nu propunem nicio schimbare. Poți ajusta oricând setările din opțiuni.";
    }
}

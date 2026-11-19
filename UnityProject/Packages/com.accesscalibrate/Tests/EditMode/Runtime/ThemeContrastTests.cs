// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AccessCalibration.Tests
{
    /// <summary>
    /// Every theme keeps text readable: WCAG contrast ≥ 4.5:1 for text on the panel and for
    /// --ac-on-focus on --ac-focus. The panel is translucent, so each check is made over three
    /// possible games behind it (black, white, mid grey) and the worst case must pass.
    /// Tokens are read straight from the USS files, so a theme edit that breaks contrast fails here.
    /// </summary>
    public class ThemeContrastTests
    {
        const string Folder = "Packages/com.accesscalibrate/Runtime/UI/Resources/AccessCalibrate/";

        static Dictionary<string, string> Tokens(params string[] files)
        {
            var tokens = new Dictionary<string, string>();
            foreach (var f in files)
                foreach (Match m in Regex.Matches(File.ReadAllText(Folder + f), @"(--ac-[a-z0-9-]+)\s*:\s*([^;]+);"))
                    tokens[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            return tokens;
        }

        struct Rgba { public double R, G, B, A; }

        static Rgba Parse(string v)
        {
            v = v.Trim();
            if (v.StartsWith("#"))
            {
                int hex = int.Parse(v.Substring(1), NumberStyles.HexNumber);
                return new Rgba { R = (hex >> 16) & 255, G = (hex >> 8) & 255, B = hex & 255, A = 1 };
            }
            var m = Regex.Match(v, @"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+))?\s*\)");
            if (!m.Success) throw new FormatException("Not a colour: " + v);
            double P(int i) => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
            return new Rgba { R = P(1), G = P(2), B = P(3), A = m.Groups[4].Success ? P(4) : 1 };
        }

        static Rgba Over(Rgba top, Rgba under) => new Rgba
        {
            R = top.R * top.A + under.R * (1 - top.A),
            G = top.G * top.A + under.G * (1 - top.A),
            B = top.B * top.A + under.B * (1 - top.A),
            A = 1,
        };

        static double Luminance(Rgba c)
        {
            double Ch(double v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        static double Contrast(Rgba a, Rgba b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static readonly Rgba[] Games =
        {
            new Rgba { R = 0, G = 0, B = 0, A = 1 },
            new Rgba { R = 255, G = 255, B = 255, A = 1 },
            new Rgba { R = 128, G = 128, B = 128, A = 1 },
        };

        static IEnumerable<TestCaseData> Themes()
        {
            yield return new TestCaseData((object)new[] { "CalibrationViewStyle.uss" }).SetName("GlassDark");
            yield return new TestCaseData((object)new[] { "CalibrationViewStyle.uss", "Themes/GlassLight.uss" }).SetName("GlassLight");
            yield return new TestCaseData((object)new[] { "CalibrationViewStyle.uss", "Themes/HighContrast.uss" }).SetName("HighContrast");
        }

        [TestCaseSource(nameof(Themes))]
        public void TextOnPanel_AndOnFocus_AreAtLeast4_5To1(string[] files)
        {
            var t = Tokens(files);
            var failures = new List<string>();
            foreach (var game in Games)
            {
                var panel = Over(Parse(t["--ac-panel"]), Over(Parse(t["--ac-overlay"]), game));
                var row = Over(Parse(t["--ac-surface-row"]), panel);
                var focusRow = Over(Parse(t["--ac-surface-focus"]), panel);

                void Check(string fg, Rgba bg, string bgName)
                {
                    var c = Over(Parse(t[fg]), bg);
                    double ratio = Contrast(c, bg);
                    if (ratio < 4.5) failures.Add($"{fg} on {bgName} over game {game.R}: {ratio:0.00}:1");
                }

                foreach (var fg in new[] { "--ac-text", "--ac-text-secondary", "--ac-text-muted", "--ac-text-hint" })
                    Check(fg, panel, "panel");
                Check("--ac-text", row, "row");
                Check("--ac-text-reason", row, "row");
                Check("--ac-text", focusRow, "focused row");
                Check("--ac-on-focus", Over(Parse(t["--ac-focus"]), panel), "--ac-focus");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EveryColourInTheMainStylesheet_IsAToken()
        {
            // Outside the token block, colours must be var(--ac-…), never literals.
            string uss = File.ReadAllText(Folder + "CalibrationViewStyle.uss");
            var offenders = new List<string>();
            foreach (var line in uss.Split('\n'))
            {
                string l = line.Trim();
                if (l.StartsWith("--ac-") || l.StartsWith("/*") || l.StartsWith("*")) continue;
                if (Regex.IsMatch(l, @"(#[0-9A-Fa-f]{3,8}\b|rgba?\()")) offenders.Add(l);
            }
            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }
    }
}

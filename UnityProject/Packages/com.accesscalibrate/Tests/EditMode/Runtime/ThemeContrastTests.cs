// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AccessCalibration.Tests
{
    /// <summary>
    /// Every theme keeps text readable: WCAG contrast ≥ 4.5:1 for each text colour on each surface
    /// it can sit on (the background, cards, buttons, sunken boxes), and for the text on the accent and
    /// on the primary button. The background is opaque: the gradient ends, and for a theme with an
    /// image (Dark: the galaxy) its brightest pixel under --ac-bg-overlay, the worst case for light text.
    /// Tokens are read straight from the USS files, so a theme edit that breaks contrast fails here.
    /// </summary>
    public class ThemeContrastTests
    {
        const string Folder = "Packages/com.accesscalibrate/Runtime/UI/Resources/AccessCalibrate/";
        const string MainSheet = "CalibrationViewStyle.uss";

        static Dictionary<string, string> Tokens(params string[] files)
        {
            var tokens = new Dictionary<string, string>();
            foreach (var f in files)
                foreach (Match m in Regex.Matches(File.ReadAllText(Folder + f), @"(--ac-[a-z0-9-]+)\s*:\s*([^;]+);"))
                    tokens[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            return tokens;
        }

        struct Rgba { public double R, G, B, A; public override string ToString() => $"({R:0},{G:0},{B:0})"; }

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

        // Unity blends in linear space in Linear-colour-space projects (browsers in gamma space):
        // a translucent surface is checked both ways and the worse result counts.
        static double ToLinear(double v) { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        static double ToGamma(double v) => 255 * (v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055);
        static Rgba OverLinear(Rgba top, Rgba under) => new Rgba
        {
            R = ToGamma(ToLinear(top.R) * top.A + ToLinear(under.R) * (1 - top.A)),
            G = ToGamma(ToLinear(top.G) * top.A + ToLinear(under.G) * (1 - top.A)),
            B = ToGamma(ToLinear(top.B) * top.A + ToLinear(under.B) * (1 - top.A)),
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

        /// <summary>The brightest pixel of a background image (by luminance).</summary>
        static Rgba Brightest(string imagePath)
        {
            var tex = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(imagePath)), "cannot read " + imagePath);
                Rgba best = default; double bestL = -1;
                foreach (var p in tex.GetPixels32())
                {
                    var c = new Rgba { R = p.r, G = p.g, B = p.b, A = 1 };
                    double l = Luminance(c);
                    if (l > bestL) { bestL = l; best = c; }
                }
                return best;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static IEnumerable<TestCaseData> Themes()
        {
            yield return new TestCaseData((object)new[] { MainSheet }).SetName("Dark");
            yield return new TestCaseData((object)new[] { MainSheet, "Themes/Light.uss" }).SetName("Light");
            yield return new TestCaseData((object)new[] { MainSheet, "Themes/HighContrast.uss" }).SetName("HighContrast");
        }

        static readonly string[] TextTokens =
            { "--ac-text", "--ac-text-body", "--ac-text-soft", "--ac-text-dim", "--ac-text-muted", "--ac-text-faint", "--ac-accent" };
        static readonly string[] Surfaces = { "--ac-surface-card", "--ac-surface-control", "--ac-surface-sunken" };

        [TestCaseSource(nameof(Themes))]
        public void EveryTextColour_OnEverySurface_IsAtLeast4_5To1(string[] files)
        {
            var t = Tokens(files);
            var backgrounds = new List<(string name, Rgba colour)>
            {
                ("--ac-bg-top", Parse(t["--ac-bg-top"])),
                ("--ac-bg-bottom", Parse(t["--ac-bg-bottom"])),
            };
            var image = Regex.Match(t["--ac-bg-image"], @"url\(\s*""?([^"")]+)""?\s*\)");
            if (image.Success)
            {
                var px = Brightest(Folder + image.Groups[1].Value);
                backgrounds.Add(($"brightest pixel {px} of {image.Groups[1].Value}", Over(Parse(t["--ac-bg-overlay"]), px)));
            }

            var failures = new List<string>();
            void Check(string fg, Rgba bg, string bgName)
            {
                var c = Over(Parse(t[fg]), bg);   // text is opaque (see TextAndLines_AreOpaque)
                double ratio = Contrast(c, bg);
                if (ratio < 4.5) failures.Add($"{fg} on {bgName}: {ratio:0.00}:1");
            }

            foreach (var (bgName, bg) in backgrounds)
            {
                foreach (var fg in TextTokens) Check(fg, bg, bgName);
                foreach (var s in Surfaces)
                {
                    var top = Parse(t[s]);
                    foreach (var (blend, surface) in new[] { ("gamma", Over(top, bg)), ("linear", OverLinear(top, bg)) })
                        foreach (var fg in TextTokens) Check(fg, surface, $"{s} over {bgName} ({blend} blending)");
                }
                Check("--ac-on-accent", Over(Parse(t["--ac-accent"]), bg), "--ac-accent");
                Check("--ac-on-primary", Over(Parse(t["--ac-primary"]), bg), "--ac-primary");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [TestCaseSource(nameof(Themes))]
        public void TextAndLines_AreOpaque(string[] files)
        {
            // Translucent text would render lighter on light backgrounds in Linear-colour-space projects
            // than in the design (and than this test assumes), so text and line colours carry no alpha.
            var t = Tokens(files);
            var translucent = new List<string>();
            foreach (var name in new List<string>(TextTokens) { "--ac-on-accent", "--ac-on-primary", "--ac-line", "--ac-line-strong", "--ac-control-border" })
                if (Parse(t[name]).A < 1) translucent.Add($"{name}: {t[name]}");
            Assert.IsEmpty(translucent, string.Join("\n", translucent));
        }

        [Test]
        public void EachTheme_SetsEveryToken_TheDefaultSets()
        {
            // A theme that forgot a colour would silently show a Dark value: every colour token of the
            // default stylesheet must be set by each theme (shape and font tokens may stay shared).
            var main = Tokens(MainSheet);
            foreach (var theme in new[] { "Themes/Light.uss", "Themes/HighContrast.uss" })
            {
                var t = Tokens(theme);
                var missing = new List<string>();
                foreach (var kv in main)
                {
                    if (kv.Key.StartsWith("--ac-radius") || kv.Key.StartsWith("--ac-font") || kv.Key == "--ac-transparent") continue;
                    if (kv.Value.StartsWith("var(")) continue;   // per-element alias of a token, not a theme token
                    if (!t.ContainsKey(kv.Key)) missing.Add(kv.Key);
                }
                Assert.IsEmpty(missing, theme + " misses: " + string.Join(", ", missing));
            }
        }

        [Test]
        public void EveryColourInTheMainStylesheet_IsAToken()
        {
            // Outside the token block, colours must be var(--ac-…), never literals.
            string uss = File.ReadAllText(Folder + MainSheet);
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

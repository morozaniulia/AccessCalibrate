// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AccessCalibration.UI
{
    /// <summary>
    /// Builds the device drawings of the check screens from the design SVGs: the controller with the
    /// requested button lit and a callout (A B X Y, bumpers, triggers), the buttons already reached,
    /// PlayStation shapes (✕ ○ □ △, drawn — Manrope has no such glyphs) in place of the Xbox letters,
    /// and the ANSI keyboard with any key lit.
    /// </summary>
    internal static class DeviceDrawings
    {
        const string Accent = "#F2EEE6", OnAccent = "#0B0C10";

        struct Face { public string Control, Letter; public int X, Y; public string Line; public int TextX, TextY; public string Anchor; }

        static readonly Face[] Faces =
        {
            new Face { Control = "buttonNorth", Letter = "Y", X = 420, Y = 118, Line = "M446 102 L 486 62 L 580 62", TextX = 580, TextY = 52, Anchor = "end" },
            new Face { Control = "buttonWest", Letter = "X", X = 388, Y = 150, Line = "M364 120 L 334 40 L 230 40", TextX = 230, TextY = 30, Anchor = "start" },
            new Face { Control = "buttonEast", Letter = "B", X = 452, Y = 150, Line = "M490 150 L 580 150", TextX = 580, TextY = 140, Anchor = "end" },
            new Face { Control = "buttonSouth", Letter = "A", X = 420, Y = 182, Line = "M446 204 L 486 244 L 580 244", TextX = 580, TextY = 234, Anchor = "end" },
        };

        struct Shoulder { public string Control, Path, NormalFill, NormalStroke, Line; public int TextX, TextY; public string Anchor; }

        static readonly Shoulder[] Shoulders =
        {
            new Shoulder { Control = "leftTrigger", Path = "M102 112 C 104 76", NormalFill = "#0C1330", NormalStroke = "rgba(255,255,255,0.16)", Line = "M150 58 L 130 26 L 20 26", TextX = 20, TextY = 18, Anchor = "start" },
            new Shoulder { Control = "rightTrigger", Path = "M 498 112 C 496 76", NormalFill = "#0C1330", NormalStroke = "rgba(255,255,255,0.16)", Line = "M450 58 L 470 26 L 580 26", TextX = 580, TextY = 18, Anchor = "end" },
            new Shoulder { Control = "leftShoulder", Path = "M86 110 C 98 84", NormalFill = "#0E1636", NormalStroke = "rgba(255,255,255,0.32)", Line = "M112 86 L 80 44 L 20 44", TextX = 20, TextY = 36, Anchor = "start" },
            new Shoulder { Control = "rightShoulder", Path = "M 514 110 C 502 84", NormalFill = "#0E1636", NormalStroke = "rgba(255,255,255,0.32)", Line = "M488 86 L 520 44 L 580 44", TextX = 580, TextY = 36, Anchor = "end" },
        };

        /// <summary>The control name of a gamepad binding ("&lt;Gamepad&gt;/buttonSouth" → "buttonSouth"), or null.</summary>
        public static string GamepadControl(IEnumerable<string> bindings)
        {
            foreach (var b in bindings)
                if (b != null && b.StartsWith("<Gamepad>/")) return b.Substring("<Gamepad>/".Length);
            return null;
        }

        /// <summary>The keyboard key of a binding ("&lt;Keyboard&gt;/e" → "E", "space" → "Space"), or null.</summary>
        public static string KeyboardKey(IEnumerable<string> bindings)
        {
            foreach (var b in bindings)
            {
                if (b == null || !b.StartsWith("<Keyboard>/")) continue;
                string k = b.Substring("<Keyboard>/".Length);
                return k.Length == 1 ? k.ToUpperInvariant() : char.ToUpperInvariant(k[0]) + k.Substring(1);
            }
            return null;
        }

        /// <summary>Name of a control for a callout: "A" / "Cross", "LB" / "L1"…</summary>
        public static string ControlName(string control, GlyphFamily family)
        {
            bool ps = family == GlyphFamily.PlayStation;
            switch (control)
            {
                case "buttonSouth": return ps ? ScreenTexts.PsCross : "A";
                case "buttonEast": return ps ? ScreenTexts.PsCircle : "B";
                case "buttonWest": return ps ? ScreenTexts.PsSquare : "X";
                case "buttonNorth": return ps ? ScreenTexts.PsTriangle : "Y";
                case "leftShoulder": return ps ? "L1" : "LB";
                case "rightShoulder": return ps ? "R1" : "RB";
                case "leftTrigger": return ps ? "L2" : "LT";
                case "rightTrigger": return ps ? "R2" : "RT";
                default: return control;
            }
        }

        /// <summary>
        /// The controller with <paramref name="lit"/> highlighted and a callout; <paramref name="reached"/>
        /// controls look pressed. Any other control (a stick, the d-pad) gets no highlight.
        /// </summary>
        public static string Controller(string lit, ICollection<string> reached, GlyphFamily family, string callout)
        {
            string src = SvgDrawing.Source("controller-hold-a");
            int cut = src.IndexOf("<circle cx=\"420\" cy=\"150\" r=\"54\"");
            cut = src.IndexOf('\n', cut) + 1;
            var sb = new StringBuilder();
            string head = src.Substring(0, cut);
            head = Regex.Replace(head, "aria-label=\"[^\"]*\"", "aria-label=\"" + Esc("Controller" + (callout != null ? ": " + callout : "")) + "\"");
            foreach (var s in Shoulders)
            {
                bool on = s.Control == lit, done = reached != null && reached.Contains(s.Control);
                if (!on && !done) continue;
                string from = "fill=\"" + s.NormalFill + "\" stroke=\"" + s.NormalStroke + "\"";
                string to = on ? "fill=\"" + Accent + "\" stroke=\"" + Accent + "\"" : "fill=\"rgba(255,255,255,0.16)\" stroke=\"rgba(255,255,255,0.44)\"";
                int at = head.IndexOf("<path d=\"" + s.Path);
                if (at < 0) continue;
                int end = head.IndexOf('>', at);
                head = head.Substring(0, at) + head.Substring(at, end - at).Replace(from, to) + head.Substring(end);
            }
            sb.Append(head);

            Face? litFace = null;
            foreach (var f in Faces)
            {
                if (f.Control == lit) { litFace = f; continue; }
                bool done = reached != null && reached.Contains(f.Control);
                sb.Append(Circle(f.X, f.Y, 15, done ? "rgba(255,255,255,0.16)" : "rgba(255,255,255,0.03)", done ? "rgba(255,255,255,0.44)" : "rgba(255,255,255,0.32)", 1.5f));
                sb.Append(Label(f, family, done ? "rgba(255,255,255,0.90)" : "rgba(255,255,255,0.56)", 12, 700, 1.6f));
                sb.Append('\n');
            }
            if (litFace is Face lf)
            {
                sb.Append($"<circle cx=\"{lf.X}\" cy=\"{lf.Y}\" r=\"38\" fill=\"none\" stroke=\"{Accent}\" stroke-opacity=\"0.22\" stroke-width=\"1.5\"></circle>");
                sb.Append($"<circle cx=\"{lf.X}\" cy=\"{lf.Y}\" r=\"26\" fill=\"{Accent}\" fill-opacity=\"0.16\"></circle>");
                sb.Append($"<circle cx=\"{lf.X}\" cy=\"{lf.Y}\" r=\"15\" fill=\"{Accent}\"></circle>");
                sb.Append(Label(lf, family, OnAccent, 13, 800, 2f));
                sb.Append('\n');
                if (callout != null) sb.Append(CalloutSvg(lf.Line, lf.TextX, lf.TextY, lf.Anchor, callout));
            }
            else if (callout != null)
            {
                foreach (var s in Shoulders)
                    if (s.Control == lit) sb.Append(CalloutSvg(s.Line, s.TextX, s.TextY, s.Anchor, callout));
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }

        /// <summary>A design drawing of the controller with PlayStation shapes in place of A B X Y (others unchanged).</summary>
        public static string WithFamily(string svg, GlyphFamily family)
        {
            if (family != GlyphFamily.PlayStation) return svg;
            return Regex.Replace(svg, "<text x=\"([0-9.]+)\" y=\"([0-9.]+)\" text-anchor=\"middle\" font-family=\"[^\"]*\" font-size=\"(\\d+)\" font-weight=\"(\\d+)\" fill=\"([^\"]*)\">([ABXY])</text>", m =>
            {
                float x = F(m.Groups[1].Value), y = F(m.Groups[2].Value);
                string letter = m.Groups[6].Value;
                foreach (var f in Faces)
                    if (f.Letter == letter) return Shape(f.Control, x, y - 4.5f, m.Groups[5].Value, m.Groups[4].Value == "800" ? 2f : 1.6f);
                return m.Value;
            });
        }

        /// <summary>The ANSI keyboard with <paramref name="key"/> lit (keys as printed: "Space", "E"…) and a callout under it.</summary>
        public static string Keyboard(string key, ICollection<string> reached, string callout)
        {
            string src = SvgDrawing.Source("keyboard-ansi");
            var lines = new List<string>(src.Split('\n'));
            var outLines = new List<string>();
            float calloutX = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string l = lines[i];
                // the design's lit Space key: two rects + a dark label → back to a plain key first
                if (l.StartsWith("<rect x=\"146\" y=\"155\"")) continue;
                if (l.StartsWith("<rect x=\"151\" y=\"160\" width=\"221\"")) l = "<rect x=\"151\" y=\"160\" width=\"221\" height=\"32\" rx=\"6\" fill=\"rgba(255,255,255,0.03)\" stroke=\"rgba(255,255,255,0.30)\" stroke-width=\"1.3\"></rect>";
                if (l.Contains(">Space</text>")) l = l.Replace("font-weight=\"800\" fill=\"#0B0C10\"", "font-weight=\"600\" fill=\"rgba(255,255,255,0.44)\"");
                if (l.StartsWith("<path d=\"M261.5 200")) continue;   // the design's callout
                if (l.StartsWith("<svg")) l = Regex.Replace(l, "aria-label=\"[^\"]*\"", "aria-label=\"" + Esc("Keyboard" + (callout != null ? ": " + callout : "")) + "\"");

                var rect = Regex.Match(l, "^<rect x=\"([0-9.]+)\" y=\"([0-9.]+)\" width=\"([0-9.]+)\" height=\"([0-9.]+)\" rx=\"6\"");
                if (rect.Success && i + 1 < lines.Count)
                {
                    var text = Regex.Match(lines[i + 1], ">([^<]+)</text>");
                    string label = text.Success ? text.Groups[1].Value : null;
                    if (label != null && label == key)
                    {
                        float x = F(rect.Groups[1].Value), y = F(rect.Groups[2].Value), w = F(rect.Groups[3].Value), h = F(rect.Groups[4].Value);
                        outLines.Add($"<rect x=\"{N(x - 5)}\" y=\"{N(y - 5)}\" width=\"{N(w + 10)}\" height=\"{N(h + 10)}\" rx=\"9\" fill=\"{Accent}\" fill-opacity=\"0.16\"></rect>");
                        outLines.Add($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"6\" fill=\"{Accent}\"></rect>");
                        lines[i + 1] = Regex.Replace(lines[i + 1], "font-weight=\"\\d+\" fill=\"[^\"]*\"", "font-weight=\"800\" fill=\"" + OnAccent + "\"");
                        calloutX = x + w / 2;
                        continue;
                    }
                    if (label != null && reached != null && reached.Contains(label))
                    {
                        l = l.Replace("fill=\"rgba(255,255,255,0.03)\" stroke=\"rgba(255,255,255,0.30)\"", "fill=\"rgba(255,255,255,0.16)\" stroke=\"rgba(255,255,255,0.44)\"");
                        lines[i + 1] = lines[i + 1].Replace("fill=\"rgba(255,255,255,0.44)\"", "fill=\"rgba(255,255,255,0.90)\"");
                    }
                }
                if (l.StartsWith("<metadata") || l.StartsWith("</svg"))
                {
                    if (callout != null && calloutX >= 0)
                    {
                        string anchor = calloutX < 80 ? "start" : calloutX > 500 ? "end" : "middle";
                        float tx = anchor == "start" ? calloutX - 16 : anchor == "end" ? calloutX + 16 : calloutX;
                        outLines.Add($"<path d=\"M{N(calloutX)} 210 V 228\" fill=\"none\" stroke=\"{Accent}\" stroke-opacity=\"0.6\" stroke-width=\"1.2\"></path><text x=\"{N(tx)}\" y=\"250\" text-anchor=\"{anchor}\" font-family=\"Manrope, sans-serif\" font-size=\"16\" font-weight=\"700\" fill=\"{Accent}\">{Esc(callout)}</text>");
                        calloutX = -1;
                    }
                }
                outLines.Add(l);
            }
            return string.Join("\n", outLines);
        }

        // ------------------------------------------------------------ pieces

        static string Label(Face f, GlyphFamily family, string fill, int size, int weight, float stroke)
        {
            if (family == GlyphFamily.PlayStation) return Shape(f.Control, f.X, f.Y, fill, stroke);
            return $"<text x=\"{f.X}\" y=\"{N(f.Y + 4.5f)}\" text-anchor=\"middle\" font-family=\"Manrope, sans-serif\" font-size=\"{size}\" font-weight=\"{weight}\" fill=\"{fill}\">{f.Letter}</text>";
        }

        /// <summary>✕ ○ □ △ centred on (x, y), stroked in the label colour.</summary>
        static string Shape(string control, float x, float y, string colour, float width)
        {
            string d;
            switch (control)
            {
                case "buttonSouth": d = $"M{N(x - 4.5f)} {N(y - 4.5f)} L {N(x + 4.5f)} {N(y + 4.5f)} M{N(x + 4.5f)} {N(y - 4.5f)} L {N(x - 4.5f)} {N(y + 4.5f)}"; break;
                case "buttonEast": return $"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"5\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\"></circle>";
                case "buttonWest": return $"<rect x=\"{N(x - 4.5f)}\" y=\"{N(y - 4.5f)}\" width=\"9\" height=\"9\" rx=\"0.5\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\"></rect>";
                default: d = $"M{N(x)} {N(y - 5.5f)} L {N(x + 5.5f)} {N(y + 4)} L {N(x - 5.5f)} {N(y + 4)} Z"; break;
            }
            return $"<path d=\"{d}\" fill=\"none\" stroke=\"{colour}\" stroke-width=\"{N(width)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path>";
        }

        static string Circle(int x, int y, int r, string fill, string stroke, float width) =>
            $"<circle cx=\"{x}\" cy=\"{y}\" r=\"{r}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{N(width)}\"></circle>";

        static string CalloutSvg(string line, int x, int y, string anchor, string text) =>
            $"<path d=\"{line}\" fill=\"none\" stroke=\"{Accent}\" stroke-opacity=\"0.7\" stroke-width=\"1.2\"></path><text x=\"{x}\" y=\"{y}\" text-anchor=\"{anchor}\" font-family=\"Manrope, sans-serif\" font-size=\"16\" font-weight=\"700\" fill=\"{Accent}\">{Esc(text)}</text>\n";

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}

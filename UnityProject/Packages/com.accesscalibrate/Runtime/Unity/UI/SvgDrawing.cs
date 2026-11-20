// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.UI
{
    /// <summary>
    /// Draws one of the design's SVG drawings (controller, keyboard, mouse, icons) with Painter2D,
    /// so it scales cleanly and follows the theme. The files are the design's own SVGs
    /// (Resources/AccessCalibrate/Drawings/&lt;name&gt;.svg.txt). Their colours are the Dark theme's;
    /// each one is mapped to a --ac-… token (see <see cref="Palette"/>), so Light and High Contrast
    /// recolour them. Supported: path (M L H V C S Q A Z, absolute and relative), rect (rx), circle,
    /// ellipse, line, text, fill / stroke / their opacities, stroke-width, caps and joins.
    /// </summary>
    public sealed class SvgDrawing : VisualElement
    {
        // ------------------------------------------------------------ colour → token
        /// <summary>Design colour (Dark theme value, lower-case "rrggbb@alpha") → token read on this element.</summary>
        internal static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
        {
            { "0b1230@1", "--ac-svg-on-primary" },      // controller body, icon strokes on a light glyph
            { "eef2ff@1", "--ac-svg-primary" },         // icon fill on the glyph
            { "eef2ff@0.8", "--ac-svg-icon" },          // tip icons
            { "0c1330@1", "--ac-svg-body" },
            { "0e1636@1", "--ac-svg-body-2" },
            { "0b0c10@1", "--ac-svg-on-highlight" },
            { "f2eee6@1", "--ac-svg-accent" },
            { "07142a@1", "--ac-svg-on-accent" },
            { "ffffff@0.32", "--ac-svg-stroke" },
            { "ffffff@0.3", "--ac-svg-stroke-key" },
            { "ffffff@0.2", "--ac-svg-stroke-frame" },
            { "ffffff@0.4", "--ac-svg-stroke-mouse" },
            { "ffffff@0.16", "--ac-svg-stroke-soft" },
            { "ffffff@0.08", "--ac-svg-fill-strong" },
            { "ffffff@0.06", "--ac-svg-stroke-faint" },
            { "ffffff@0.03", "--ac-svg-fill" },
            { "ffffff@0.02", "--ac-svg-fill-soft" },
            { "ffffff@0.56", "--ac-svg-label" },
            { "ffffff@0.9", "--ac-svg-tick" },
            { "ffffff@0.44", "--ac-svg-key-label" },
        };
        static readonly Dictionary<string, CustomStyleProperty<Color>> s_Props = new Dictionary<string, CustomStyleProperty<Color>>();
        readonly Dictionary<string, Color> _resolved = new Dictionary<string, Color>();

        static readonly Dictionary<string, Doc> s_Cache = new Dictionary<string, Doc>();
        readonly Doc _doc;
        readonly List<(Label label, TextItem item)> _labels = new List<(Label, TextItem)>();

        /// <summary>The drawing's name (file name without .svg.txt).</summary>
        public string DrawingName { get; }
        /// <summary>Accessible description from the SVG's aria-label.</summary>
        public string Description => _doc?.Label ?? "";
        /// <summary>Shapes and texts read from the file (tests).</summary>
        internal int ShapeCount => _doc?.Shapes.Count ?? 0;
        internal int TextCount => _doc?.Texts.Count ?? 0;
        internal IReadOnlyCollection<string> UnknownColours => _doc?.Unknown ?? (IReadOnlyCollection<string>)Array.Empty<string>();

        public SvgDrawing(string name)
        {
            DrawingName = name;
            AddToClassList("ac-svg");
            pickingMode = PickingMode.Ignore;
            _doc = Load(name);
            if (_doc == null) return;
            style.width = _doc.Width;
            style.height = _doc.Height;
            foreach (var t in _doc.Texts)
            {
                var l = new Label(t.Text) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("ac-svg-text");
                l.AddToClassList("ac-svg-text--w" + t.Weight);
                l.style.position = Position.Absolute;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                Add(l);
                _labels.Add((l, t));
            }
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        /// <summary>Replace a text of the drawing (e.g. the callout "Hold A" → "Press A"). True if found.</summary>
        public bool ReplaceText(string from, string to)
        {
            bool found = false;
            foreach (var (label, item) in _labels) if (label.text == from) { label.text = to; found = true; }
            return found;
        }

        /// <summary>The texts drawn on the drawing (button letters, callout).</summary>
        public IEnumerable<string> Texts { get { foreach (var (label, _) in _labels) yield return label.text; } }

        // ------------------------------------------------------------ loading

        internal static Doc Load(string name)
        {
            if (s_Cache.TryGetValue(name, out var d)) return d;
            var asset = Resources.Load<TextAsset>("AccessCalibrate/Drawings/" + name + ".svg");
            d = asset != null ? Parse(asset.text) : null;
            if (d == null) Debug.LogWarning("AccessCalibrate: drawing not found: " + name);
            s_Cache[name] = d;
            return d;
        }

        internal sealed class Shape
        {
            public List<List<Seg>> Subpaths = new List<List<Seg>>();
            public List<bool> Closed = new List<bool>();
            public string Fill, Stroke;
            public float FillOpacity = 1, StrokeOpacity = 1, Opacity = 1, StrokeWidth = 1;
            public LineCap Cap = LineCap.Butt;
            public LineJoin Join = LineJoin.Miter;
        }
        internal struct Seg { public char Kind; public Vector2 A, B, C; }   // 'M' start, 'L' line to A, 'C' cubic A,B → C
        internal sealed class TextItem { public string Text, Fill, Anchor; public float X, Y, Size, Opacity = 1; public int Weight; }
        internal sealed class Doc
        {
            public float Width, Height; public Rect ViewBox; public string Label = "";
            public List<Shape> Shapes = new List<Shape>();
            public List<TextItem> Texts = new List<TextItem>();
            public HashSet<string> Unknown = new HashSet<string>();
        }

        static float F(string s, float def = 0) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        static string A(XmlElement e, string n) => e.HasAttribute(n) ? e.GetAttribute(n) : null;

        internal static Doc Parse(string xml)
        {
            var x = new XmlDocument { XmlResolver = null };
            x.LoadXml(xml);
            var svg = x.DocumentElement;
            var doc = new Doc { Label = A(svg, "aria-label") ?? "" };
            var vb = (A(svg, "viewBox") ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            doc.Width = F(A(svg, "width"), vb.Length == 4 ? F(vb[2]) : 100);
            doc.Height = F(A(svg, "height"), vb.Length == 4 ? F(vb[3]) : 100);
            doc.ViewBox = vb.Length == 4 ? new Rect(F(vb[0]), F(vb[1]), F(vb[2]), F(vb[3])) : new Rect(0, 0, doc.Width, doc.Height);
            Walk(svg, doc, Matrix4x4.identity);
            foreach (var s in doc.Shapes) { Check(doc, s.Fill, s.FillOpacity); Check(doc, s.Stroke, s.StrokeOpacity); }
            foreach (var t in doc.Texts) Check(doc, t.Fill, 1);
            return doc;
        }

        static void Check(Doc doc, string colour, float extra)
        {
            if (string.IsNullOrEmpty(colour) || colour == "none") return;
            if (!TryParseColour(colour, out var c)) { doc.Unknown.Add(colour); return; }
            if (!Palette.ContainsKey(Key(c))) doc.Unknown.Add(Key(c));
        }

        static void Walk(XmlElement parent, Doc doc, Matrix4x4 m)
        {
            foreach (XmlNode n in parent.ChildNodes)
            {
                if (!(n is XmlElement e)) continue;
                var mm = m * Transform(A(e, "transform"));
                switch (e.LocalName)
                {
                    case "g": Walk(e, doc, mm); break;
                    case "path": AddShape(doc, e, mm, PathData(A(e, "d") ?? "")); break;
                    case "rect": AddShape(doc, e, mm, RectPath(F(A(e, "x")), F(A(e, "y")), F(A(e, "width")), F(A(e, "height")), F(A(e, "rx") ?? A(e, "ry")), F(A(e, "ry") ?? A(e, "rx")))); break;
                    case "circle": { float r = F(A(e, "r")); AddShape(doc, e, mm, EllipsePath(F(A(e, "cx")), F(A(e, "cy")), r, r)); break; }
                    case "ellipse": AddShape(doc, e, mm, EllipsePath(F(A(e, "cx")), F(A(e, "cy")), F(A(e, "rx")), F(A(e, "ry")))); break;
                    case "line":
                    {
                        var sp = new List<Seg> { new Seg { Kind = 'M', A = new Vector2(F(A(e, "x1")), F(A(e, "y1"))) }, new Seg { Kind = 'L', A = new Vector2(F(A(e, "x2")), F(A(e, "y2"))) } };
                        AddShape(doc, e, mm, (new List<List<Seg>> { sp }, new List<bool> { false }), defaultFill: "none");
                        break;
                    }
                    case "text":
                    {
                        var p = mm.MultiplyPoint3x4(new Vector3(F(A(e, "x")), F(A(e, "y"))));
                        doc.Texts.Add(new TextItem
                        {
                            Text = e.InnerText.Trim(), X = p.x, Y = p.y, Size = F(A(e, "font-size"), 12),
                            Weight = (int)F(A(e, "font-weight"), 400), Anchor = A(e, "text-anchor") ?? "start",
                            Fill = A(e, "fill") ?? "#000000", Opacity = F(A(e, "fill-opacity"), 1),
                        });
                        break;
                    }
                }
            }
        }

        static void AddShape(Doc doc, XmlElement e, Matrix4x4 m, (List<List<Seg>> subs, List<bool> closed) geo, string defaultFill = "#000000")
        {
            var s = new Shape
            {
                // No fill attribute on a stroked shape: the design's lines (browsers fill them, but they have no area).
                Fill = A(e, "fill") ?? (A(e, "stroke") != null ? "none" : defaultFill), Stroke = A(e, "stroke"),
                FillOpacity = F(A(e, "fill-opacity"), 1), StrokeOpacity = F(A(e, "stroke-opacity"), 1),
                Opacity = F(A(e, "opacity"), 1), StrokeWidth = F(A(e, "stroke-width"), 1),
                Cap = A(e, "stroke-linecap") == "round" ? LineCap.Round : LineCap.Butt,
                Join = A(e, "stroke-linejoin") == "round" ? LineJoin.Round : A(e, "stroke-linejoin") == "bevel" ? LineJoin.Bevel : LineJoin.Miter,
                Closed = geo.closed,
            };
            // A stroked open path in the design has no fill (browsers fill it, but it has no area).
            float scale = Mathf.Sqrt(Mathf.Abs(m.m00 * m.m11 - m.m01 * m.m10));
            s.StrokeWidth *= scale <= 0 ? 1 : scale;
            foreach (var sub in geo.subs)
            {
                var t = new List<Seg>(sub.Count);
                foreach (var g in sub) t.Add(new Seg { Kind = g.Kind, A = m.MultiplyPoint3x4(g.A), B = m.MultiplyPoint3x4(g.B), C = m.MultiplyPoint3x4(g.C) });
                s.Subpaths.Add(t);
            }
            doc.Shapes.Add(s);
        }

        static Matrix4x4 Transform(string t)
        {
            var m = Matrix4x4.identity;
            if (string.IsNullOrEmpty(t)) return m;
            foreach (Match f in Regex.Matches(t, @"(\w+)\s*\(([^)]*)\)"))
            {
                var a = Array.ConvertAll(f.Groups[2].Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries), v => F(v));
                Matrix4x4 n = Matrix4x4.identity;
                switch (f.Groups[1].Value)
                {
                    case "translate": n = Matrix4x4.Translate(new Vector3(a[0], a.Length > 1 ? a[1] : 0)); break;
                    case "scale": n = Matrix4x4.Scale(new Vector3(a[0], a.Length > 1 ? a[1] : a[0], 1)); break;
                    case "rotate":
                    {
                        var r = Matrix4x4.Rotate(Quaternion.Euler(0, 0, a[0]));
                        n = a.Length >= 3 ? Matrix4x4.Translate(new Vector3(a[1], a[2])) * r * Matrix4x4.Translate(new Vector3(-a[1], -a[2])) : r;
                        break;
                    }
                }
                m *= n;
            }
            return m;
        }

        static (List<List<Seg>>, List<bool>) RectPath(float x, float y, float w, float h, float rx, float ry)
        {
            rx = Mathf.Min(rx, w / 2); ry = Mathf.Min(ry, h / 2);
            if (rx <= 0 || ry <= 0) return PathData($"M{x},{y} H{x + w} V{y + h} H{x} Z");
            string d = string.Format(CultureInfo.InvariantCulture,
                "M{0},{1} H{2} A{4},{5} 0 0 1 {3},{6} V{7} A{4},{5} 0 0 1 {2},{8} H{0} A{4},{5} 0 0 1 {9},{7} V{6} A{4},{5} 0 0 1 {0},{1} Z",
                x + rx, y, x + w - rx, x + w, rx, ry, y + ry, y + h - ry, y + h, x);
            return PathData(d);
        }

        static (List<List<Seg>>, List<bool>) EllipsePath(float cx, float cy, float rx, float ry) =>
            PathData(string.Format(CultureInfo.InvariantCulture, "M{0},{1} A{2},{3} 0 1 1 {4},{1} A{2},{3} 0 1 1 {0},{1} Z", cx - rx, cy, rx, ry, cx + rx));

        /// <summary>SVG path data → subpaths of move / line / cubic segments (arcs and quadratics become cubics).</summary>
        internal static (List<List<Seg>>, List<bool>) PathData(string d)
        {
            var subs = new List<List<Seg>>(); var closed = new List<bool>();
            var tokens = Regex.Matches(d, @"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?");
            int i = 0; char cmd = ' ';
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero; char lastCmd = ' ';
            List<Seg> sub = null;
            bool IsNum() => i < tokens.Count && !char.IsLetter(tokens[i].Value[0]);
            float N() => F(tokens[i++].Value);
            void Begin(Vector2 p) { sub = new List<Seg> { new Seg { Kind = 'M', A = p } }; subs.Add(sub); closed.Add(false); start = p; }
            void Line(Vector2 p) { if (sub == null) Begin(cur); sub.Add(new Seg { Kind = 'L', A = p }); }
            void Cubic(Vector2 a, Vector2 b, Vector2 c) { if (sub == null) Begin(cur); sub.Add(new Seg { Kind = 'C', A = a, B = b, C = c }); }
            while (i < tokens.Count)
            {
                if (!IsNum()) cmd = tokens[i++].Value[0];
                bool rel = char.IsLower(cmd);
                Vector2 o = rel ? cur : Vector2.zero;
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M': cur = o + new Vector2(N(), N()); Begin(cur); cmd = rel ? 'l' : 'L'; break;
                    case 'L': cur = o + new Vector2(N(), N()); Line(cur); break;
                    case 'H': cur = new Vector2((rel ? cur.x : 0) + N(), cur.y); Line(cur); break;
                    case 'V': cur = new Vector2(cur.x, (rel ? cur.y : 0) + N()); Line(cur); break;
                    case 'C': { var a = o + new Vector2(N(), N()); var b = o + new Vector2(N(), N()); var c = o + new Vector2(N(), N()); Cubic(a, b, c); lastCtrl = b; cur = c; break; }
                    case 'S': { var a = (lastCmd == 'C' || lastCmd == 'S') ? 2 * cur - lastCtrl : cur; var b = o + new Vector2(N(), N()); var c = o + new Vector2(N(), N()); Cubic(a, b, c); lastCtrl = b; cur = c; break; }
                    case 'Q': { var q = o + new Vector2(N(), N()); var c = o + new Vector2(N(), N()); Cubic(cur + 2f / 3f * (q - cur), c + 2f / 3f * (q - c), c); cur = c; break; }
                    case 'A':
                    {
                        float rx = N(), ry = N(), rot = N(); bool large = N() != 0, sweep = N() != 0;
                        var end = o + new Vector2(N(), N());
                        foreach (var (a, b, c) in ArcToCubics(cur, rx, ry, rot, large, sweep, end)) Cubic(a, b, c);
                        cur = end; break;
                    }
                    case 'Z': if (sub != null) { closed[closed.Count - 1] = true; } cur = start; sub = null; break;
                    default: i++; break;
                }
                lastCmd = char.ToUpperInvariant(cmd);
            }
            return (subs, closed);
        }

        /// <summary>SVG endpoint arc → cubic Béziers (≤ 90° each).</summary>
        static IEnumerable<(Vector2, Vector2, Vector2)> ArcToCubics(Vector2 p0, float rx, float ry, float rotDeg, bool large, bool sweep, Vector2 p1)
        {
            if (rx == 0 || ry == 0 || p0 == p1) { yield return (p0, p1, p1); yield break; }
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            float phi = rotDeg * Mathf.Deg2Rad, cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            var d = (p0 - p1) / 2f;
            var p = new Vector2(cos * d.x + sin * d.y, -sin * d.x + cos * d.y);
            float lambda = p.x * p.x / (rx * rx) + p.y * p.y / (ry * ry);
            if (lambda > 1) { float s = Mathf.Sqrt(lambda); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * p.y * p.y - ry * ry * p.x * p.x;
            float den = rx * rx * p.y * p.y + ry * ry * p.x * p.x;
            float coef = Mathf.Sqrt(Mathf.Max(0, num / den)) * (large == sweep ? -1 : 1);
            var cp = new Vector2(coef * rx * p.y / ry, -coef * ry * p.x / rx);
            var c = new Vector2(cos * cp.x - sin * cp.y, sin * cp.x + cos * cp.y) + (p0 + p1) / 2f;
            float Ang(Vector2 u, Vector2 v) { float a = Mathf.Atan2(u.x * v.y - u.y * v.x, Vector2.Dot(u, v)); return a; }
            var v1 = new Vector2((p.x - cp.x) / rx, (p.y - cp.y) / ry);
            var v2 = new Vector2((-p.x - cp.x) / rx, (-p.y - cp.y) / ry);
            float t1 = Ang(Vector2.right, v1), dt = Ang(v1, v2);
            if (!sweep && dt > 0) dt -= 2 * Mathf.PI; else if (sweep && dt < 0) dt += 2 * Mathf.PI;
            int segs = Mathf.CeilToInt(Mathf.Abs(dt) / (Mathf.PI / 2) - 1e-4f);
            float step = dt / segs, k = 4f / 3f * Mathf.Tan(step / 4);
            Vector2 Pt(float t) { var e = new Vector2(rx * Mathf.Cos(t), ry * Mathf.Sin(t)); return new Vector2(cos * e.x - sin * e.y, sin * e.x + cos * e.y) + c; }
            Vector2 Dv(float t) { var e = new Vector2(-rx * Mathf.Sin(t), ry * Mathf.Cos(t)); return new Vector2(cos * e.x - sin * e.y, sin * e.x + cos * e.y); }
            for (int s = 0; s < segs; s++)
            {
                float a = t1 + s * step, b = a + step;
                yield return (Pt(a) + k * Dv(a), Pt(b) - k * Dv(b), s == segs - 1 ? p1 : Pt(b));
            }
        }

        // ------------------------------------------------------------ colours

        internal static bool TryParseColour(string s, out Color c)
        {
            c = Color.clear;
            if (string.IsNullOrEmpty(s) || s == "none") return false;
            if (s.StartsWith("#")) return ColorUtility.TryParseHtmlString(s, out c);
            var m = Regex.Match(s, @"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+))?\s*\)");
            if (!m.Success) return false;
            c = new Color(F(m.Groups[1].Value) / 255f, F(m.Groups[2].Value) / 255f, F(m.Groups[3].Value) / 255f, m.Groups[4].Success ? F(m.Groups[4].Value) : 1f);
            return true;
        }

        internal static string Key(Color c) =>
            ColorUtility.ToHtmlStringRGB(c).ToLowerInvariant() + "@" + Math.Round(c.a, 2).ToString(CultureInfo.InvariantCulture);

        void OnStyle(CustomStyleResolvedEvent e)
        {
            _resolved.Clear();
            foreach (var token in Palette.Values)
            {
                if (!s_Props.TryGetValue(token, out var p)) s_Props[token] = p = new CustomStyleProperty<Color>(token);
                if (e.customStyle.TryGetValue(p, out var c)) _resolved[token] = c;
            }
            foreach (var (label, item) in _labels) label.style.color = Resolve(item.Fill, item.Opacity);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// The theme's colour for a design colour (with extra opacity); unmapped colours are used as
        /// they are. A translucent result is composited over the drawing's body colour (gamma space,
        /// like the design), because Unity blends translucent colours in linear space in Linear
        /// projects, which would make a faint halo on the dark controller far too bright.
        /// </summary>
        Color Resolve(string colour, float extra)
        {
            if (!TryParseColour(colour, out var c)) return Color.clear;
            if (Palette.TryGetValue(Key(c), out var token) && _resolved.TryGetValue(token, out var t)) c = t;
            c.a *= extra;
            if (c.a > 0.001f && c.a < 0.999f && _resolved.TryGetValue("--ac-svg-on-primary", out var body))
                c = new Color(Mathf.Lerp(body.r, c.r, c.a), Mathf.Lerp(body.g, c.g, c.a), Mathf.Lerp(body.b, c.b, c.a), 1f);
            return c;
        }

        // ------------------------------------------------------------ drawing

        float Scale => _doc == null || _doc.ViewBox.width <= 0 ? 1 : Mathf.Min(contentRect.width / _doc.ViewBox.width, contentRect.height / _doc.ViewBox.height);
        Vector2 Origin => _doc == null ? Vector2.zero : new Vector2((contentRect.width - _doc.ViewBox.width * Scale) / 2, (contentRect.height - _doc.ViewBox.height * Scale) / 2) - _doc.ViewBox.position * Scale;

        void Draw(MeshGenerationContext mgc)
        {
            if (_doc == null || contentRect.width <= 0) return;
            float k = Scale; var o = Origin;
            Vector2 P(Vector2 v) => o + v * k;
            var p = mgc.painter2D;
            foreach (var s in _doc.Shapes)
            {
                void Trace()
                {
                    p.BeginPath();
                    for (int i = 0; i < s.Subpaths.Count; i++)
                    {
                        foreach (var g in s.Subpaths[i])
                        {
                            if (g.Kind == 'M') p.MoveTo(P(g.A));
                            else if (g.Kind == 'L') p.LineTo(P(g.A));
                            else p.BezierCurveTo(P(g.A), P(g.B), P(g.C));
                        }
                        if (s.Closed[i]) p.ClosePath();
                    }
                }
                var fill = Resolve(s.Fill, s.FillOpacity * s.Opacity);
                if (fill.a > 0.001f && s.Fill != "none") { Trace(); p.fillColor = fill; p.Fill(FillRule.NonZero); }
                var stroke = Resolve(s.Stroke, s.StrokeOpacity * s.Opacity);
                if (stroke.a > 0.001f && !string.IsNullOrEmpty(s.Stroke) && s.Stroke != "none")
                {
                    Trace(); p.strokeColor = stroke; p.lineWidth = s.StrokeWidth * k; p.lineCap = s.Cap; p.lineJoin = s.Join; p.Stroke();
                }
            }
        }

        void PlaceLabels()
        {
            if (_doc == null) return;
            float k = Scale; var o = Origin;
            foreach (var (label, item) in _labels)
            {
                float size = item.Size * k;
                label.style.fontSize = size;
                // SVG y is the baseline; Manrope's ascent is about 0.8 em above it at line-height 1.
                label.style.left = o.x + item.X * k;
                label.style.top = o.y + item.Y * k - size * 0.98f;
                label.style.translate = new Translate(item.Anchor == "middle" ? Length.Percent(-50) : item.Anchor == "end" ? Length.Percent(-100) : new Length(0), new Length(0));
            }
        }
    }
}

// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.UI
{
    /// <summary>
    /// Base for elements drawn with Painter2D. Colours come from USS custom properties declared
    /// on the element's own class (e.g. <c>.ac-progress-ring { --ac-ring-fill: var(--ac-focus); }</c>),
    /// so every drawn colour is still a theme token.
    /// </summary>
    public abstract class DrawnElement : VisualElement
    {
        readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
        readonly string[] _properties;

        protected DrawnElement(string className, params string[] colorProperties)
        {
            AddToClassList(className);
            pickingMode = PickingMode.Ignore;
            _properties = colorProperties;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
        }

        void OnStyle(CustomStyleResolvedEvent e)
        {
            foreach (var p in _properties)
                if (e.customStyle.TryGetValue(new CustomStyleProperty<Color>(p), out var c)) _colors[p] = c;
            MarkDirtyRepaint();
        }

        protected Color C(string property) => _colors.TryGetValue(property, out var c) ? c : Color.white;

        protected abstract void Draw(MeshGenerationContext ctx);
    }

    /// <summary>Hold test: circular progress, starts at 12 o'clock, round caps.</summary>
    public sealed class ProgressRing : DrawnElement
    {
        float _value;
        public float LineWidth = 10f;

        public ProgressRing() : base("ac-progress-ring", "--ac-ring-fill", "--ac-ring-track") { }

        public float Value
        {
            get => _value;
            set { float v = Mathf.Clamp01(value); if (!Mathf.Approximately(v, _value)) { _value = v; MarkDirtyRepaint(); } }
        }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float radius = Mathf.Min(r.width, r.height) / 2f - LineWidth / 2f;
            if (radius <= 0) return;
            var centre = r.center;
            var p = ctx.painter2D;
            p.lineWidth = LineWidth;
            p.lineCap = LineCap.Round;

            p.strokeColor = C("--ac-ring-track");
            p.BeginPath();
            p.Arc(centre, radius, 0f, 360f);
            p.Stroke();

            if (_value <= 0.001f) return;
            p.strokeColor = C("--ac-ring-fill");
            p.BeginPath();
            p.Arc(centre, radius, -90f, -90f + 360f * _value);
            p.Stroke();
        }
    }

    /// <summary>Dashed rounded-rectangle outline (UI Toolkit has no dashed borders).</summary>
    public sealed class DashedOutline : DrawnElement
    {
        public float Radius = 20f, Dash = 6f, Gap = 5f, Width = 1f;

        public DashedOutline() : base("ac-dashed", "--ac-dash-c") { }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            float rad = Mathf.Min(Radius, r.width / 2f, r.height / 2f);
            var pts = RoundedRectPoints(new Rect(r.x + Width / 2, r.y + Width / 2, r.width - Width, r.height - Width), rad);
            DrawDashed(ctx.painter2D, pts, C("--ac-dash-c"), Dash, Gap, Width, closed: true);
        }

        internal static List<Vector2> RoundedRectPoints(Rect r, float rad)
        {
            var pts = new List<Vector2>();
            void Corner(Vector2 c, float from)
            {
                for (int i = 0; i <= 8; i++)
                {
                    float a = (from + 90f * i / 8f) * Mathf.Deg2Rad;
                    pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
                }
            }
            Corner(new Vector2(r.xMax - rad, r.yMin + rad), -90f);
            Corner(new Vector2(r.xMax - rad, r.yMax - rad), 0f);
            Corner(new Vector2(r.xMin + rad, r.yMax - rad), 90f);
            Corner(new Vector2(r.xMin + rad, r.yMin + rad), 180f);
            return pts;
        }

        internal static void DrawDashed(Painter2D p, List<Vector2> pts, Color color, float dash, float gap, float width, bool closed)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Butt;
            float pattern = dash + gap, pos = 0f;
            int n = closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                float len = Vector2.Distance(a, b);
                float t = 0f;
                while (t < len)
                {
                    float phase = pos % pattern;
                    float step = Mathf.Min(len - t, phase < dash ? dash - phase : pattern - phase);
                    if (phase < dash)
                    {
                        p.BeginPath();
                        p.MoveTo(Vector2.Lerp(a, b, t / len));
                        p.LineTo(Vector2.Lerp(a, b, (t + step) / len));
                        p.Stroke();
                    }
                    t += step;
                    pos += step;
                }
            }
        }
    }

    /// <summary>
    /// Steadiness arena: round field, faint crosshair, dashed target circle, cursor with a dark
    /// halo and a short trail. Positions are in arena units (−1…1, y up).
    /// </summary>
    public sealed class StabilityArena : DrawnElement
    {
        public Vector2 Target;          // centre of the target circle
        public float TargetRadius = 0.25f;
        public Vector2 Cursor;
        readonly Queue<Vector2> _trail = new Queue<Vector2>();
        public int TrailLength = 6;

        public StabilityArena() : base("ac-arena", "--ac-arena-fill-c", "--ac-arena-border-c", "--ac-crosshair-c",
                                       "--ac-target-c", "--ac-cursor-c", "--ac-cursor-halo-c", "--ac-trail-c") { }

        public void SetCursor(Vector2 position)
        {
            _trail.Enqueue(Cursor);
            while (_trail.Count > TrailLength) _trail.Dequeue();
            Cursor = position;
            MarkDirtyRepaint();
        }

        public void ClearTrail() { _trail.Clear(); MarkDirtyRepaint(); }

        Vector2 ToPx(Vector2 a, Rect r, float R) => r.center + new Vector2(a.x, -a.y) * R;

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float R = Mathf.Min(r.width, r.height) / 2f - 1f;
            if (R <= 0) return;
            var p = ctx.painter2D;
            var c = r.center;

            p.fillColor = C("--ac-arena-fill-c");
            p.BeginPath(); p.Arc(c, R, 0, 360); p.Fill();
            p.strokeColor = C("--ac-arena-border-c"); p.lineWidth = 1.5f;
            p.BeginPath(); p.Arc(c, R, 0, 360); p.Stroke();

            p.strokeColor = C("--ac-crosshair-c"); p.lineWidth = 1f;
            p.BeginPath(); p.MoveTo(c + new Vector2(-R, 0)); p.LineTo(c + new Vector2(R, 0)); p.Stroke();
            p.BeginPath(); p.MoveTo(c + new Vector2(0, -R)); p.LineTo(c + new Vector2(0, R)); p.Stroke();

            // Dashed target circle.
            var tc = ToPx(Target, r, R);
            float tr = TargetRadius * R;
            var circle = new List<Vector2>();
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                circle.Add(tc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * tr);
            }
            DashedOutline.DrawDashed(p, circle, C("--ac-target-c"), 6f, 5f, 1.5f, closed: true);

            p.fillColor = C("--ac-trail-c");
            foreach (var t in _trail) { p.BeginPath(); p.Arc(ToPx(t, r, R), 3f, 0, 360); p.Fill(); }

            var cp = ToPx(Cursor, r, R);
            p.fillColor = C("--ac-cursor-halo-c");
            p.BeginPath(); p.Arc(cp, 15f, 0, 360); p.Fill();
            p.fillColor = C("--ac-cursor-c");
            p.BeginPath(); p.Arc(cp, 11f, 0, 360); p.Fill();
        }
    }

    /// <summary>Small speaker icon (reaction test: "a soft tone plays").</summary>
    public sealed class SpeakerIcon : DrawnElement
    {
        public SpeakerIcon() : base("ac-speaker-icon", "--ac-icon-c") { style.flexGrow = 1; }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height) / 20f;
            var p = ctx.painter2D;
            var col = C("--ac-icon-c");
            p.fillColor = col;
            p.BeginPath();
            p.MoveTo(new Vector2(2, 7) * s); p.LineTo(new Vector2(6, 7) * s); p.LineTo(new Vector2(11, 3) * s);
            p.LineTo(new Vector2(11, 17) * s); p.LineTo(new Vector2(6, 13) * s); p.LineTo(new Vector2(2, 13) * s);
            p.ClosePath(); p.Fill();
            p.strokeColor = col; p.lineWidth = 1.6f * s; p.lineCap = LineCap.Round;
            p.BeginPath(); p.Arc(new Vector2(11, 10) * s, 4f * s, -45f, 45f); p.Stroke();
            p.BeginPath(); p.Arc(new Vector2(11, 10) * s, 7.5f * s, -50f, 50f); p.Stroke();
        }
    }

    /// <summary>Check mark for finished steps in the rail.</summary>
    public sealed class CheckMark : DrawnElement
    {
        public CheckMark() : base("ac-rail-check", "--ac-check-c") { }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            p.strokeColor = C("--ac-check-c");
            p.lineWidth = 2.2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + r.width * 0.12f, r.yMin + r.height * 0.55f));
            p.LineTo(new Vector2(r.xMin + r.width * 0.40f, r.yMin + r.height * 0.82f));
            p.LineTo(new Vector2(r.xMin + r.width * 0.90f, r.yMin + r.height * 0.20f));
            p.Stroke();
        }
    }

    /// <summary>The on/off switch of a confirmation row (60×34 track, 28 px knob).</summary>
    public sealed class SwitchElement : VisualElement
    {
        public SwitchElement()
        {
            AddToClassList("ac-switch");
            var knob = new VisualElement();
            knob.AddToClassList("ac-switch-knob");
            Add(knob);
        }

        public bool On
        {
            get => ClassListContains("ac-switch--on");
            set => EnableInClassList("ac-switch--on", value);
        }
    }

    /// <summary>
    /// A button glyph: a letter or word in a round / key badge, or a PlayStation shape drawn in
    /// code (Manrope has no ✕ ○ □ △).
    /// </summary>
    public sealed class GlyphBadge : VisualElement
    {
        readonly Label _text;
        GlyphShape _shape;
        Color _color = Color.white;

        public GlyphBadge()
        {
            AddToClassList("ac-glyph");
            pickingMode = PickingMode.Ignore;
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("ac-glyph-text");
            Add(_text);
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(new CustomStyleProperty<Color>("--ac-glyph-c"), out var c)) _color = c;
                MarkDirtyRepaint();
            });
        }

        public void Set(Glyph glyph)
        {
            _shape = glyph.Shape;
            _text.text = glyph.Shape == GlyphShape.None ? glyph.Text : "";
            EnableInClassList("ac-glyph--key", glyph.IsKey);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (_shape == GlyphShape.None) return;
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height) * 0.30f;
            var c = r.center;
            var p = ctx.painter2D;
            p.strokeColor = _color;
            p.lineWidth = Mathf.Max(1.6f, s * 0.22f);
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            p.BeginPath();
            switch (_shape)
            {
                case GlyphShape.Cross:
                    p.MoveTo(c + new Vector2(-s, -s)); p.LineTo(c + new Vector2(s, s));
                    p.MoveTo(c + new Vector2(s, -s)); p.LineTo(c + new Vector2(-s, s));
                    break;
                case GlyphShape.Circle:
                    p.Arc(c, s, 0, 360);
                    break;
                case GlyphShape.Square:
                    p.MoveTo(c + new Vector2(-s, -s)); p.LineTo(c + new Vector2(s, -s));
                    p.LineTo(c + new Vector2(s, s)); p.LineTo(c + new Vector2(-s, s)); p.ClosePath();
                    break;
                case GlyphShape.Triangle:
                    p.MoveTo(c + new Vector2(0, -s * 1.1f)); p.LineTo(c + new Vector2(s * 1.1f, s * 0.8f));
                    p.LineTo(c + new Vector2(-s * 1.1f, s * 0.8f)); p.ClosePath();
                    break;
            }
            p.Stroke();
        }
    }
}

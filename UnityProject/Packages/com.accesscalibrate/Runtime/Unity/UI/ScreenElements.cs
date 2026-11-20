// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.UI
{
    /// <summary>
    /// Hold (06): 300 px ring. Track (r 132, 10 px), the progress arc from 12 o'clock with round caps,
    /// and a dot with a soft halo at the arc's end. The arc's glow is a second, blurred ring behind
    /// it (<see cref="Glow"/>, USS filter: blur).
    /// </summary>
    public sealed class HoldRing : DrawnElement
    {
        float _value;
        /// <summary>Draw only the arc (the blurred glow copy).</summary>
        public bool Glow { get; }

        public HoldRing(bool glow = false) : base(glow ? "ac-hold-glow" : "ac-hold-ring", "--ac-ring-fill", "--ac-ring-track", "--ac-ring-dot-c")
        {
            Glow = glow;
        }

        public float Value
        {
            get => _value;
            set { float v = Mathf.Clamp01(value); if (!Mathf.Approximately(v, _value)) { _value = v; MarkDirtyRepaint(); } }
        }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float k = Mathf.Min(r.width, r.height) / 300f;
            if (k <= 0) return;
            var c = r.center;
            float radius = 132f * k;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            if (!Glow)
            {
                p.strokeColor = C("--ac-ring-track");
                p.lineWidth = 10f * k;
                p.BeginPath(); p.Arc(c, radius, 0, 360); p.Stroke();
            }
            if (_value <= 0.001f) return;
            float end = -90f + 360f * _value;
            p.strokeColor = C("--ac-ring-fill");
            p.lineWidth = (Glow ? 22f : 10f) * k;
            p.BeginPath(); p.Arc(c, radius, -90f, end); p.Stroke();
            if (Glow) return;
            var dot = c + new Vector2(Mathf.Cos(end * Mathf.Deg2Rad), Mathf.Sin(end * Mathf.Deg2Rad)) * radius;
            var halo = C("--ac-ring-fill"); halo.a *= 0.25f;
            p.fillColor = halo; p.BeginPath(); p.Arc(dot, 15f * k, 0, 360); p.Fill();
            p.fillColor = C("--ac-ring-dot-c"); p.BeginPath(); p.Arc(dot, 9f * k, 0, 360); p.Fill();
        }
    }

    /// <summary>Repeated presses (08): 40 ticks round a 300 px circle, one lights per press; the latest is longer.</summary>
    public sealed class TickRing : DrawnElement
    {
        int _lit;
        public const int Ticks = 40;

        public TickRing() : base("ac-tick-ring", "--ac-tick-c", "--ac-tick-lit-c") { }

        /// <summary>How many ticks are lit (presses so far, wrapping after 40).</summary>
        public int Lit
        {
            get => _lit;
            set { if (value != _lit) { _lit = value; MarkDirtyRepaint(); } }
        }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float k = Mathf.Min(r.width, r.height) / 300f;
            if (k <= 0) return;
            var c = r.center;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            int lit = _lit <= 0 ? 0 : ((_lit - 1) % Ticks) + 1;
            for (int i = 0; i < Ticks; i++)
            {
                bool on = i < lit, latest = i == lit - 1;
                float a = (-90f + i * 9f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float r1 = (latest ? 118f : 124f) * k, r2 = (latest ? 146f : 142f) * k;
                p.strokeColor = C(on ? "--ac-tick-lit-c" : "--ac-tick-c");
                p.lineWidth = (latest ? 4f : 3f) * k;
                p.BeginPath(); p.MoveTo(c + dir * r1); p.LineTo(c + dir * r2); p.Stroke();
            }
        }
    }

    /// <summary>A dashed circle filling the element (reaction: the inner waiting ring, the signal's outer ring).</summary>
    public sealed class DashedCircle : DrawnElement
    {
        public float Dash = 4f, Gap = 4f, Width = 1f;

        public DashedCircle() : base("ac-dashed-circle", "--ac-dash-c") { }

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float radius = Mathf.Min(r.width, r.height) / 2f - Width / 2f;
            if (radius <= 0) return;
            var p = ctx.painter2D;
            p.strokeColor = C("--ac-dash-c");
            p.lineWidth = Width;
            float step = (Dash + Gap) / radius * Mathf.Rad2Deg, dash = Dash / radius * Mathf.Rad2Deg;
            for (float a = 0; a < 360f - 0.01f; a += step)
            {
                p.BeginPath(); p.Arc(r.center, radius, a, Mathf.Min(a + dash, 360f)); p.Stroke();
            }
        }
    }

    /// <summary>
    /// Steadiness (10): 320 px target. Circle (r 150) with a 2 px ring that is accent-coloured while
    /// the dot is inside and dim outside, a faint crosshair, and the dot (r 9) with a soft halo (r 22).
    /// Cursor is in units of the circle radius (0,0 = centre); no trail.
    /// </summary>
    public sealed class SteadinessTarget : DrawnElement
    {
        Vector2 _cursor;
        public SteadinessTarget() : base("ac-arena", "--ac-arena-fill-c", "--ac-arena-border-c", "--ac-arena-dim-c", "--ac-crosshair-c", "--ac-cursor-c") { }

        public Vector2 Cursor
        {
            get => _cursor;
            set { if (value != _cursor) { _cursor = value; MarkDirtyRepaint(); } }
        }
        public bool Inside => _cursor.sqrMagnitude <= 1f;

        protected override void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float k = Mathf.Min(r.width, r.height) / 320f;
            if (k <= 0) return;
            var c = r.center;
            float R = 150f * k;
            var p = ctx.painter2D;
            p.fillColor = C("--ac-arena-fill-c");
            p.BeginPath(); p.Arc(c, R, 0, 360); p.Fill();
            p.strokeColor = C(Inside ? "--ac-arena-border-c" : "--ac-arena-dim-c");
            p.lineWidth = 2f * k;
            p.BeginPath(); p.Arc(c, R - 1f * k, 0, 360); p.Stroke();
            p.strokeColor = C("--ac-crosshair-c");
            p.lineWidth = 1f;
            p.BeginPath(); p.MoveTo(c + new Vector2(0, -R)); p.LineTo(c + new Vector2(0, R)); p.MoveTo(c + new Vector2(-R, 0)); p.LineTo(c + new Vector2(R, 0)); p.Stroke();
            var dot = c + new Vector2(_cursor.x, -_cursor.y) * R;   // +y is up for the player
            var halo = C("--ac-cursor-c"); halo.a *= 0.16f;
            p.fillColor = halo; p.BeginPath(); p.Arc(dot, 22f * k, 0, 360); p.Fill();
            p.fillColor = C("--ac-cursor-c"); p.BeginPath(); p.Arc(dot, 9f * k, 0, 360); p.Fill();
        }
    }
}

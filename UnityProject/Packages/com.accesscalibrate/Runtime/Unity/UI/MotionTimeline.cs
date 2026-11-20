// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.UI
{
    internal enum Ease { Linear, Out, InOut, Overshoot }

    /// <summary>
    /// The screens' motion (Docs/design-v2, spec section 4): a timeline of tweens on opacity,
    /// translate, scale and blur, plus looping effects. Times are seconds from <see cref="Begin"/>.
    ///
    /// Rules: before its start a tween holds its "from" state only when it is the element's first
    /// tween (so nothing flashes before it fogs in); later tweens take over where the earlier ones
    /// ended. With <see cref="Reduce"/> nothing animates: every element shows its final state.
    /// At the end the inline styles are cleared, so the USS decides again.
    /// </summary>
    internal sealed class MotionTimeline
    {
        /// <summary>Same time axis as the calibration (tests replace it).</summary>
        public Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;
        /// <summary>Reduce motion: no animation, final states only.</summary>
        public bool Reduce;
        /// <summary>Animate a real blur in fog-ins (off: opacity and scale only, the fallback when blur is too slow).</summary>
        public bool Blur = true;

        /// <summary>Frame time source (tests replace it).</summary>
        public Func<float> FrameTime = () => Time.unscaledDeltaTime;
        /// <summary>Above this mean frame time while blurring (below 40 fps), blur is turned off: opacity and scale stay.</summary>
        public const float SlowFrameSeconds = 1f / 40f;
        /// <summary>
        /// While a blur plays: the first few frames are skipped (layout, first draw), then the decision
        /// comes after half a second of frames (at least 4), or 30 frames, whichever is first.
        /// </summary>
        public const int BlurSampleFrames = 30, BlurSkipFrames = 3, BlurMinFrames = 4;
        public const float BlurSampleSeconds = 0.5f;

        /// <summary>Blur was too slow on this device: every timeline made from now on starts without it.</summary>
        public static bool SlowDevice { get; private set; }
        internal static void ResetSlowDevice() => SlowDevice = false;
        /// <summary>Raised once when blur was turned off for being too slow.</summary>
        public event Action BlurFellBack;
        int _blurFrames;
        float _blurTime;

        sealed class Tween
        {
            public VisualElement E;
            public double Start, Duration;
            public Ease Ease;
            public bool First;
            public bool HasOpacity, HasTranslate, HasScale, HasBlur, HasScaleX;
            public float O0, O1, B0, B1;
            public Func<Vector2> T0, T1;
            public Vector2 S0, S1;
            public Action Call;
            public bool HiddenForLayout;
            public bool Fired;
            public double End => Start + Duration;
        }

        readonly List<Tween> _tweens = new List<Tween>();
        readonly List<(VisualElement e, Action<VisualElement, double> f)> _loops = new List<(VisualElement, Action<VisualElement, double>)>();
        double _origin;
        int _version;   // changes when the timeline is finished or replaced (a call may do that mid-tick)

        /// <summary>Seconds since the timeline began.</summary>
        public double Elapsed => Clock() - _origin;
        /// <summary>Seconds from the start to the end of the last tween (0 when nothing is scheduled).</summary>
        public double Length { get; private set; }
        /// <summary>A timeline is playing.</summary>
        public bool Playing => _tweens.Count > 0;

        /// <summary>Ends the running timeline (final states) and starts a new one now.</summary>
        public void Begin()
        {
            Finish();
            _origin = Clock();
            Length = 0;
        }

        /// <summary>Shows every element in its final state and stops the timeline. Loops keep running.</summary>
        public void Finish()
        {
            _version++;
            var list = new List<Tween>(_tweens);
            _tweens.Clear();
            foreach (var t in list)
            {
                if (t.Call != null) { if (!t.Fired) { t.Fired = true; t.Call(); } }
                else Clear(t.E);
            }
        }

        /// <summary>Stops everything, loops included (a new screen).</summary>
        public void Stop()
        {
            Finish();
            foreach (var (e, _) in _loops) { e.style.opacity = StyleKeyword.Null; e.style.scale = StyleKeyword.Null; e.style.rotate = StyleKeyword.Null; e.style.translate = StyleKeyword.Null; }
            _loops.Clear();
        }

        // ------------------------------------------------------------ building blocks

        /// <summary>"Fog-in": opacity 0 → 1, blur 14 → 0, up from <paramref name="dy"/>, scale <paramref name="scale"/> → 1, ease-out.</summary>
        public void FogIn(VisualElement e, double at, double duration = 0.7, float dy = 8, float scale = 1.06f)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = Ease.Out,
                HasOpacity = true, O0 = 0, O1 = 1,
                HasBlur = Blur, B0 = 14, B1 = 0,
                HasTranslate = dy != 0, T0 = Const(new Vector2(0, dy)), T1 = Const(Vector2.zero),
                HasScale = scale != 1, S0 = Vector2.one * scale, S1 = Vector2.one,
            });
        }

        /// <summary>Fog-in at a place given by functions (read once the layout is known), and a fixed scale.</summary>
        public void FogInAt(VisualElement e, double at, double duration, Func<Vector2> place, float scale)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = Ease.Out,
                HasOpacity = true, O0 = 0, O1 = 1,
                HasBlur = Blur, B0 = 14, B1 = 0,
                HasTranslate = true, T0 = () => place() + new Vector2(0, 8), T1 = place,
                HasScale = true, S0 = Vector2.one * scale, S1 = Vector2.one * scale,
            });
        }

        /// <summary>Opacity only.</summary>
        public void Fade(VisualElement e, double at, double duration, float from = 0, float to = 1)
        {
            Add(new Tween { E = e, Start = at, Duration = duration, Ease = Ease.Out, HasOpacity = true, O0 = from, O1 = to });
        }

        /// <summary>Fade out with blur and a scale (the centre leaving on ready → active).</summary>
        public void FogOut(VisualElement e, double at, double duration, float scaleTo = 0.94f)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = Ease.InOut,
                HasOpacity = true, O0 = 1, O1 = 0,
                HasBlur = Blur, B0 = 0, B1 = 14,
                HasScale = true, S0 = Vector2.one, S1 = Vector2.one * scaleTo,
            });
        }

        /// <summary>Move from one offset to another (and optionally scale), with an easing.</summary>
        public void Slide(VisualElement e, double at, double duration, Func<Vector2> from, Func<Vector2> to, Ease ease = Ease.InOut, float scaleFrom = 1, float scaleTo = 1)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = ease,
                HasTranslate = true, T0 = from, T1 = to,
                HasScale = scaleFrom != 1 || scaleTo != 1, S0 = Vector2.one * scaleFrom, S1 = Vector2.one * scaleTo,
            });
        }

        /// <summary>A line drawing left → right (scale X 0 → 1 from the left edge).</summary>
        public void DrawX(VisualElement e, double at, double duration)
        {
            e.style.transformOrigin = new TransformOrigin(UnityEngine.UIElements.Length.Percent(0), UnityEngine.UIElements.Length.Percent(50));
            Add(new Tween { E = e, Start = at, Duration = duration, Ease = Ease.InOut, HasScaleX = true, S0 = new Vector2(0, 1), S1 = Vector2.one });
        }

        /// <summary>A card dropping into place from <paramref name="fromY"/> with an overshoot (cubic-bezier(.34,1.56,.64,1)).</summary>
        public void Drop(VisualElement e, double at, double duration, Vector2 from)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = Ease.Overshoot,
                HasOpacity = true, O0 = 0, O1 = 1,
                HasTranslate = true, T0 = Const(from), T1 = Const(Vector2.zero),
            });
        }

        /// <summary>A small element popping in (scale 0.6 → 1 with an overshoot).</summary>
        public void Pop(VisualElement e, double at, double duration = 0.45)
        {
            Add(new Tween
            {
                E = e, Start = at, Duration = duration, Ease = Ease.Overshoot,
                HasOpacity = true, O0 = 0, O1 = 1,
                HasScale = true, S0 = Vector2.one * 0.6f, S1 = Vector2.one,
            });
        }

        /// <summary>A looping effect: <paramref name="f"/> gets the element and the time since the loop started.</summary>
        public void Loop(VisualElement e, Action<VisualElement, double> f)
        {
            if (Reduce || e == null) return;
            double start = Clock();
            _loops.Add((e, (el, _) => f(el, Clock() - start)));
        }

        /// <summary>Runs <paramref name="call"/> once at <paramref name="at"/> (at once with reduce motion, or when the timeline is finished early).</summary>
        public void At(double at, Action call)
        {
            if (Reduce) { call(); return; }
            var t = new Tween { Start = at, Call = call };
            _tweens.Add(t);
            Length = Math.Max(Length, at);
            if (Elapsed >= at) { t.Fired = true; call(); }
        }

        static Func<Vector2> Const(Vector2 v) => () => v;

        void Add(Tween t)
        {
            if (Reduce || t.E == null) return;
            t.First = !_tweens.Exists(x => x.E == t.E && x.Call == null);
            _tweens.Add(t);
            Length = Math.Max(Length, t.End);
            Apply(t, Elapsed);
        }

        // ------------------------------------------------------------ playing

        /// <summary>Applies the timeline at the current time. Called every frame.</summary>
        public void Tick()
        {
            if (_tweens.Count > 0)
            {
                double now = Elapsed;
                if (Blur && _tweens.Exists(x => x.HasBlur && now >= x.Start && now <= x.End)) SampleBlurFrame();
                var list = new List<Tween>(_tweens);
                int version = _version;
                foreach (var t in list)
                {
                    if (_version != version) break;
                    if (t.Call != null) { if (!t.Fired && now >= t.Start) { t.Fired = true; t.Call(); } }
                    else Apply(t, now);
                }
                if (_version == version && now >= Length && _tweens.Count > 0) Finish();
            }
            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                var (e, f) = _loops[i];
                if (e.panel == null) { _loops.RemoveAt(i); continue; }
                f(e, 0);
            }
        }

        void SampleBlurFrame()
        {
            _blurFrames++;
            if (_blurFrames <= BlurSkipFrames) return;
            _blurTime += FrameTime();
            int n = _blurFrames - BlurSkipFrames;
            if (n < BlurSampleFrames && (n < BlurMinFrames || _blurTime < BlurSampleSeconds)) return;
            float mean = _blurTime / n;
            _blurFrames = 0; _blurTime = 0;
            if (mean > SlowFrameSeconds) TurnOffBlur();
        }

        /// <summary>No more blur (too slow): running fog-ins go on with opacity and scale only.</summary>
        public void TurnOffBlur()
        {
            if (!Blur) return;
            Blur = false;
            SlowDevice = true;
            foreach (var t in _tweens)
                if (t.HasBlur) { t.HasBlur = false; t.E.style.filter = StyleKeyword.Null; }
            BlurFellBack?.Invoke();
        }

        void Apply(Tween t, double now)
        {
            if (now < t.Start && !t.First) return;
            double raw = t.Duration <= 0 ? 1 : (now - t.Start) / t.Duration;
            float p = (float)Math.Max(0, Math.Min(1, raw));
            float k = Eval(t.Ease, p);
            var s = t.E.style;
            if (t.HasOpacity) s.opacity = Mathf.LerpUnclamped(t.O0, t.O1, Mathf.Clamp01(t.Ease == Ease.Overshoot ? p * 2 : k));
            if (t.HasTranslate)
            {
                var a = t.T0(); var b = t.T1();
                if (float.IsNaN(a.x) || float.IsNaN(b.x)) { s.opacity = 0; t.HiddenForLayout = true; }   // layout not known yet: stay hidden
                else
                {
                    if (t.HiddenForLayout && !t.HasOpacity) s.opacity = StyleKeyword.Null;
                    t.HiddenForLayout = false;
                    var v = Vector2.LerpUnclamped(a, b, k);
                    s.translate = new Translate(v.x, v.y);
                }
            }
            if (t.HasScale || t.HasScaleX)
            {
                var v = Vector2.LerpUnclamped(t.S0, t.S1, k);
                s.scale = new Scale(new Vector3(v.x, v.y, 1));
            }
            if (t.HasBlur)
            {
                float b = Mathf.Lerp(t.B0, t.B1, k);
                if (b < 0.05f) s.filter = StyleKeyword.Null;
                else
                {
                    var f = new FilterFunction(FilterFunctionType.Blur);
                    f.AddParameter(new FilterParameter(b));
                    s.filter = new List<FilterFunction> { f };
                }
            }
        }

        static void Clear(VisualElement e)
        {
            var s = e.style;
            s.opacity = StyleKeyword.Null;
            s.translate = StyleKeyword.Null;
            s.scale = StyleKeyword.Null;
            s.filter = StyleKeyword.Null;
        }

        /// <summary>Easing curves: ease-out (cubic), ease-in-out (cubic), and the design's overshoot cubic-bezier(.34,1.56,.64,1).</summary>
        internal static float Eval(Ease ease, float p)
        {
            switch (ease)
            {
                case Ease.Out: return 1 - Mathf.Pow(1 - p, 3);
                case Ease.InOut: return p < 0.5f ? 4 * p * p * p : 1 - Mathf.Pow(-2 * p + 2, 3) / 2;
                case Ease.Overshoot: return Bezier(0.34f, 1.56f, 0.64f, 1f, p);
                default: return p;
            }
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at time <paramref name="x"/>.</summary>
        static float Bezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float cx = Curve(x1, x2, t) - x;
                float d = Derivative(x1, x2, t);
                if (Mathf.Abs(cx) < 1e-5f || Mathf.Abs(d) < 1e-6f) break;
                t -= cx / d;
            }
            return Curve(y1, y2, Mathf.Clamp01(t));
        }

        static float Curve(float a, float b, float t) => 3 * a * t * (1 - t) * (1 - t) + 3 * b * t * t * (1 - t) + t * t * t;
        static float Derivative(float a, float b, float t) => 3 * a * (1 - t) * (1 - t) + 6 * (b - a) * t * (1 - t) + 3 * (1 - b) * t * t;
    }
}

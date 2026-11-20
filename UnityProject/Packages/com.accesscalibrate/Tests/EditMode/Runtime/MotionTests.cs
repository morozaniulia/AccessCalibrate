// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using AccessCalibration.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AccessCalibration.Tests
{
    /// <summary>The motion timeline (spec section 4) with a fixed clock.</summary>
    public class MotionTests
    {
        double _t;
        MotionTimeline _m;

        [SetUp]
        public void SetUp()
        {
            _t = 100;
            MotionTimeline.ResetSlowDevice();
            _m = new MotionTimeline { Clock = () => _t };
            _m.Begin();
        }

        void At(double seconds) { _t = 100 + seconds; _m.Tick(); }

        [Test]
        public void FogIn_HiddenBeforeItStarts_ThenClearsItsStylesAtTheEnd()
        {
            var e = new VisualElement();
            _m.FogIn(e, 1.0, 0.7);
            Assert.AreEqual(0f, e.style.opacity.value, "hidden at once: nothing flashes before it fogs in");
            At(0.5);
            Assert.AreEqual(0f, e.style.opacity.value);
            Assert.AreEqual(1.06f, e.style.scale.value.value.x, 1e-4);
            Assert.AreEqual(8f, e.style.translate.value.y.value, 1e-4);
            Assert.AreEqual(1, e.style.filter.value.Count, "blurred");
            At(1.35);
            Assert.Greater(e.style.opacity.value, 0f);
            Assert.Less(e.style.opacity.value, 1f);
            At(1.8);
            Assert.IsFalse(_m.Playing, "the timeline is over");
            Assert.AreEqual(StyleKeyword.Null, e.style.opacity.keyword, "the USS decides again");
            Assert.AreEqual(StyleKeyword.Null, e.style.filter.keyword);
        }

        [Test]
        public void WithoutBlur_FogInIsOpacityAndScaleOnly()
        {
            _m.Blur = false;
            var e = new VisualElement();
            _m.FogIn(e, 0, 1);
            At(0.2);
            Assert.AreEqual(StyleKeyword.Null, e.style.filter.keyword, "no blur set");
            Assert.Greater(e.style.scale.value.value.x, 1f);
        }

        [Test]
        public void SlowFrames_WhileBlurring_TurnBlurOff_OpacityAndScaleStay()
        {
            float frame = 0.045f;   // about 22 fps
            _m.FrameTime = () => frame;
            bool fellBack = false;
            _m.BlurFellBack += () => fellBack = true;
            var e = new VisualElement();
            _m.FogIn(e, 0, 5);
            // a slow device decides fast: 3 skipped frames, then half a second of 45 ms frames (12 frames)
            for (int i = 0; i < MotionTimeline.BlurSkipFrames + 12; i++) At(0.01 * (i + 1));
            Assert.IsTrue(fellBack);
            Assert.IsTrue(MotionTimeline.SlowDevice, "remembered for the next screens");
            Assert.IsFalse(_m.Blur);
            Assert.AreEqual(StyleKeyword.Null, e.style.filter.keyword, "the running fog-in lost its blur");
            At(1);
            Assert.AreEqual(StyleKeyword.Null, e.style.filter.keyword);
            Assert.Greater(e.style.scale.value.value.x, 1f, "scale and opacity still animate");
            Assert.Greater(e.style.opacity.value, 0f);
        }

        [TearDown]
        public void TearDown() => MotionTimeline.ResetSlowDevice();

        [Test]
        public void FastFrames_KeepTheBlur()
        {
            _m.FrameTime = () => 1f / 60f;
            var e = new VisualElement();
            _m.FogIn(e, 0, 5);
            for (int i = 0; i < 100; i++) At(0.01 * (i + 1));
            Assert.IsTrue(_m.Blur);
            Assert.AreEqual(1, e.style.filter.value.Count);
        }

        [Test]
        public void ReduceMotion_NothingAnimates_CallsRunAtOnce()
        {
            _m.Reduce = true;
            var e = new VisualElement();
            _m.FogIn(e, 1, 1);
            _m.Drop(e, 2, 1, new Vector2(0, -56));
            bool called = false;
            _m.At(3, () => called = true);
            Assert.IsTrue(called);
            Assert.IsFalse(_m.Playing);
            Assert.AreEqual(StyleKeyword.Null, e.style.opacity.keyword, "no inline style at all");
            _m.Loop(e, (x, t) => x.style.opacity = 0.1f);
            _m.Tick();
            Assert.AreEqual(StyleKeyword.Null, e.style.opacity.keyword, "no loops either");
        }

        [Test]
        public void ALaterTween_TakesOverWhereTheFirstEnded()
        {
            var e = new VisualElement();
            _m.FogInAt(e, 0, 1, () => new Vector2(100, 50), 1.08f);
            _m.Slide(e, 2, 1, () => new Vector2(100, 50), () => Vector2.zero, Ease.InOut, 1.08f, 1f);
            At(1.5);   // between the two: held centred at 1.08
            Assert.AreEqual(100f, e.style.translate.value.x.value, 1e-3);
            Assert.AreEqual(1.08f, e.style.scale.value.value.x, 1e-4);
            At(2.5);
            Assert.AreEqual(50f, e.style.translate.value.x.value, 1e-3, "half way (ease-in-out is symmetric)");
        }

        [Test]
        public void Overshoot_GoesPastTheEnd_ThenSettles()
        {
            float max = 0;
            for (float p = 0; p <= 1; p += 0.01f) max = Mathf.Max(max, MotionTimeline.Eval(Ease.Overshoot, p));
            Assert.Greater(max, 1.05f, "cubic-bezier(.34,1.56,.64,1) overshoots");
            Assert.AreEqual(1f, MotionTimeline.Eval(Ease.Overshoot, 1), 1e-5);
            Assert.AreEqual(0f, MotionTimeline.Eval(Ease.Overshoot, 0), 1e-5);
            Assert.AreEqual(0.5f, MotionTimeline.Eval(Ease.InOut, 0.5f), 1e-5);
        }

        [Test]
        public void Begin_FinishesTheRunningTimeline_AndRunsItsPendingCalls()
        {
            var e = new VisualElement();
            bool called = false;
            _m.FogIn(e, 5, 1);
            _m.At(4, () => called = true);
            _m.Begin();
            Assert.IsTrue(called, "a state swap is never lost");
            Assert.AreEqual(StyleKeyword.Null, e.style.opacity.keyword, "final state");
        }

        [Test]
        public void Loop_StopsWhenItsElementLeavesThePanel()
        {
            var e = new VisualElement();
            int calls = 0;
            _m.Loop(e, (x, t) => calls++);
            _m.Tick();
            Assert.AreEqual(0, calls, "not in a panel: removed");
        }
    }
}

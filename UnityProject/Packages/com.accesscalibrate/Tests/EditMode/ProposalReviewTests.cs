// Copyright 2026 Morozan Iulia. Licensed under the Apache License, Version 2.0.

using System.Linq;
using NUnit.Framework;

namespace AccessCalibration.Core.Tests
{
    public class ProposalReviewTests
    {
        static readonly RulesConfig Config = new RulesConfig { ReactionLatencyMarginMs = 0 };

        static Measurements Sample(Confidence reactionConfidence = Confidence.Ok) => new Measurements
        {
            Reaction = new ReactionMeasurement { MedianMs = 800, Confidence = reactionConfidence },  // timing ×2 + QTE skippable
            Hold = new HoldMeasurement { MaxHoldSeconds = 1.0, Confidence = Confidence.Ok },        // hold to toggle
            Buttons = new ButtonAccessMeasurement
            {
                RequestedButtons = { "A", "B", "X", "Y" },
                UnpressedButtons = { "Y" },                                                       // remap Y
                Confidence = Confidence.Ok,
            },
        };

        static ProposalReview.Item Item(ProposalReview r, string setting) => r.Items.Single(i => i.Change.Setting == setting);

        [Test]
        public void ReliableProposals_StartOn_UncertainOnes_StartOff()
        {
            var review = new ProposalReview(RulesEngine.Evaluate(Sample(Confidence.Uncertain), Config));

            Assert.IsFalse(Item(review, "timingMultiplier").Accepted);
            Assert.IsFalse(Item(review, "qteMode").Accepted);
            Assert.IsTrue(Item(review, "holdToToggle").Accepted);
            Assert.IsTrue(Item(review, "remapSuggestions").Accepted);
        }

        [Test]
        public void BuildProfile_AppliesOnlyAcceptedProposals()
        {
            var review = new ProposalReview(RulesEngine.Evaluate(Sample(), Config));
            Item(review, "qteMode").Accepted = false;            // player switches one off

            var p = review.BuildProfile(new AccessibilityProfile());

            Assert.AreEqual(2.0f, p.timingMultiplier);
            Assert.AreEqual(QteMode.Normal, p.qteMode);          // stayed at the base value
            Assert.IsTrue(p.holdToToggle);
            CollectionAssert.AreEqual(new[] { "Y" }, p.remapSuggestions);
        }

        [Test]
        public void BuildProfile_NothingAccepted_EqualsTheBaseProfile()
        {
            var review = new ProposalReview(RulesEngine.Evaluate(Sample(), Config));
            foreach (var i in review.Items) i.Accepted = false;

            var baseProfile = new AccessibilityProfile { stickDeadzone = 0.2f };
            Assert.AreEqual(baseProfile, review.BuildProfile(baseProfile));
        }

        [Test]
        public void UncertainProposal_CanStillBeSwitchedOnByThePlayer()
        {
            var review = new ProposalReview(RulesEngine.Evaluate(Sample(Confidence.Uncertain), Config));
            Item(review, "timingMultiplier").Accepted = true;

            Assert.AreEqual(2.0f, review.BuildProfile(new AccessibilityProfile()).timingMultiplier);
        }

        [Test]
        public void EveryProposal_KnowsWhichTestItCameFrom()
        {
            var review = new ProposalReview(RulesEngine.Evaluate(Sample(), Config));
            Assert.AreEqual(MeasurementKind.Reaction, Item(review, "qteMode").Change.Source);
            Assert.AreEqual(MeasurementKind.Hold, Item(review, "holdToToggle").Change.Source);
            Assert.AreEqual(MeasurementKind.Buttons, Item(review, "remapSuggestions").Change.Source);
        }
    }
}

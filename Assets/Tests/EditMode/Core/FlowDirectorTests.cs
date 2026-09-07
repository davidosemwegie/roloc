using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roloc.Core;

namespace Roloc.Tests
{
    public sealed class FlowDirectorTests
    {
        [Test]
        public void EqualSeedsProduceEqualSchedules()
        {
            var first = new FlowDirector(new Random(123));
            var second = new FlowDirector(new Random(123));
            for (int score = 1; score <= 500; score++)
            {
                first.Advance(score);
                second.Advance(score);
                Assert.That(second.Mode, Is.EqualTo(first.Mode));
                Assert.That(second.RotationSteps, Is.EqualTo(first.RotationSteps));
                Assert.That(second.ExtraSeconds, Is.EqualTo(first.ExtraSeconds));
            }
        }

        [Test]
        public void DifferentSeedsProduceVariedSchedules()
        {
            var schedules = new HashSet<string>();
            for (int seed = 0; seed < 12; seed++)
            {
                var director = new FlowDirector(new Random(seed));
                var schedule = new System.Text.StringBuilder();
                for (int score = 1; score <= 100; score++)
                {
                    director.Advance(score);
                    schedule.Append((int)director.Mode);
                }
                schedules.Add(schedule.ToString());
            }
            Assert.That(schedules.Count, Is.GreaterThan(1));
        }

        [Test]
        public void EarlyScoresAndEligibilityThresholdsAreProtected()
        {
            var observed = new HashSet<FlowMode>();
            for (int seed = 0; seed < 20; seed++)
            {
                var director = new FlowDirector(new Random(seed));
                int firstEpisode = 0;
                for (int score = 1; score <= 200; score++)
                {
                    director.Advance(score);
                    observed.Add(director.Mode);
                    if (director.Mode != FlowMode.Steady && firstEpisode == 0) firstEpisode = score;
                    if (score < 5) Assert.That(director.Mode, Is.EqualTo(FlowMode.Steady));
                    if (score < 20) Assert.That(director.Mode, Is.Not.EqualTo(FlowMode.Drifting));
                    if (score < 40) Assert.That(director.Mode, Is.Not.EqualTo(FlowMode.Rotation));
                }
                Assert.That(firstEpisode, Is.InRange(5, 7));
            }
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(FlowMode)), observed);
        }

        [Test]
        public void EpisodesHaveBoundedLengthsCalmGapsAndNoImmediateRepeats()
        {
            var settings = new FlowSettings { StartScore = 1, DriftStartScore = 1, RotationStartScore = 1 };
            var director = new FlowDirector(new Random(44), settings);
            FlowMode previousMode = FlowMode.Steady;
            FlowMode previousSpecial = FlowMode.Steady;
            int duration = 0;
            int calmRounds = 0;
            bool hadEpisode = false;
            for (int score = 1; score <= 1000; score++)
            {
                director.Advance(score);
                if (director.Mode == FlowMode.Steady)
                {
                    if (previousMode != FlowMode.Steady)
                    {
                        if (previousMode == FlowMode.Rotation) Assert.That(duration, Is.EqualTo(1));
                        else Assert.That(duration, Is.InRange(3, 5));
                        duration = 0;
                        calmRounds = 0;
                    }
                    calmRounds++;
                }
                else
                {
                    if (previousMode == FlowMode.Steady)
                    {
                        if (hadEpisode) Assert.That(calmRounds, Is.InRange(1, 2));
                        Assert.That(director.Mode, Is.Not.EqualTo(previousSpecial));
                        previousSpecial = director.Mode;
                        hadEpisode = true;
                    }
                    else Assert.That(director.Mode, Is.EqualTo(previousMode));
                    duration++;
                }
                previousMode = director.Mode;
            }
        }

        [Test]
        public void BreatherCooldownIsMeasuredFromPriorEntry()
        {
            var director = new FlowDirector(new Random(53));
            int previousEntry = -10;
            int episodes = 0;
            FlowMode previous = FlowMode.Steady;
            for (int score = 1; score <= 1000; score++)
            {
                director.Advance(score);
                if (director.Mode == FlowMode.Breather && previous != FlowMode.Breather)
                {
                    Assert.That(score - previousEntry, Is.GreaterThanOrEqualTo(10));
                    previousEntry = score;
                    episodes++;
                }
                Assert.That(director.ExtraSeconds, Is.EqualTo(director.Mode == FlowMode.Breather ? 0.65f : 0f));
                previous = director.Mode;
            }
            Assert.That(episodes, Is.GreaterThan(1));
        }

        [Test]
        public void RotationLastsOneRoundAndChoosesBothDirections()
        {
            var director = new FlowDirector(new Random(73));
            var directions = new HashSet<int>();
            bool wasRotation = false;
            for (int score = 1; score <= 2000; score++)
            {
                director.Advance(score);
                if (wasRotation) Assert.That(director.Mode, Is.EqualTo(FlowMode.Steady));
                if (director.Mode == FlowMode.Rotation)
                {
                    Assert.That(Math.Abs(director.RotationSteps), Is.EqualTo(1));
                    directions.Add(director.RotationSteps);
                }
                else Assert.That(director.RotationSteps, Is.Zero);
                wasRotation = director.Mode == FlowMode.Rotation;
            }
            CollectionAssert.AreEquivalent(new[] { -1, 1 }, directions);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(-2f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(0.4f, 0.4f)]
        public void BreatherBonusIsFiniteAndBounded(float configured, float expected)
        {
            var settings = new FlowSettings { BreatherExtraSeconds = configured };
            var director = new FlowDirector(new ConstantRandom(), settings);
            bool sawBreather = false;
            for (int score = 1; score <= 40; score++)
            {
                director.Advance(score);
                Assert.That(director.ExtraSeconds, Is.InRange(0f, 1f));
                if (director.Mode != FlowMode.Breather) continue;
                sawBreather = true;
                Assert.That(director.ExtraSeconds, Is.EqualTo(expected));
            }
            Assert.That(sawBreather, Is.True);
        }

        [Test]
        public void InvalidSettingsAndConstantRandomCannotHangOrEscapeBounds()
        {
            var settings = new FlowSettings
            {
                StartScore = -1, DriftStartScore = -100, RotationStartScore = -200,
                MinMatches = int.MaxValue, MaxMatches = -1,
                MinCalmMatches = 0, MaxCalmMatches = -1,
                BreatherCooldownMatches = -10, BreatherExtraSeconds = float.NaN
            };
            var director = new FlowDirector(new ConstantRandom(), settings);
            FlowMode previous = FlowMode.Steady;
            int duration = 0;
            for (int score = 1; score <= 200; score++)
            {
                director.Advance(score);
                if (director.Mode == previous && director.Mode != FlowMode.Steady) duration++;
                else duration = 1;
                Assert.That(duration, Is.LessThanOrEqualTo(8));
                Assert.That(director.ExtraSeconds, Is.Zero);
                previous = director.Mode;
            }
        }

        [Test]
        public void ReversedThresholdsAreOrderedWithoutMutatingSettings()
        {
            var settings = new FlowSettings { StartScore = 10, DriftStartScore = 2, RotationStartScore = 1 };
            var director = new FlowDirector(new ConstantRandom(), settings);
            for (int score = 1; score < 10; score++)
            {
                director.Advance(score);
                Assert.That(director.Mode, Is.EqualTo(FlowMode.Steady));
            }
            Assert.That(settings.DriftStartScore, Is.EqualTo(2));
            Assert.That(settings.RotationStartScore, Is.EqualTo(1));
        }

        [Test]
        public void BlockedEarlyAlternativesRemainCalmInsteadOfRepeatingOrIgnoringCooldown()
        {
            var settings = new FlowSettings
            {
                StartScore = 1, DriftStartScore = 100, RotationStartScore = 100,
                MinMatches = 1, MaxMatches = 1, MinCalmMatches = 1, MaxCalmMatches = 1,
                BreatherCooldownMatches = 10
            };
            var director = new FlowDirector(new ConstantRandom(), settings);
            director.Advance(1);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Floating));
            director.Advance(2);
            director.Advance(3);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Breather));
            director.Advance(4);
            director.Advance(5);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Floating));
            for (int score = 6; score < 13; score++)
            {
                director.Advance(score);
                Assert.That(director.Mode, Is.EqualTo(FlowMode.Steady));
            }
            director.Advance(13);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Breather));
        }

        [Test]
        public void ResetClearsEpisodeCooldownAndAwardedScoreHistory()
        {
            var director = new FlowDirector(new ConstantRandom());
            var initial = new List<FlowMode>();
            for (int score = 1; score <= 50; score++)
            {
                director.Advance(score);
                initial.Add(director.Mode);
            }
            director.Reset();
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Steady));
            Assert.That(director.RotationSteps, Is.Zero);
            Assert.That(director.ExtraSeconds, Is.Zero);
            for (int score = 1; score <= 50; score++)
            {
                director.Advance(score);
                Assert.That(director.Mode, Is.EqualTo(initial[score - 1]));
            }
        }

        [Test]
        public void DuplicateOrStaleAwardsDoNotAdvanceEpisode()
        {
            var director = new FlowDirector(new ConstantRandom());
            director.Advance(5);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Floating));
            for (int i = 0; i < 10; i++)
            {
                director.Advance(5);
                director.Advance(-1);
            }
            director.Advance(6);
            Assert.That(director.Mode, Is.EqualTo(FlowMode.Floating));
        }

        private sealed class ConstantRandom : Random
        {
            public override int Next(int maxValue) => 0;
            public override int Next(int minValue, int maxValue) => minValue;
        }
    }
}

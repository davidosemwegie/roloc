using System;
using NUnit.Framework;
using Roloc.Core;
using UnityEngine;

namespace Roloc.Tests
{
    public sealed class ModeRulesTests
    {
        static GameSession Flow(bool variations = false)
        {
            var game = new GameSession(GameMode.Flow, BoardStyle.Lively, new System.Random(42), variationsEnabled: variations);
            game.StartGame();
            return game;
        }

        static void Matches(GameSession game, int count, bool perfect = false)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.That(game.Drop(game.ActiveColor, true, perfect), Is.EqualTo(MatchResult.Matched));
                game.CompleteTransition();
            }
        }

        [TestCase(GameMode.Flow, false)]
        [TestCase(GameMode.Flow, true)]
        [TestCase(GameMode.Rush, false)]
        [TestCase(GameMode.Rush, true)]
        [TestCase(GameMode.Daily, false)]
        [TestCase(GameMode.Daily, true)]
        public void ReleasingInactivePuckIsOneMistakeWithoutScoreOrSequenceAdvance(GameMode mode, bool geometry)
        {
            var game = mode == GameMode.Daily ? GameSession.CreateDaily(42, BoardStyle.Lively)
                : new GameSession(mode, BoardStyle.Lively, new System.Random(42));
            var control = mode == GameMode.Daily ? GameSession.CreateDaily(42, BoardStyle.Lively)
                : new GameSession(mode, BoardStyle.Lively, new System.Random(42));
            game.StartGame(); control.StartGame();
            Matches(game, 2, true); Matches(control, 2, true);
            int active = game.ActiveColor;
            int inactive = (active + 1) % 4;
            var rings = game.RingOrder; var pucks = game.PuckOrder;
            var flow = game.FlowMode;
            var center = game.GetRingCenter(inactive);
            game.TickMilliseconds(100);
            var result = geometry ? game.DropAt(inactive, center.X, center.Y) : game.Drop(inactive, true, true);
            Assert.That(result, Is.EqualTo(mode == GameMode.Flow ? MatchResult.ChanceLost : MatchResult.Failed));
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.InactivePuck));
            Assert.That(game.Chances, Is.EqualTo(mode == GameMode.Flow ? 2 : 0));
            Assert.That(game.Score, Is.EqualTo(2));
            Assert.That(game.PerfectCount, Is.EqualTo(2));
            Assert.That(game.Combo, Is.Zero); Assert.That(game.PerfectStreak, Is.Zero);
            Assert.That(game.BestCombo, Is.EqualTo(2)); Assert.That(game.BestPerfectStreak, Is.EqualTo(2));
            Assert.That(game.LastProgressEarned, Is.Zero); Assert.That(game.LastDropPerfect, Is.False);
            Assert.That(game.ActiveColor, Is.EqualTo(active));
            Assert.That(game.RingOrder, Is.SameAs(rings)); Assert.That(game.PuckOrder, Is.SameAs(pucks));
            Assert.That(game.FlowMode, Is.EqualTo(flow));
            Assert.That(game.DropAt(inactive, center.X, center.Y), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.Drop(active, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.Chances, Is.EqualTo(mode == GameMode.Flow ? 2 : 0));
            if (mode != GameMode.Flow) return;
            Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds));
            game.CompleteTransition();
            Matches(game, 1); Matches(control, 1);
            Assert.That(game.ActiveColor, Is.EqualTo(control.ActiveColor));
            CollectionAssert.AreEqual(control.RingOrder, game.RingOrder);
            CollectionAssert.AreEqual(control.PuckOrder, game.PuckOrder);
            Assert.That(game.FlowMode, Is.EqualTo(control.FlowMode));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TutorialIgnoresInactiveReleaseEvenInsideItsOwnRing(bool geometry)
        {
            var game = Flow(); game.StartTutorial();
            int inactive = (game.ActiveColor + 1) % 4;
            var center = game.GetRingCenter(inactive);
            var result = geometry ? game.DropAt(inactive, center.X, center.Y) : game.Drop(inactive, true, true);
            Assert.That(result, Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.State, Is.EqualTo(RoundState.Tutorial));
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.None));
            Assert.That(game.Chances, Is.EqualTo(3)); Assert.That(game.Score, Is.Zero);
            Assert.That(game.LastProgressEarned, Is.Zero); Assert.That(game.PerfectCount, Is.Zero);
            Assert.That(game.Drop(game.ActiveColor, true), Is.EqualTo(MatchResult.TutorialCompleted));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidColorCannotConsumeAChanceOrIndexGeometry(int color)
        {
            var game = Flow();
            Assert.That(game.Drop(color, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.DropAt(color, 0, 0), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.Chances, Is.EqualTo(3)); Assert.That(game.Score, Is.Zero);
            Assert.That(game.State, Is.EqualTo(RoundState.Playing));
        }

        [Test]
        public void PausedInactiveReleaseCannotConsumeAChance()
        {
            var game = Flow(); game.Pause();
            int inactive = (game.ActiveColor + 1) % 4;
            Assert.That(game.Drop(inactive, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.DropAt(inactive, 0, 0), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.Chances, Is.EqualTo(3));
            game.Resume();
            Assert.That(game.State, Is.EqualTo(RoundState.Playing));
        }

        [TestCase(0, 3.5f)]
        [TestCase(9, 3.5f)]
        [TestCase(10, 3f)]
        [TestCase(24, 3f)]
        [TestCase(25, 2.5f)]
        [TestCase(49, 2.5f)]
        [TestCase(50, 2.25f)]
        public void FlowDifficultyHasGentlerThresholds(int score, float seconds)
        {
            var game = Flow();
            Matches(game, score);
            Assert.That(game.DurationSeconds, Is.EqualTo(seconds));
            var settings = ScriptableObject.CreateInstance<DifficultySettings>();
            try { Assert.That(settings.GetFlowSeconds(score), Is.EqualTo(seconds)); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void FlowGetsThreeChancesAndRetriesTheExactTargetWithoutConsumingRandomness()
        {
            var game = Flow(true);
            var control = Flow(true);
            Matches(game, 20); Matches(control, 20);
            var rings = game.RingOrder;
            var pucks = game.PuckOrder;
            int active = game.ActiveColor;
            var mode = game.FlowMode;
            game.Tick(1);
            Assert.That(game.Drop(active, false), Is.EqualTo(MatchResult.ChanceLost));
            Assert.That(game.Chances, Is.EqualTo(2));
            Assert.That(game.Score, Is.EqualTo(20));
            Assert.That(game.ActiveColor, Is.EqualTo(active));
            Assert.That(game.RingOrder, Is.SameAs(rings));
            Assert.That(game.PuckOrder, Is.SameAs(pucks));
            Assert.That(game.FlowMode, Is.EqualTo(mode));
            Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds));
            Assert.That(game.Combo, Is.Zero);
            Assert.That(game.LastProgressEarned, Is.Zero);
            Assert.That(game.Drop(active, true), Is.EqualTo(MatchResult.Ignored));
            game.CompleteTransition();
            for (int i = 0; i < 50; i++)
            {
                Matches(game, 1); Matches(control, 1);
                Assert.That(game.ActiveColor, Is.EqualTo(control.ActiveColor));
                Assert.That(game.FlowMode, Is.EqualTo(control.FlowMode));
                CollectionAssert.AreEqual(control.RingOrder, game.RingOrder);
                CollectionAssert.AreEqual(control.PuckOrder, game.PuckOrder);
            }
        }

        [Test]
        public void RecoverableTimeoutSignalsChanceLostAndLastTimeoutEndsOnlyOnce()
        {
            var game = Flow();
            for (int chance = 2; chance >= 0; chance--)
            {
                var result = game.TickMilliseconds(game.DurationMilliseconds);
                Assert.That(result, Is.EqualTo(chance == 0 ? MatchResult.Failed : MatchResult.ChanceLost));
                Assert.That(game.LastFailure, Is.EqualTo(DropFailure.TimeExpired));
                Assert.That(game.Chances, Is.EqualTo(chance));
                Assert.That(game.TickResult(100), Is.EqualTo(MatchResult.Ignored));
                game.CompleteTransition();
            }
            Assert.That(game.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void RecoveryNeedsFifteenCleanMatchesAndIsLimitedToTwice()
        {
            var game = Flow();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(game.Drop(game.ActiveColor, false), Is.EqualTo(MatchResult.ChanceLost));
                game.CompleteTransition();
                Matches(game, 14);
                Assert.That(game.Chances, Is.EqualTo(2));
                Matches(game, 1);
                Assert.That(game.Chances, Is.EqualTo(cycle < 2 ? 3 : 2));
                Assert.That(game.LastChanceRestored, Is.EqualTo(cycle < 2));
                Assert.That(game.RecoveriesUsed, Is.EqualTo(Math.Min(cycle + 1, 2)));
            }
        }

        [Test]
        public void FullHealthDoesNotBankRecoveryOrConsumeAnAllowance()
        {
            var game = Flow();
            Matches(game, 30);
            Assert.That(game.RecoveriesUsed, Is.Zero);
            game.Drop(game.ActiveColor, false); game.CompleteTransition();
            Matches(game, 14);
            Assert.That(game.Chances, Is.EqualTo(2));
            Matches(game, 1);
            Assert.That(game.Chances, Is.EqualTo(3));
            Assert.That(game.RecoveriesUsed, Is.EqualTo(1));
        }

        [Test]
        public void PerfectStreakIsDistinctFromComboAndNeitherChangesScoreWeight()
        {
            var game = Flow();
            Matches(game, 3, true);
            Assert.That(game.Score, Is.EqualTo(3));
            Assert.That(game.PerfectCount, Is.EqualTo(3));
            Assert.That(game.PerfectStreak, Is.EqualTo(3));
            Assert.That(game.LastProgressEarned, Is.EqualTo(2));
            Matches(game, 1);
            Assert.That(game.Combo, Is.EqualTo(4));
            Assert.That(game.PerfectStreak, Is.Zero);
            Assert.That(game.BestPerfectStreak, Is.EqualTo(3));
            Assert.That(game.LastProgressEarned, Is.EqualTo(1));
            Matches(game, 2, true);
            game.Pause(); game.Tick(100); game.Resume();
            Assert.That(game.PerfectStreak, Is.EqualTo(2));
            Assert.That(game.Combo, Is.EqualTo(6));
            game.Drop(game.ActiveColor, false);
            Assert.That(game.Combo, Is.Zero);
            Assert.That(game.PerfectStreak, Is.Zero);
            Assert.That(game.BestCombo, Is.EqualTo(6));
            Assert.That(game.BestPerfectStreak, Is.EqualTo(3));
        }

        [Test]
        public void TutorialAndRestartClearSkillAndNeverGrantProgress()
        {
            var game = Flow(); Matches(game, 4, true);
            game.StartTutorial(); game.Tick(100);
            Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Steady));
            Assert.That(game.Drop(game.ActiveColor, true, true), Is.EqualTo(MatchResult.TutorialCompleted));
            Assert.That(game.LastProgressEarned, Is.Zero);
            Assert.That(game.PerfectCount, Is.Zero);
            Assert.That(game.Score, Is.Zero);
            game.StartGame();
            Assert.That(game.Chances, Is.EqualTo(3));
            Assert.That(game.BestCombo, Is.Zero);
            Assert.That(game.BestPerfectStreak, Is.Zero);
        }

        [Test]
        public void FlowOpeningIsTenCalmMatchesThenChallengesAndTwoRecoveryMatches()
        {
            var game = Flow(true);
            for (int score = 0; score < 10; score++)
            {
                Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Steady));
                Matches(game, 1);
            }
            Assert.That(game.RhythmPhase, Is.EqualTo(RhythmPhase.Challenge));
            int challengeLength = 0;
            while (game.RhythmPhase == RhythmPhase.Challenge) { challengeLength++; Matches(game, 1); }
            Assert.That(challengeLength, Is.InRange(3, 5));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Breather));
                Assert.That(game.DurationSeconds, Is.EqualTo(DefaultRules.FlowSecondsForScore(game.Score) + .65f));
                Matches(game, 1);
            }
            Assert.That(game.RhythmPhase, Is.EqualTo(RhythmPhase.Calm));
        }

        [Test]
        public void RhythmsHaveBoundedPhasesNoRepeatedChallengesAndSingleRotationEntry()
        {
            var director = new RhythmDirector(new System.Random(42), GameMode.Rush); director.Reset();
            var phase = director.Phase; int length = 0;
            var lastChallenge = FlowMode.Steady;
            for (int score = 0; score < 500; score++)
            {
                length++;
                director.Advance(score + 1);
                if (director.Phase == phase)
                {
                    Assert.That(director.RotationSteps, Is.Zero);
                    continue;
                }
                if (phase == RhythmPhase.Recovery) Assert.That(length, Is.EqualTo(2));
                else Assert.That(length, Is.InRange(3, 5));
                length = 0; phase = director.Phase;
                if (phase != RhythmPhase.Challenge) continue;
                Assert.That(director.Mode, Is.Not.EqualTo(lastChallenge));
                lastChallenge = director.Mode;
                if (director.Mode == FlowMode.Rotation) Assert.That(Math.Abs(director.RotationSteps), Is.EqualTo(1));
            }
        }
    }
}

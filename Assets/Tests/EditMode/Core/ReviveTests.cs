using System;
using NUnit.Framework;
using Roloc.Core;

namespace Roloc.Tests
{
    public sealed class ReviveTests
    {
        static GameSession NewGame()
        {
            var game = new GameSession(new Random(42));
            game.StartGame();
            return game;
        }

        static void Reach(GameSession game, int score)
        {
            while (game.Score < score)
            {
                Assert.That(game.Drop(game.ActiveColor, true, true), Is.EqualTo(MatchResult.Matched));
                game.CompleteTransition();
            }
        }

        static void Fail(GameSession game)
            => Assert.That(game.Drop(game.ActiveColor, false), Is.EqualTo(MatchResult.Failed));

        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(21, 1)]
        [TestCase(49, 1)]
        [TestCase(50, 2)]
        [TestCase(51, 2)]
        [TestCase(99, 2)]
        [TestCase(100, 3)]
        [TestCase(149, 3)]
        [TestCase(150, 3)]
        [TestCase(250, 3)]
        public void BankEarnsAtMilestonesAndCapsAtThree(int score, int expected)
        {
            var game = NewGame();
            Reach(game, score);
            Assert.That(game.RevivesAvailable, Is.EqualTo(expected));
        }

        [Test]
        public void OverflowIsDiscardedAndOnlyNewMilestonesReplenish()
        {
            var game = NewGame();
            Reach(game, 150);
            Fail(game);
            Assert.That(game.ApplyRewardedRevive(), Is.True);
            game.CompleteTransition();
            Reach(game, 199);
            Assert.That(game.RevivesAvailable, Is.EqualTo(2));
            Reach(game, 200);
            Assert.That(game.RevivesAvailable, Is.EqualTo(3));
        }

        [Test]
        public void RepeatedFailureAtOneScoreCannotEarnAnotherRevive()
        {
            var game = NewGame();
            Reach(game, 20);
            Fail(game);
            Assert.That(game.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(game.ApplyRewardedRevive(), Is.True);
            Assert.That(game.ApplyRewardedRevive(), Is.False);
            Assert.That(game.RevivesAvailable, Is.Zero);
            game.CompleteTransition();
            Fail(game);
            Assert.That(game.Score, Is.EqualTo(20));
            Assert.That(game.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.ApplyRewardedRevive(), Is.False);
        }

        [Test]
        public void RewardPreservesScoreAndComboButResetsPerfectStreakAndFailure()
        {
            var game = NewGame();
            Reach(game, 20);
            Fail(game);
            Assert.That(game.ComboBeforeFailure, Is.EqualTo(20));
            Assert.That(game.Combo, Is.Zero);
            Assert.That(game.PerfectStreak, Is.Zero);
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.MissedRing));
            Assert.That(game.ApplyRewardedRevive(), Is.True);
            Assert.That(game.Score, Is.EqualTo(20));
            Assert.That(game.Combo, Is.EqualTo(20));
            Assert.That(game.BestCombo, Is.EqualTo(20));
            Assert.That(game.PerfectCount, Is.EqualTo(20));
            Assert.That(game.PerfectStreak, Is.Zero);
            Assert.That(game.BestPerfectStreak, Is.EqualTo(20));
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.None));
            Assert.That(game.LastResult, Is.EqualTo(MatchResult.Ignored));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingDecisionAndCountdownFreezeTimerAndRewardRestoresFullRound(bool timeout)
        {
            var game = NewGame();
            Reach(game, 20);
            game.TickMilliseconds(731);
            if (timeout) Assert.That(game.TickMilliseconds(game.DurationMilliseconds), Is.EqualTo(MatchResult.Failed));
            else Fail(game);
            int elapsed = game.RoundElapsedMilliseconds;
            Assert.That(game.TickMilliseconds(100000), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.Drop(game.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            game.Pause(); game.Resume();
            Assert.That(game.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(game.RoundElapsedMilliseconds, Is.EqualTo(elapsed));
            Assert.That(game.ApplyRewardedRevive(), Is.True);
            Assert.That(game.State, Is.EqualTo(RoundState.Transition));
            Assert.That(game.RequiredTransitionMilliseconds, Is.EqualTo(3000));
            game.TickMilliseconds(100000);
            Assert.That(game.RoundElapsedMilliseconds, Is.Zero);
            Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds));
            game.CompleteTransition();
            Assert.That(game.TickMilliseconds(game.DurationMilliseconds - 1), Is.EqualTo(MatchResult.Ignored));
            Assert.That(game.TickMilliseconds(1), Is.EqualTo(MatchResult.Failed));
        }

        [Test]
        public void ReviveRetriesSameBoardAndDoesNotConsumeDailyRandomness()
        {
            var revived = GameSession.CreateDaily(42, BoardStyle.Lively, 2);
            var control = GameSession.CreateDaily(42, BoardStyle.Lively, 2);
            revived.StartGame(); control.StartGame();
            Reach(revived, 20); Reach(control, 20);
            var rings = revived.RingOrder;
            var pucks = revived.PuckOrder;
            Fail(revived);
            Assert.That(revived.ApplyRewardedRevive(), Is.True);
            Assert.That(revived.RingOrder, Is.SameAs(rings));
            Assert.That(revived.PuckOrder, Is.SameAs(pucks));
            revived.CompleteTransition();
            for (int score = 20; score < 110; score++)
            {
                Assert.That(revived.ActiveColor, Is.EqualTo(control.ActiveColor));
                CollectionAssert.AreEqual(control.RingOrder, revived.RingOrder);
                CollectionAssert.AreEqual(control.PuckOrder, revived.PuckOrder);
                Assert.That(revived.FlowMode, Is.EqualTo(control.FlowMode));
                Assert.That(revived.RhythmPhase, Is.EqualTo(control.RhythmPhase));
                Assert.That(revived.DurationMilliseconds, Is.EqualTo(control.DurationMilliseconds));
                for (int color = 0; color < 4; color++)
                {
                    Assert.That(revived.GetRingCenter(color), Is.EqualTo(control.GetRingCenter(color)));
                    Assert.That(revived.GetPuckHome(color), Is.EqualTo(control.GetPuckHome(color)));
                }
                Reach(revived, score + 1); Reach(control, score + 1);
            }
        }

        [Test]
        public void DecliningKeepsFailureAndRejectsLateRewards()
        {
            var game = NewGame();
            Reach(game, 50);
            Fail(game);
            game.EndRun(); game.EndRun();
            Assert.That(game.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.MissedRing));
            Assert.That(game.LastResult, Is.EqualTo(MatchResult.Failed));
            Assert.That(game.RevivesAvailable, Is.EqualTo(2));
            Assert.That(game.ApplyRewardedRevive(), Is.False);
        }

        [Test]
        public void EveryRestartAndTutorialClearsTheBank()
        {
            var game = NewGame();
            Reach(game, 100);
            game.ReturnToMenu();
            Assert.That(game.RevivesAvailable, Is.Zero);
            game.StartGame();
            Reach(game, 20);
            game.StartGame();
            Assert.That(game.RevivesAvailable, Is.Zero);
            Assert.That(game.ComboBeforeFailure, Is.Zero);
            Reach(game, 20);
            game.StartTutorial();
            Assert.That(game.RevivesEnabled, Is.False);
            Assert.That(game.RevivesAvailable, Is.Zero);
            game.StartGame();
            Assert.That(game.RevivesEnabled, Is.True);
        }

        [Test]
        public void FlowSpendsNormalChancesFirstAndDoesNotResetRecoveryQuota()
        {
            var game = new GameSession(GameMode.Flow, BoardStyle.Still, new Random(42), variationsEnabled: false);
            game.StartGame();
            Reach(game, 20);
            Assert.That(game.Drop(game.ActiveColor, false), Is.EqualTo(MatchResult.ChanceLost));
            game.CompleteTransition(); Reach(game, 35);
            Assert.That(game.RecoveriesUsed, Is.EqualTo(1));
            Assert.That(game.Drop(game.ActiveColor, false), Is.EqualTo(MatchResult.ChanceLost));
            game.CompleteTransition(); Reach(game, 50);
            Assert.That(game.RecoveriesUsed, Is.EqualTo(2));
            for (int chance = 2; chance >= 1; chance--)
            {
                Assert.That(game.Drop(game.ActiveColor, false), Is.EqualTo(MatchResult.ChanceLost));
                Assert.That(game.Chances, Is.EqualTo(chance));
                Assert.That(game.RevivesAvailable, Is.EqualTo(2));
                game.CompleteTransition();
            }
            Reach(game, 54);
            Fail(game);
            Assert.That(game.ApplyRewardedRevive(), Is.True);
            Assert.That(game.Chances, Is.EqualTo(1));
            Assert.That(game.Combo, Is.EqualTo(4));
            Assert.That(game.RecoveriesUsed, Is.EqualTo(2));
            game.CompleteTransition(); Reach(game, 69);
            Assert.That(game.Chances, Is.EqualTo(1));
            Assert.That(game.RecoveriesUsed, Is.EqualTo(2));
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void DailyRevivesRequireVersionTwo(int version, bool enabled)
        {
            var game = GameSession.CreateDaily(42, BoardStyle.Still, version);
            game.StartGame(); Reach(game, 20);
            Assert.That(game.DailyRulesVersion, Is.EqualTo(version));
            Assert.That(game.RevivesEnabled, Is.EqualTo(enabled));
            Assert.That(game.RevivesAvailable, Is.EqualTo(enabled ? 1 : 0));
            Fail(game);
            Assert.That(game.State, Is.EqualTo(enabled ? RoundState.AwaitingRevive : RoundState.GameOver));
        }

        [TestCase(0)]
        [TestCase(3)]
        public void UnknownDailyVersionsAreRejected(int version)
            => Assert.Throws<ArgumentOutOfRangeException>(() => GameSession.CreateDaily(42, BoardStyle.Still, version));
    }
}

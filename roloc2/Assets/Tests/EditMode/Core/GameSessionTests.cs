using System;
using NUnit.Framework;
using Roloc.Core;
using UnityEngine;

namespace Roloc.Tests
{
    public sealed class GameSessionTests
    {
        [TestCase(0, 3f)]
        [TestCase(4, 3f)]
        [TestCase(5, 2.5f)]
        [TestCase(19, 2.5f)]
        [TestCase(20, 2f)]
        [TestCase(39, 2f)]
        [TestCase(40, 1.75f)]
        [TestCase(100, 1.75f)]
        public void DifficultyChangesAtAwardedScore(int score, float seconds)
        {
            Assert.That(DefaultRules.SecondsForScore(score), Is.EqualTo(seconds));
            var session = NewGame();
            Award(session, score);
            Assert.That(session.DurationSeconds, Is.EqualTo(seconds));
        }

        [TestCase(30, false)]
        [TestCase(31, false)]
        [TestCase(35, true)]
        [TestCase(60, true)]
        [TestCase(61, false)]
        [TestCase(62, true)]
        [TestCase(65, true)]
        [TestCase(79, false)]
        [TestCase(80, true)]
        [TestCase(81, true)]
        [TestCase(83, true)]
        public void ShuffleUsesUnionOfThresholds(int score, bool expected)
        {
            Assert.That(DefaultRules.ShouldShuffle(score), Is.EqualTo(expected));
        }

        [TestCase(39, false)]
        [TestCase(40, false)]
        [TestCase(41, false)]
        [TestCase(44, false)]
        [TestCase(45, true)]
        [TestCase(46, false)]
        [TestCase(50, true)]
        public void PucksShuffleEveryFiveMatchesAboveForty(int score, bool expected)
        {
            Assert.That(DefaultRules.ShouldShufflePucks(score), Is.EqualTo(expected));
        }

        [Test]
        public void SettingsDefaultsMatchStandaloneRules()
        {
            var settings = ScriptableObject.CreateInstance<DifficultySettings>();
            try
            {
                for (int score = 0; score <= 100; score++)
                {
                    Assert.That(settings.GetSeconds(score), Is.EqualTo(DefaultRules.SecondsForScore(score)));
                    Assert.That(settings.IsShuffleScore(score), Is.EqualTo(DefaultRules.ShouldShuffle(score)));
                    Assert.That(settings.IsPuckShuffleScore(score), Is.EqualTo(DefaultRules.ShouldShufflePucks(score)));
                }
                Assert.That(settings.TransitionSeconds, Is.EqualTo(0.24f));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void TimeoutEndsExactlyOnceAndRejectsLateDrop()
        {
            var session = NewGame();
            Assert.That(session.Tick(2.99f), Is.False);
            Assert.That(session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(session.Tick(0.02f), Is.True);
            Assert.That(session.RemainingSeconds, Is.Zero);
            Assert.That(session.Tick(1f), Is.False);
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.Score, Is.Zero);
        }

        [Test]
        public void ExactTimerBoundaryExpires()
        {
            var session = NewGame();
            Assert.That(session.Tick(session.DurationSeconds), Is.True);
            Assert.That(session.State, Is.EqualTo(RoundState.GameOver));
        }

        [Test]
        public void InactivePucksAreIgnoredAndWrongActiveDropEndsOnce()
        {
            var session = NewGame();
            Assert.That(session.Drop((session.ActiveColor + 1) % 4, false), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(session.Drop(session.ActiveColor, false), Is.EqualTo(MatchResult.Failed));
            Assert.That(session.Drop(session.ActiveColor, false), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.Tick(100f), Is.False);
        }

        [Test]
        public void RepeatedActiveColorResetsTimerAndTransitionBlocksDuplicateMatch()
        {
            var session = new GameSession(new ZeroRandom());
            session.StartGame();
            int color = session.ActiveColor;
            session.Tick(2f);
            Assert.That(session.Drop(color, true), Is.EqualTo(MatchResult.Matched));
            Assert.That(session.ActiveColor, Is.EqualTo(color));
            Assert.That(session.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(session.Drop(color, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(session.Score, Is.EqualTo(1));
            session.CompleteTransition();
            Assert.That(session.Tick(1f), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void RingAndPuckShufflesFollowIndependentAwardedScoreRules()
        {
            Func<int, bool> shufflePucks = score => score % 3 == 0;
            var session = new GameSession(new System.Random(1234), shouldShufflePucks: shufflePucks);
            session.StartGame();
            for (int score = 1; score <= 100; score++)
            {
                var previousRings = session.RingOrder;
                var previousPucks = session.PuckOrder;
                Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
                Assert.That(ReferenceEquals(previousRings, session.RingOrder), Is.EqualTo(!DefaultRules.ShouldShuffle(score)),
                    "Ring shuffle at score " + score);
                Assert.That(ReferenceEquals(previousPucks, session.PuckOrder), Is.EqualTo(!shufflePucks(score)),
                    "Puck shuffle at score " + score);
                if (shufflePucks(score)) CollectionAssert.AreNotEqual(previousPucks, session.PuckOrder);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, session.RingOrder);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, session.PuckOrder);
                session.CompleteTransition();
            }
        }

        [Test]
        public void DefaultPuckShuffleUsesDefaultRules()
        {
            var session = NewGame();
            for (int score = 1; score <= 100; score++)
            {
                var previousPucks = session.PuckOrder;
                session.Drop(session.ActiveColor, true);
                Assert.That(ReferenceEquals(previousPucks, session.PuckOrder),
                    Is.EqualTo(!DefaultRules.ShouldShufflePucks(score)), "Puck shuffle at score " + score);
                session.CompleteTransition();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PuckShuffleAlwaysChangesOrderWithoutMutatingPreviousBoard(bool constantRandom)
        {
            System.Random random = constantRandom ? new ZeroRandom() : new System.Random(1234);
            var session = new GameSession(random, shouldShuffle: _ => false, shouldShufflePucks: _ => true);
            session.StartGame();
            var rings = session.RingOrder;
            for (int i = 0; i < 20; i++)
            {
                var previous = session.PuckOrder;
                var snapshot = (int[])previous.Clone();
                Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
                CollectionAssert.AreEqual(snapshot, previous);
                CollectionAssert.AreNotEqual(previous, session.PuckOrder);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, session.PuckOrder);
                Assert.That(session.RingOrder, Is.SameAs(rings));
                session.CompleteTransition();
            }
        }

        [Test]
        public void InjectedPuckShuffleReceivesAwardedScoreOnlyForSuccessfulGameMatches()
        {
            var queries = new System.Collections.Generic.List<int>();
            var session = new GameSession(new ZeroRandom(), shouldShufflePucks: score =>
            {
                queries.Add(score);
                return score == 2;
            });
            session.StartGame();
            var initialPucks = session.PuckOrder;
            Award(session, 1);
            Assert.That(session.PuckOrder, Is.SameAs(initialPucks));
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
            Assert.That(session.PuckOrder, Is.Not.SameAs(initialPucks));
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            CollectionAssert.AreEqual(new[] { 1, 2 }, queries);
        }

        [Test]
        public void TutorialWrongDropAndTimeoutDoNotShufflePucks()
        {
            int queries = 0;
            var session = new GameSession(new ZeroRandom(), shouldShufflePucks: _ =>
            {
                queries++;
                return true;
            });
            session.StartTutorial();
            var tutorialPucks = session.PuckOrder;
            session.Drop(session.ActiveColor, false);
            session.Tick(100f);
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.TutorialCompleted));
            Assert.That(session.PuckOrder, Is.SameAs(tutorialPucks));

            session.StartGame();
            var gamePucks = session.PuckOrder;
            session.Drop((session.ActiveColor + 1) % 4, true);
            Assert.That(session.Drop(session.ActiveColor, false), Is.EqualTo(MatchResult.Failed));
            Assert.That(session.PuckOrder, Is.SameAs(gamePucks));

            session.StartGame();
            var timeoutPucks = session.PuckOrder;
            Assert.That(session.Tick(session.DurationSeconds), Is.True);
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.PuckOrder, Is.SameAs(timeoutPucks));
            Assert.That(queries, Is.Zero);
        }

        [Test]
        public void ShuffledPucksAndFreshTimerSurvivePausedTransition()
        {
            var session = new GameSession(new ZeroRandom(), shouldShufflePucks: _ => true);
            session.StartGame();
            int color = session.ActiveColor;
            session.Tick(2f);
            Assert.That(session.Drop(color, true), Is.EqualTo(MatchResult.Matched));
            var pucks = session.PuckOrder;
            var snapshot = (int[])pucks.Clone();
            Assert.That(session.ActiveColor, Is.EqualTo(color));
            Assert.That(session.RemainingSeconds, Is.EqualTo(session.DurationSeconds));
            session.Pause();
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.Drop(color, true), Is.EqualTo(MatchResult.Ignored));
            session.CompleteTransition();
            session.Resume();
            Assert.That(session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.PuckOrder, Is.SameAs(pucks));
            CollectionAssert.AreEqual(snapshot, session.PuckOrder);
            Assert.That(session.RemainingSeconds, Is.EqualTo(session.DurationSeconds));
            session.CompleteTransition();
            Assert.That(session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(session.Tick(1f), Is.False);
            Assert.That(session.RemainingSeconds, Is.EqualTo(session.DurationSeconds - 1f));
        }

        [Test]
        public void StartingAgainClearsRunAndRandomizesValidBoards()
        {
            var session = NewGame();
            Award(session, 12);
            var oldRings = session.RingOrder;
            var oldPucks = session.PuckOrder;
            session.StartGame();
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(session.RemainingSeconds, Is.EqualTo(3f));
            Assert.That(session.ActiveColor, Is.InRange(0, 3));
            Assert.That(session.RingOrder, Is.Not.SameAs(oldRings));
            Assert.That(session.PuckOrder, Is.Not.SameAs(oldPucks));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, session.RingOrder);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, session.PuckOrder);
        }

        [Test]
        public void TutorialIsUntimedAllowsRetryAndNeverAwardsScore()
        {
            var session = new GameSession();
            session.StartTutorial();
            Assert.That(session.WasTutorial, Is.True);
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.Drop(session.ActiveColor, false), Is.EqualTo(MatchResult.Ignored));
            Assert.That(session.State, Is.EqualTo(RoundState.Tutorial));
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.TutorialCompleted));
            Assert.That(session.State, Is.EqualTo(RoundState.Menu));
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            session.StartGame();
            Assert.That(session.WasTutorial, Is.False);
        }

        [TestCase(RoundState.Playing)]
        [TestCase(RoundState.Tutorial)]
        [TestCase(RoundState.Transition)]
        public void RepeatedPauseAndResumeRestoreOriginalState(RoundState state)
        {
            var session = NewGame();
            if (state == RoundState.Tutorial) session.StartTutorial();
            if (state == RoundState.Transition) session.Drop(session.ActiveColor, true);
            float remaining = session.RemainingSeconds;
            session.Pause();
            session.Pause();
            Assert.That(session.State, Is.EqualTo(RoundState.Paused));
            Assert.That(session.Tick(100f), Is.False);
            Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            session.CompleteTransition();
            Assert.That(session.State, Is.EqualTo(RoundState.Paused));
            session.Resume();
            session.Resume();
            Assert.That(session.State, Is.EqualTo(state));
            Assert.That(session.RemainingSeconds, Is.EqualTo(remaining));
        }

        [Test]
        public void ReturningToMenuCannotResumeAbandonedRun()
        {
            var session = NewGame();
            Award(session, 3);
            session.Pause();
            session.ReturnToMenu();
            session.Resume();
            session.CompleteTransition();
            Assert.That(session.State, Is.EqualTo(RoundState.Menu));
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.Tick(100f), Is.False);
        }

        [Test]
        public void InjectedDifficultyReceivesCurrentAwardedScore()
        {
            int queriedScore = -1;
            var session = new GameSession(new System.Random(1), score => 5f - score,
                score => { queriedScore = score; return true; });
            session.StartGame();
            session.Drop(session.ActiveColor, true);
            Assert.That(queriedScore, Is.EqualTo(1));
            Assert.That(session.DurationSeconds, Is.EqualTo(4f));
        }

        private static GameSession NewGame()
        {
            var session = new GameSession(new System.Random(1234));
            session.StartGame();
            return session;
        }

        private static void Award(GameSession session, int points)
        {
            for (int i = 0; i < points; i++)
            {
                Assert.That(session.Drop(session.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
                session.CompleteTransition();
            }
        }

        private sealed class ZeroRandom : System.Random
        {
            public override int Next(int maxValue) => 0;
        }
    }
}

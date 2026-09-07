using System;
using System.IO;
using NUnit.Framework;
using Roloc.Core;
using UnityEngine;

namespace Roloc.Tests
{
    public sealed class DailyRulesTests
    {
        [Serializable] sealed class Fixture
        {
            public int rulesVersion, seed, elapsedMs;
            public string variant;
            public RoundFixture[] rounds;
        }

        [Serializable] sealed class RoundFixture
        {
            public int round, activeColor, flowMode, rhythmPhase, rotationSteps, durationMs, transitionMs;
            public int[] ringOrder, puckOrder;
            public BoardPoint[] rings, pucks;
        }

        [Test]
        public void PublishedFixtureMatchesEveryRoundAndMovingCoordinate()
        {
            string path = Path.Combine(Application.dataPath, "Tests/EditMode/Core/DailyFixtures.json");
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.That(fixture.rulesVersion, Is.EqualTo(DailyRules.Version));
            var game = GameSession.CreateDaily((uint)fixture.seed, BoardStyle.Lively); game.StartGame();
            foreach (var round in fixture.rounds)
            {
                game.TickMilliseconds(fixture.elapsedMs);
                Assert.That(game.Score, Is.EqualTo(round.round));
                Assert.That(game.ActiveColor, Is.EqualTo(round.activeColor), "Active at round " + round.round);
                CollectionAssert.AreEqual(round.ringOrder, game.RingOrder);
                CollectionAssert.AreEqual(round.puckOrder, game.PuckOrder);
                Assert.That((int)game.FlowMode, Is.EqualTo(round.flowMode));
                Assert.That((int)game.RhythmPhase, Is.EqualTo(round.rhythmPhase));
                Assert.That(game.RotationSteps, Is.EqualTo(round.rotationSteps));
                Assert.That(game.DurationMilliseconds, Is.EqualTo(round.durationMs));
                Assert.That(game.RequiredTransitionMilliseconds, Is.EqualTo(round.transitionMs));
                for (int color = 0; color < 4; color++)
                {
                    Assert.That(game.GetRingCenter(color), Is.EqualTo(round.rings[color]));
                    Assert.That(game.GetPuckHome(color), Is.EqualTo(round.pucks[color]));
                }
                var target = game.GetRingCenter(game.ActiveColor);
                Assert.That(game.DropAt(game.ActiveColor, target.X, target.Y), Is.EqualTo(MatchResult.Matched));
                game.CompleteTransition();
            }
        }

        [TestCase(0u)]
        [TestCase(42u)]
        [TestCase(uint.MaxValue)]
        public void EveryRetryRestartsTheEntirePublishedSequence(uint seed)
        {
            var restarted = GameSession.CreateDaily(seed, BoardStyle.Lively); restarted.StartGame();
            for (int i = 0; i < 87; i++) { restarted.Drop(restarted.ActiveColor, true); restarted.CompleteTransition(); }
            restarted.ReturnToMenu(); restarted.StartGame();
            var fresh = GameSession.CreateDaily(seed, BoardStyle.Lively); fresh.StartGame();
            for (int i = 0; i < 150; i++)
            {
                Assert.That(restarted.ActiveColor, Is.EqualTo(fresh.ActiveColor));
                CollectionAssert.AreEqual(fresh.RingOrder, restarted.RingOrder);
                CollectionAssert.AreEqual(fresh.PuckOrder, restarted.PuckOrder);
                Assert.That(restarted.DurationMilliseconds, Is.EqualTo(fresh.DurationMilliseconds));
                Assert.That(restarted.FlowMode, Is.EqualTo(fresh.FlowMode));
                Assert.That(restarted.RequiredTransitionMilliseconds, Is.EqualTo(fresh.RequiredTransitionMilliseconds));
                restarted.Drop(restarted.ActiveColor, true); restarted.CompleteTransition();
                fresh.Drop(fresh.ActiveColor, true); fresh.CompleteTransition();
            }
        }

        [TestCase(22225, true, true)]
        [TestCase(22226, true, false)]
        [TestCase(63500, true, false)]
        [TestCase(63501, false, false)]
        public void CanonicalBoundariesAreInclusiveAndIndependentOfVisualScale(int offset, bool valid, bool perfect)
        {
            var game = GameSession.CreateDaily(42, BoardStyle.Lively); game.StartGame();
            var center = game.GetRingCenter(game.ActiveColor);
            var result = game.DropAt(game.ActiveColor, center.X + offset, center.Y);
            Assert.That(result, Is.EqualTo(valid ? MatchResult.Matched : MatchResult.Failed));
            Assert.That(game.LastDropPerfect, Is.EqualTo(perfect));
            Assert.That(game.LastProgressEarned, Is.EqualTo(valid ? perfect ? 2 : 1 : 0));
        }

        [Test]
        public void WrongRingAndMissedRingProduceDifferentReasons()
        {
            var game = GameSession.CreateDaily(12, BoardStyle.Still); game.StartGame();
            var other = game.GetRingCenter((game.ActiveColor + 1) % 4);
            game.DropAt(game.ActiveColor, other.X, other.Y);
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.WrongRing));
            game.StartGame(); game.DropAt(game.ActiveColor, 0, 0);
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.MissedRing));
        }

        [Test]
        public void IntegerTimerExpiresAtDeadlineAndIgnoresPausedAndTransitionTime()
        {
            var game = GameSession.CreateDaily(42, BoardStyle.Lively); game.StartGame();
            game.TickMilliseconds(2999);
            Assert.That(game.RoundElapsedMilliseconds, Is.EqualTo(2999));
            game.Pause(); game.TickMilliseconds(9999); game.Resume();
            Assert.That(game.RoundElapsedMilliseconds, Is.EqualTo(2999));
            Assert.That(game.TickMilliseconds(1), Is.EqualTo(MatchResult.Failed));
            Assert.That(game.LastFailure, Is.EqualTo(DropFailure.TimeExpired));
            Assert.That(game.Drop(game.ActiveColor, true), Is.EqualTo(MatchResult.Ignored));
            game.StartGame(); game.Drop(game.ActiveColor, true);
            game.TickMilliseconds(99999);
            Assert.That(game.RoundElapsedMilliseconds, Is.Zero);
        }

        [Test]
        public void StillHasIdenticalSequenceButNoContinuousMovement()
        {
            var still = GameSession.CreateDaily(42, BoardStyle.Still); still.StartGame();
            var lively = GameSession.CreateDaily(42, BoardStyle.Lively); lively.StartGame();
            bool sawMovement = false;
            for (int i = 0; i < 100; i++)
            {
                var beforeRing = still.GetRingCenter(still.ActiveColor);
                var inactive = (still.ActiveColor + 1) % 4;
                var beforePuck = still.GetPuckHome(inactive);
                still.TickMilliseconds(1370); lively.TickMilliseconds(1370);
                Assert.That(still.GetRingCenter(still.ActiveColor), Is.EqualTo(beforeRing));
                Assert.That(still.GetPuckHome(inactive), Is.EqualTo(beforePuck));
                Assert.That(still.ActiveColor, Is.EqualTo(lively.ActiveColor));
                CollectionAssert.AreEqual(still.RingOrder, lively.RingOrder);
                CollectionAssert.AreEqual(still.PuckOrder, lively.PuckOrder);
                sawMovement |= !beforeRing.Equals(lively.GetRingCenter(lively.ActiveColor))
                    || !beforePuck.Equals(lively.GetPuckHome(inactive));
                still.Drop(still.ActiveColor, true); still.CompleteTransition();
                lively.Drop(lively.ActiveColor, true); lively.CompleteTransition();
            }
            Assert.That(sawMovement, Is.True);
        }

        [Test]
        public void FloatingNeverMovesActivePuckAndMotionStaysWithinBoundedOffsets()
        {
            for (int ms = 0; ms < 10000; ms += 137)
                for (int color = 0; color < 4; color++)
                {
                    var active = DailyRules.PuckHome(0, color, color, 100, uint.MaxValue,
                        FlowMode.Floating, BoardStyle.Lively, ms);
                    Assert.That(active, Is.EqualTo(new BoardPoint(-47000, 53000)));
                    var ring = DailyRules.RingCenter(0, color, 100, uint.MaxValue,
                        FlowMode.Drifting, BoardStyle.Lively, ms);
                    Assert.That(Math.Abs(ring.X + 103000), Is.LessThanOrEqualTo(8000));
                    Assert.That(Math.Abs(ring.Y - 152000), Is.LessThanOrEqualTo(5200));
                }
        }

        [Test]
        public void InvalidCoordinatesCannotOverflowIntoAValidDrop()
        {
            Assert.That(DailyRules.Within(new BoardPoint(int.MaxValue, int.MinValue), new BoardPoint(), DailyRules.RingRadius), Is.False);
        }
    }
}

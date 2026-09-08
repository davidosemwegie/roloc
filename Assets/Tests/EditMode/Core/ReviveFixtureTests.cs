using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Roloc.Tests
{
    public sealed class ReviveFixtureTests
    {
        [Serializable] sealed class Fixture
        {
            public int rulesVersion, seed, elapsedMs, reviveTransitionMs;
            public string[] variants;
            public Scenario[] scenarios;
        }
        [Serializable] sealed class Scenario { public string name; public Step[] steps; }
        [Serializable] sealed class Step { public string action, state; public int score, bank; }

        [Test]
        public void SharedReviveActionsPreserveTheSeededSequenceAndExpectedBank()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(
                Path.Combine(Application.dataPath, "Tests/EditMode/Core/ReviveFixtures.json")));
            foreach (string variant in fixture.variants)
                foreach (var scenario in fixture.scenarios)
                {
                    var style = variant == "still" ? Core.BoardStyle.Still : Core.BoardStyle.Lively;
                    var game = Core.GameSession.CreateDaily((uint)fixture.seed, style, fixture.rulesVersion);
                    var uninterrupted = Core.GameSession.CreateDaily((uint)fixture.seed, style, fixture.rulesVersion);
                    game.StartGame(); uninterrupted.StartGame();
                    foreach (var step in scenario.steps)
                    {
                        string context = variant + " / " + scenario.name + " / " + step.action + " / " + step.score;
                        switch (step.action)
                        {
                            case "match":
                                while (game.Score < step.score)
                                {
                                    Match(game, fixture.elapsedMs);
                                    Match(uninterrupted, fixture.elapsedMs);
                                    AssertSameBoard(game, uninterrupted, context);
                                }
                                break;
                            case "miss":
                                game.TickMilliseconds(fixture.elapsedMs);
                                Assert.That(game.DropAt(game.ActiveColor, 0, 0), Is.EqualTo(Core.MatchResult.Failed), context);
                                break;
                            case "inactive":
                                game.TickMilliseconds(fixture.elapsedMs);
                                var target = game.GetRingCenter(game.ActiveColor);
                                Assert.That(game.DropAt((game.ActiveColor + 1) % 4, target.X, target.Y), Is.EqualTo(Core.MatchResult.Failed), context);
                                break;
                            case "timeout":
                                Assert.That(game.TickMilliseconds(game.DurationMilliseconds), Is.EqualTo(Core.MatchResult.Failed), context);
                                break;
                            case "revive":
                                Assert.That(game.ApplyRewardedRevive(), Is.True, context);
                                Assert.That(game.RequiredTransitionMilliseconds, Is.EqualTo(fixture.reviveTransitionMs), context);
                                Assert.That(game.RoundElapsedMilliseconds, Is.Zero, context);
                                Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds), context);
                                game.CompleteTransition();
                                break;
                            case "abandon": game.EndRun(); break;
                            default: Assert.Fail("Unknown fixture action: " + step.action); break;
                        }
                        Assert.That(game.Score, Is.EqualTo(step.score), context);
                        Assert.That(game.RevivesAvailable, Is.EqualTo(step.bank), context);
                        var expected = step.state == "playing" ? Core.RoundState.Playing
                            : step.state == "failed" ? Core.RoundState.AwaitingRevive : Core.RoundState.GameOver;
                        Assert.That(game.State, Is.EqualTo(expected), context);
                        AssertSameBoard(game, uninterrupted, context);
                    }
                }
        }

        static void Match(Core.GameSession game, int elapsedMs)
        {
            game.TickMilliseconds(elapsedMs);
            var target = game.GetRingCenter(game.ActiveColor);
            Assert.That(game.DropAt(game.ActiveColor, target.X, target.Y), Is.EqualTo(Core.MatchResult.Matched));
            game.CompleteTransition();
        }

        static void AssertSameBoard(Core.GameSession game, Core.GameSession uninterrupted, string context)
        {
            Assert.That(game.ActiveColor, Is.EqualTo(uninterrupted.ActiveColor), context);
            CollectionAssert.AreEqual(uninterrupted.RingOrder, game.RingOrder, context);
            CollectionAssert.AreEqual(uninterrupted.PuckOrder, game.PuckOrder, context);
            Assert.That(game.FlowMode, Is.EqualTo(uninterrupted.FlowMode), context);
            Assert.That(game.RhythmPhase, Is.EqualTo(uninterrupted.RhythmPhase), context);
            Assert.That(game.DurationMilliseconds, Is.EqualTo(uninterrupted.DurationMilliseconds), context);
            Assert.That(game.PerfectCount, Is.EqualTo(uninterrupted.PerfectCount), context);
            if (game.State != Core.RoundState.Playing) return;
            for (int color = 0; color < 4; color++)
            {
                Assert.That(game.GetRingCenter(color), Is.EqualTo(uninterrupted.GetRingCenter(color)), context);
                Assert.That(game.GetPuckHome(color), Is.EqualTo(uninterrupted.GetPuckHome(color)), context);
            }
        }
    }
}

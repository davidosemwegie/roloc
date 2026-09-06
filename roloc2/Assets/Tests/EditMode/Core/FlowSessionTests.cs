using System;
using NUnit.Framework;
using Roloc.Core;

namespace Roloc.Tests
{
    public sealed class FlowSessionTests
    {
        static GameSession NewSession()
        {
            var flow = new FlowDirector(new Random(42), new FlowSettings
            {
                StartScore = 0, DriftStartScore = 0, RotationStartScore = 0,
                MinMatches = 1, MaxMatches = 1, MinCalmMatches = 1, MaxCalmMatches = 1,
                BreatherCooldownMatches = 0
            });
            return new GameSession(new Random(12), _ => 2f, _ => true, _ => true, flow);
        }

        [Test]
        public void RotationReplacesScramblingAndUsesTheCorrectPerimeterOrder()
        {
            var game = NewSession(); game.StartGame();
            int rotations = 0;
            int[] cycle = { 0, 1, 3, 2 };
            for (int score = 1; score <= 100; score++)
            {
                var rings = game.RingOrder;
                var pucks = game.PuckOrder;
                game.Drop(game.ActiveColor, true);
                if (game.RotationSteps != 0)
                {
                    rotations++;
                    Assert.That(game.RingOrder, Is.SameAs(rings));
                    for (int i = 0; i < 4; i++)
                        Assert.That(game.PuckOrder[cycle[(i + game.RotationSteps + 4) % 4]], Is.EqualTo(pucks[cycle[i]]));
                }
                game.CompleteTransition();
            }
            Assert.That(rotations, Is.GreaterThan(0));
        }

        [Test]
        public void BreatherBonusAndFlowRemainStableThroughPauseAndTransition()
        {
            var game = NewSession(); game.StartGame();
            bool sawBreather = false;
            for (int score = 1; score <= 60; score++)
            {
                game.Drop(game.ActiveColor, true);
                FlowMode mode = game.FlowMode;
                float duration = mode == FlowMode.Breather ? 2.65f : 2f;
                sawBreather |= mode == FlowMode.Breather;
                Assert.That(game.DurationSeconds, Is.EqualTo(duration).Within(.001f));
                Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds));
                game.Pause(); game.Tick(100); game.Drop(game.ActiveColor, true); game.Resume();
                Assert.That(game.FlowMode, Is.EqualTo(mode));
                Assert.That(game.RemainingSeconds, Is.EqualTo(game.DurationSeconds));
                game.CompleteTransition(); game.Tick(.1f);
                Assert.That(game.FlowMode, Is.EqualTo(mode));
            }
            Assert.That(sawBreather, Is.True);
        }

        [Test]
        public void TutorialAndRestartAlwaysBeginCalm()
        {
            var game = NewSession(); game.StartTutorial();
            game.Tick(100); game.Drop(game.ActiveColor, true);
            Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Steady));
            game.StartGame();
            for (int i = 0; i < 10; i++) { game.Drop(game.ActiveColor, true); game.CompleteTransition(); }
            game.ReturnToMenu();
            Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Steady));
            game.StartGame();
            Assert.That(game.FlowMode, Is.EqualTo(FlowMode.Steady));
            Assert.That(game.DurationSeconds, Is.EqualTo(2));
            Assert.That(game.RotationSteps, Is.Zero);
        }
    }
}

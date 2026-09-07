using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roloc.Core;

namespace Roloc.Tests
{
    public sealed class VariationRulesTests
    {
        static GameSession Session(BoardStyle board = BoardStyle.Lively, VariationSettings settings = null, GameMode mode = GameMode.Flow)
        {
            var game = new GameSession(mode, board, new Random(283),
                shouldShuffle: _ => true, shouldShufflePucks: _ => true, variationSettings: settings);
            game.StartGame();
            return game;
        }

        static void Match(GameSession game)
        {
            Assert.That(game.Drop(game.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
            game.CompleteTransition();
        }

        static bool OwnsLayout(FlowMode mode) => mode == FlowMode.PuckOrbit || mode == FlowMode.RingOrbit
            || mode == FlowMode.DualOrbit;

        [TestCase(BoardStyle.Lively, GameMode.Flow)]
        [TestCase(BoardStyle.Still, GameMode.Flow)]
        [TestCase(BoardStyle.Lively, GameMode.Rush)]
        [TestCase(BoardStyle.Still, GameMode.Rush)]
        public void NewChallengesRespectThresholdsBoardsAndEpisodeLengths(BoardStyle board, GameMode mode)
        {
            var game = Session(board, mode: mode);
            var seen = new HashSet<FlowMode>();
            var previousChallenge = FlowMode.Steady;
            int challengeStart = 0;
            for (int i = 0; i < 3000; i++)
            {
                var previousMode = game.FlowMode;
                var previousPhase = game.RhythmPhase;
                Match(game);
                Assert.That(game.ActiveColor, Is.InRange(0, 3));
                if (game.FlowMode == FlowMode.ColorShift) Assert.That(game.Score, Is.GreaterThanOrEqualTo(20));
                if (game.FlowMode == FlowMode.PuckOrbit || game.FlowMode == FlowMode.RingOrbit)
                    Assert.That(game.Score, Is.GreaterThanOrEqualTo(40));
                if (game.FlowMode == FlowMode.DualOrbit) Assert.That(game.Score, Is.GreaterThanOrEqualTo(60));
                if (board == BoardStyle.Still)
                    CollectionAssert.DoesNotContain(new[] { FlowMode.Floating, FlowMode.Drifting,
                        FlowMode.PuckOrbit, FlowMode.RingOrbit, FlowMode.DualOrbit }, game.FlowMode);
                if (previousPhase == RhythmPhase.Challenge && game.RhythmPhase != previousPhase)
                    Assert.That(game.Score - challengeStart, Is.InRange(3, 5));
                if (game.RhythmPhase == RhythmPhase.Challenge && previousPhase != RhythmPhase.Challenge)
                {
                    Assert.That(game.FlowMode, Is.Not.EqualTo(previousChallenge));
                    previousChallenge = game.FlowMode;
                    challengeStart = game.Score;
                    seen.Add(game.FlowMode);
                }
                if (previousMode != game.FlowMode && (OwnsLayout(previousMode) || OwnsLayout(game.FlowMode)))
                    Assert.That(game.RequiredTransitionMilliseconds, Is.EqualTo(750));
            }
            Assert.That(seen, Does.Contain(FlowMode.ColorShift));
            if (board == BoardStyle.Lively)
            {
                Assert.That(seen, Does.Contain(FlowMode.PuckOrbit));
                Assert.That(seen, Does.Contain(FlowMode.RingOrbit));
                Assert.That(seen, Does.Contain(FlowMode.DualOrbit));
            }
        }

        [Test]
        public void OrbitsKeepDirectionsAndPermutationsAndExerciseBothDualCombinations()
        {
            var game = Session();
            var combinations = new HashSet<string>();
            for (int i = 0; i < 5000; i++)
            {
                var mode = game.FlowMode;
                var rings = game.RingOrder; var pucks = game.PuckOrder;
                int puckDirection = game.PuckOrbitDirection, ringDirection = game.RingOrbitDirection;
                if (mode == FlowMode.DualOrbit) combinations.Add(puckDirection + ":" + ringDirection);
                Match(game);
                if (OwnsLayout(mode))
                {
                    Assert.That(game.RingOrder, Is.SameAs(rings));
                    Assert.That(game.PuckOrder, Is.SameAs(pucks));
                    if (game.FlowMode == mode)
                    {
                        Assert.That(game.PuckOrbitDirection, Is.EqualTo(puckDirection));
                        Assert.That(game.RingOrbitDirection, Is.EqualTo(ringDirection));
                    }
                }
                Assert.That(game.PuckOrbitDirection == 0,
                    Is.EqualTo(game.FlowMode != FlowMode.PuckOrbit && game.FlowMode != FlowMode.DualOrbit));
                Assert.That(game.RingOrbitDirection == 0,
                    Is.EqualTo(game.FlowMode != FlowMode.RingOrbit && game.FlowMode != FlowMode.DualOrbit));
            }
            Assert.That(combinations.Count, Is.EqualTo(4));
        }

        [Test]
        public void PaletteCategoriesKeepWeightsNoRepeatsAndRemainFixedThroughMistakes()
        {
            var game = Session();
            int previous = -1, episodes = 0;
            var categories = new int[3];
            var seen = new HashSet<int>();
            for (int i = 0; i < 30000; i++)
            {
                var previousMode = game.FlowMode;
                int palette = game.PaletteIndex;
                Match(game);
                if (game.FlowMode != FlowMode.ColorShift)
                {
                    Assert.That(game.PaletteIndex, Is.EqualTo(-1));
                    continue;
                }
                Assert.That(game.PaletteIndex, Is.InRange(0, 5));
                if (previousMode == FlowMode.ColorShift)
                {
                    Assert.That(game.PaletteIndex, Is.EqualTo(palette));
                    continue;
                }
                Assert.That(game.PaletteIndex, Is.Not.EqualTo(previous));
                previous = game.PaletteIndex; episodes++;
                seen.Add(previous); categories[previous / 2]++;
                if (episodes == 1)
                {
                    int active = game.ActiveColor;
                    game.Drop(active, false); game.CompleteTransition();
                    Assert.That(game.ActiveColor, Is.EqualTo(active));
                    Assert.That(game.PaletteIndex, Is.EqualTo(previous));
                }
            }
            Assert.That(seen.Count, Is.EqualTo(6));
            Assert.That((float)categories[0] / episodes, Is.InRange(.40f, .60f));
            Assert.That((float)categories[1] / episodes, Is.InRange(.17f, .33f));
            Assert.That((float)categories[2] / episodes, Is.InRange(.17f, .33f));
            game.StartTutorial();
            Assert.That(game.PaletteIndex, Is.EqualTo(-1));
            game.StartGame();
            Assert.That(game.PaletteIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ConfiguredThresholdsCanDeferAllNewVariations()
        {
            var game = Session(BoardStyle.Lively, new VariationSettings
            {
                ColorShiftStartScore = 10000, OrbitStartScore = 10000,
                DualOrbitStartScore = 10000
            });
            for (int i = 0; i < 1000; i++)
            {
                Match(game);
                Assert.That((int)game.FlowMode, Is.LessThan((int)FlowMode.ColorShift));
            }
        }
    }
}

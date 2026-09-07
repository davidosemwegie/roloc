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

        static bool OwnsLayout(FlowMode mode) => VariationMotion.HasOrbit(mode);

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
                if (game.FlowMode == FlowMode.DualOrbit || game.FlowMode == FlowMode.PuckOrbitDrifting
                    || game.FlowMode == FlowMode.RingOrbitFloating) Assert.That(game.Score, Is.GreaterThanOrEqualTo(60));
                if (game.FlowMode == FlowMode.FloatingDrifting) Assert.That(game.Score, Is.GreaterThanOrEqualTo(20));
                if (board == BoardStyle.Still)
                    CollectionAssert.DoesNotContain(new[] { FlowMode.Floating, FlowMode.Drifting,
                        FlowMode.PuckOrbit, FlowMode.RingOrbit, FlowMode.DualOrbit, FlowMode.FloatingDrifting,
                        FlowMode.PuckOrbitDrifting, FlowMode.RingOrbitFloating }, game.FlowMode);
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
                Assert.That(seen, Does.Contain(FlowMode.FloatingDrifting));
                Assert.That(seen, Does.Contain(FlowMode.PuckOrbitDrifting));
                Assert.That(seen, Does.Contain(FlowMode.RingOrbitFloating));
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
                    Is.EqualTo(!VariationMotion.OrbitsPucks(game.FlowMode)));
                Assert.That(game.RingOrbitDirection == 0,
                    Is.EqualTo(!VariationMotion.OrbitsRings(game.FlowMode)));
            }
            Assert.That(combinations.Count, Is.EqualTo(4));
        }

        [Test]
        public void PaletteCategoriesKeepWeightsAndPersistAcrossEveryModeAndMistake()
        {
            var game = Session();
            int previous = -1, episodes = 0;
            var categories = new int[3];
            var seen = new HashSet<int>();
            var modesWithPalette = new HashSet<FlowMode>();
            for (int i = 0; i < 30000; i++)
            {
                var previousMode = game.FlowMode;
                int palette = game.PaletteIndex;
                Match(game);
                if (game.PaletteIndex >= 0) modesWithPalette.Add(game.FlowMode);
                if (game.FlowMode != FlowMode.ColorShift)
                {
                    Assert.That(game.PaletteIndex, Is.EqualTo(palette));
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
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(FlowMode)), modesWithPalette);
            Assert.That((float)categories[0] / episodes, Is.InRange(.40f, .60f));
            Assert.That((float)categories[1] / episodes, Is.InRange(.17f, .33f));
            Assert.That((float)categories[2] / episodes, Is.InRange(.17f, .33f));
            game.ReturnToMenu();
            Assert.That(game.PaletteIndex, Is.EqualTo(-1));
            game.StartGame();
            for (int i = 0; i < 3000 && game.PaletteIndex < 0; i++) Match(game);
            Assert.That(game.PaletteIndex, Is.GreaterThanOrEqualTo(0));
            game.StartTutorial();
            Assert.That(game.PaletteIndex, Is.EqualTo(-1));
            game.StartGame();
            Assert.That(game.PaletteIndex, Is.EqualTo(-1));
        }

        [TestCase(FlowMode.Floating, true, false, false, false)]
        [TestCase(FlowMode.Drifting, false, true, false, false)]
        [TestCase(FlowMode.PuckOrbit, false, false, true, false)]
        [TestCase(FlowMode.RingOrbit, false, false, false, true)]
        [TestCase(FlowMode.DualOrbit, false, false, true, true)]
        [TestCase(FlowMode.FloatingDrifting, true, true, false, false)]
        [TestCase(FlowMode.PuckOrbitDrifting, false, true, true, false)]
        [TestCase(FlowMode.RingOrbitFloating, true, false, false, true)]
        [TestCase(FlowMode.Steady, false, false, false, false)]
        [TestCase(FlowMode.Breather, false, false, false, false)]
        [TestCase(FlowMode.Rotation, false, false, false, false)]
        [TestCase(FlowMode.ColorShift, false, false, false, false)]
        public void MotionCompositionUsesOneEffectPerGroup(FlowMode mode, bool floating, bool drifting, bool puckOrbit, bool ringOrbit)
        {
            Assert.That(VariationMotion.FloatsPucks(mode), Is.EqualTo(floating));
            Assert.That(VariationMotion.DriftsRings(mode), Is.EqualTo(drifting));
            Assert.That(VariationMotion.OrbitsPucks(mode), Is.EqualTo(puckOrbit));
            Assert.That(VariationMotion.OrbitsRings(mode), Is.EqualTo(ringOrbit));
            Assert.That(VariationMotion.HasOrbit(mode), Is.EqualTo(puckOrbit || ringOrbit));
            Assert.That(floating && puckOrbit, Is.False);
            Assert.That(drifting && ringOrbit, Is.False);
        }

        [TestCase(FlowMode.FloatingDrifting)]
        [TestCase(FlowMode.PuckOrbitDrifting)]
        [TestCase(FlowMode.RingOrbitFloating)]
        public void CompositeMistakesAndPausePreserveTheSameEpisodeTargetPaletteAndDirections(FlowMode mode)
        {
            var game = Session();
            var control = Session();
            for (int i = 0; i < 3000 && game.FlowMode != mode; i++) { Match(game); Match(control); }
            Assert.That(game.FlowMode, Is.EqualTo(mode));
            int target = game.ActiveColor, palette = game.PaletteIndex;
            int puckDirection = game.PuckOrbitDirection, ringDirection = game.RingOrbitDirection;
            var rings = game.RingOrder; var pucks = game.PuckOrder;
            Assert.That(game.Drop(target, false), Is.EqualTo(MatchResult.ChanceLost));
            Assert.That(game.FlowMode, Is.EqualTo(mode));
            Assert.That(game.RingOrder, Is.SameAs(rings)); Assert.That(game.PuckOrder, Is.SameAs(pucks));
            Assert.That(game.ActiveColor, Is.EqualTo(target)); Assert.That(game.PaletteIndex, Is.EqualTo(palette));
            Assert.That(game.PuckOrbitDirection, Is.EqualTo(puckDirection));
            Assert.That(game.RingOrbitDirection, Is.EqualTo(ringDirection));
            game.Pause(); game.Tick(100); game.Resume(); game.CompleteTransition();
            Match(game); Match(control);
            Assert.That(game.FlowMode, Is.EqualTo(control.FlowMode));
            Assert.That(game.ActiveColor, Is.EqualTo(control.ActiveColor));
            Assert.That(game.PaletteIndex, Is.EqualTo(control.PaletteIndex));
            Assert.That(game.PuckOrbitDirection, Is.EqualTo(control.PuckOrbitDirection));
            Assert.That(game.RingOrbitDirection, Is.EqualTo(control.RingOrbitDirection));
            CollectionAssert.AreEqual(control.RingOrder, game.RingOrder);
            CollectionAssert.AreEqual(control.PuckOrder, game.PuckOrder);
        }

        [Test]
        public void ConfiguredThresholdsCanDeferAllNewVariations()
        {
            var game = Session(BoardStyle.Lively, new VariationSettings
            {
                ColorShiftStartScore = 10000, CombinedMotionStartScore = 10000, OrbitStartScore = 10000,
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

using System;

namespace Roloc.Core
{
    public enum FlowMode { Steady, Floating, Drifting, Breather, Rotation, ColorShift, PuckOrbit, RingOrbit, DualOrbit, FloatingDrifting, PuckOrbitDrifting, RingOrbitFloating }

    /// <summary>Schedules the next round's variation after each awarded match.</summary>
    public sealed class FlowDirector
    {
        private readonly Random random;
        private readonly int startScore;
        private readonly int driftStartScore;
        private readonly int rotationStartScore;
        private readonly int minMatches;
        private readonly int maxMatches;
        private readonly int minCalmMatches;
        private readonly int maxCalmMatches;
        private readonly int breatherCooldownMatches;
        private readonly float breatherExtraSeconds;
        private readonly FlowMode[] candidates = new FlowMode[4];

        private FlowMode previousSpecial;
        private int remainingMatches;
        private long nextEpisodeScore;
        private int lastAwardedScore;
        private int lastBreatherScore;
        private bool hasBreatherHistory;

        public FlowMode Mode { get; private set; }
        public int RotationSteps { get; private set; }
        public float ExtraSeconds => Mode == FlowMode.Breather ? breatherExtraSeconds : 0f;

        public FlowDirector(Random random, FlowSettings settings = null)
        {
            this.random = random ?? new Random();
            settings = settings ?? new FlowSettings();
            startScore = Math.Max(0, settings.StartScore);
            driftStartScore = Math.Max(startScore, settings.DriftStartScore);
            rotationStartScore = Math.Max(driftStartScore, settings.RotationStartScore);
            minMatches = Clamp(settings.MinMatches, 1, 8);
            maxMatches = Clamp(settings.MaxMatches, minMatches, 8);
            minCalmMatches = Clamp(settings.MinCalmMatches, 1, 8);
            maxCalmMatches = Clamp(settings.MaxCalmMatches, minCalmMatches, 8);
            breatherCooldownMatches = Math.Max(0, settings.BreatherCooldownMatches);
            breatherExtraSeconds = float.IsNaN(settings.BreatherExtraSeconds)
                || float.IsInfinity(settings.BreatherExtraSeconds)
                ? 0f : Math.Max(0f, Math.Min(1f, settings.BreatherExtraSeconds));
            Reset();
        }

        public void Reset()
        {
            Mode = FlowMode.Steady;
            RotationSteps = 0;
            previousSpecial = FlowMode.Steady;
            remainingMatches = 0;
            nextEpisodeScore = (long)startScore + random.Next(3);
            lastAwardedScore = 0;
            lastBreatherScore = 0;
            hasBreatherHistory = false;
        }

        public void Advance(int awardedScore)
        {
            // A repeated notification must not shorten an episode or rotate twice.
            if (awardedScore <= lastAwardedScore) return;
            lastAwardedScore = awardedScore;
            RotationSteps = 0;

            if (Mode != FlowMode.Steady)
            {
                remainingMatches--;
                if (remainingMatches > 0) return;
                Mode = FlowMode.Steady;
                nextEpisodeScore = (long)awardedScore + random.Next(minCalmMatches, maxCalmMatches + 1);
                return;
            }

            if (awardedScore < nextEpisodeScore) return;

            int count = 0;
            AddCandidate(FlowMode.Floating, awardedScore >= startScore, ref count);
            AddCandidate(FlowMode.Drifting, awardedScore >= driftStartScore, ref count);
            AddCandidate(FlowMode.Breather, awardedScore >= startScore && (!hasBreatherHistory
                || (long)awardedScore - lastBreatherScore >= breatherCooldownMatches), ref count);
            AddCandidate(FlowMode.Rotation, awardedScore >= rotationStartScore, ref count);

            // Early in a run, cooldown plus no-repeat may exhaust eligible modes.
            // Keep a calm round and retry after the next success.
            if (count == 0) return;
            Mode = candidates[random.Next(count)];
            previousSpecial = Mode;
            remainingMatches = Mode == FlowMode.Rotation ? 1 : random.Next(minMatches, maxMatches + 1);
            if (Mode == FlowMode.Rotation) RotationSteps = random.Next(2) == 0 ? -1 : 1;
            if (Mode == FlowMode.Breather)
            {
                hasBreatherHistory = true;
                lastBreatherScore = awardedScore;
            }
        }

        private void AddCandidate(FlowMode mode, bool eligible, ref int count)
        {
            if (eligible && mode != previousSpecial) candidates[count++] = mode;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}

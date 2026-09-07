using System;

namespace Roloc.Core
{
    public enum GameMode { Flow, Rush, Daily }
    public enum BoardStyle { Lively, Still }
    public enum DropFailure { None, MissedRing, WrongRing, TimeExpired, InactivePuck }
    public enum RhythmPhase { Calm, Challenge, Recovery }

    /// <summary>Run pacing shared by ordinary modes and the versioned Daily rules.</summary>
    public sealed class RhythmDirector
    {
        readonly Random random;
        readonly GameMode mode;
        readonly FlowMode[] candidates = new FlowMode[3];
        static readonly FlowMode[] Eligible = { FlowMode.Floating, FlowMode.Drifting, FlowMode.Rotation };
        int remaining, lastAwardedScore;
        FlowMode previousChallenge;

        public RhythmPhase Phase { get; private set; }
        public FlowMode Mode { get; private set; }
        public int RotationSteps { get; private set; }
        public float ExtraSeconds => Phase == RhythmPhase.Recovery ? .65f : 0f;

        public RhythmDirector(Random random, GameMode mode)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.mode = mode;
        }

        public void Reset()
        {
            Phase = RhythmPhase.Calm;
            Mode = previousChallenge = FlowMode.Steady;
            RotationSteps = lastAwardedScore = 0;
            remaining = mode == GameMode.Flow ? 10 : random.Next(3, 6);
        }

        public void Advance(int awardedScore)
        {
            if (awardedScore <= lastAwardedScore) return;
            lastAwardedScore = awardedScore;
            RotationSteps = 0;
            if (--remaining > 0) return;
            if (Phase == RhythmPhase.Calm)
            {
                Phase = RhythmPhase.Challenge;
                // Keep early challenges gentle; rotation unlocks at forty matches.
                var count = awardedScore >= 40 ? 3 : 2;
                int available = 0;
                for (int i = 0; i < count; i++)
                    if (Eligible[i] != previousChallenge) candidates[available++] = Eligible[i];
                Mode = previousChallenge = candidates[random.Next(available)];
                remaining = random.Next(3, 6);
                if (Mode == FlowMode.Rotation) RotationSteps = random.Next(2) == 0 ? -1 : 1;
            }
            else if (Phase == RhythmPhase.Challenge)
            {
                Phase = RhythmPhase.Recovery;
                Mode = FlowMode.Breather;
                remaining = 2;
            }
            else
            {
                Phase = RhythmPhase.Calm;
                Mode = FlowMode.Steady;
                remaining = random.Next(3, 6);
            }
        }
    }
}

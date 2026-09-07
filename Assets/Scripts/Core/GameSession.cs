using System;

namespace Roloc.Core
{
    public enum RoundState { Menu, Tutorial, Playing, Transition, Paused, GameOver }
    public enum MatchResult { Ignored, Matched, Failed, TutorialCompleted, ChanceLost }

    /// <summary>Frame-independent rules. Rendering and input consume run events and canonical geometry.</summary>
    public sealed class GameSession
    {
        readonly Random random;
        readonly Func<int, float> secondsForScore;
        readonly Func<int, bool> shouldShuffle;
        readonly Func<int, bool> shouldShufflePucks;
        readonly FlowDirector flowDirector;
        readonly RhythmDirector rhythm;
        readonly uint dailySeed;
        RoundState pausedState;
        double elapsedMilliseconds;
        int cleanMatchesSinceRecovery;
        int previousPalette = -1;

        public RoundState State { get; private set; } = RoundState.Menu;
        public GameMode Mode { get; private set; } = GameMode.Rush;
        public BoardStyle BoardStyle { get; private set; } = BoardStyle.Lively;
        public bool IsDaily => Mode == GameMode.Daily;
        public uint DailySeed => dailySeed;
        public int Score { get; private set; }
        public int ActiveColor { get; private set; }
        public int PaletteIndex { get; private set; } = -1;
        public int PuckOrbitDirection { get; private set; }
        public int RingOrbitDirection { get; private set; }
        // Color at each board position. Consumers must not modify these arrays.
        public int[] RingOrder { get; private set; } = new[] { 0, 1, 2, 3 };
        public int[] PuckOrder { get; private set; } = new[] { 0, 1, 2, 3 };
        public float RemainingSeconds { get; private set; }
        public float DurationSeconds { get; private set; }
        public int DurationMilliseconds => (int)Math.Round(DurationSeconds * 1000.0);
        public int RoundElapsedMilliseconds => Math.Min(DurationMilliseconds, (int)elapsedMilliseconds);
        public int RequiredTransitionMilliseconds { get; private set; }
        public bool WasTutorial { get; private set; }
        public int Chances { get; private set; }
        public int RecoveriesUsed { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int PerfectStreak { get; private set; }
        public int BestPerfectStreak { get; private set; }
        public bool LastDropPerfect { get; private set; }
        public int LastProgressEarned { get; private set; }
        public bool LastChanceRestored { get; private set; }
        public DropFailure LastFailure { get; private set; }
        public MatchResult LastResult { get; private set; }
        public FlowMode FlowMode => WasTutorial ? Roloc.Core.FlowMode.Steady
            : rhythm?.Mode ?? flowDirector?.Mode ?? Roloc.Core.FlowMode.Steady;
        public RhythmPhase RhythmPhase => rhythm?.Phase ?? Roloc.Core.RhythmPhase.Calm;
        public int RotationSteps { get; private set; }

        // Legacy constructor remains strict Rush and retains its injected variation schedule.
        public GameSession(Random random = null, Func<int, float> secondsForScore = null,
            Func<int, bool> shouldShuffle = null, Func<int, bool> shouldShufflePucks = null,
            FlowDirector flowDirector = null)
        {
            this.random = random ?? new Random();
            this.secondsForScore = secondsForScore ?? DefaultRules.SecondsForScore;
            this.shouldShuffle = shouldShuffle ?? DefaultRules.ShouldShuffle;
            this.shouldShufflePucks = shouldShufflePucks ?? DefaultRules.ShouldShufflePucks;
            this.flowDirector = flowDirector;
        }

        public GameSession(GameMode mode, BoardStyle boardStyle, Random random = null,
            Func<int, float> secondsForScore = null, Func<int, bool> shouldShuffle = null,
            Func<int, bool> shouldShufflePucks = null, bool variationsEnabled = true,
            VariationSettings variationSettings = null)
            : this(random, secondsForScore ?? (mode == GameMode.Flow
                ? (Func<int, float>)DefaultRules.FlowSecondsForScore : DefaultRules.SecondsForScore),
                shouldShuffle, shouldShufflePucks)
        {
            if (mode == GameMode.Daily)
                throw new ArgumentException("Use CreateDaily to supply its published seed.", nameof(mode));
            Mode = mode;
            BoardStyle = boardStyle;
            if (variationsEnabled) rhythm = new RhythmDirector(this.random, mode, boardStyle, variationSettings);
        }

        GameSession(uint seed, BoardStyle boardStyle) : this(new DailyRandom(seed))
        {
            Mode = GameMode.Daily;
            BoardStyle = boardStyle;
            dailySeed = seed;
            rhythm = new RhythmDirector(random, Mode);
        }

        public static GameSession CreateDaily(uint seed, BoardStyle boardStyle) => new GameSession(seed, boardStyle);
        public void StartGame() => Start(false);
        public void StartTutorial() => Start(true);

        void Start(bool tutorial)
        {
            if (IsDaily && random is DailyRandom seeded) seeded.Reset(dailySeed);
            Score = Combo = BestCombo = PerfectCount = PerfectStreak = BestPerfectStreak = 0;
            RecoveriesUsed = cleanMatchesSinceRecovery = RotationSteps = RequiredTransitionMilliseconds = 0;
            Chances = Mode == GameMode.Flow ? 3 : 1;
            ClearEvent();
            flowDirector?.Reset();
            WasTutorial = tutorial;
            PaletteIndex = previousPalette = -1;
            PuckOrbitDirection = RingOrbitDirection = 0;
            RingOrder = Permutation();
            PuckOrder = Permutation();
            ActiveColor = random.Next(4);
            // V1 sequence: ring shuffle, puck shuffle, active color, initial calm length.
            rhythm?.Reset();
            State = tutorial ? RoundState.Tutorial : RoundState.Playing;
            DurationSeconds = tutorial ? 0f : secondsForScore(0);
            ResetTimer();
        }

        public MatchResult Drop(int color, bool insideMatchingRing, bool perfect = false,
            DropFailure failure = DropFailure.MissedRing)
            => DropClassified(color, insideMatchingRing, perfect, failure);

        /// <summary>Classifies a released puck center with fixed geometry. Required for Daily.</summary>
        public MatchResult DropAt(int color, int xQ, int yQ)
        {
            if (!CanDrop(color)) return MatchResult.Ignored;
            if (color != ActiveColor) return DropClassified(color, false, false, DropFailure.InactivePuck);
            var point = new BoardPoint(xQ, yQ);
            var center = GetRingCenter(color);
            bool inside = DailyRules.Within(point, center, DailyRules.RingRadius);
            var failure = DropFailure.MissedRing;
            if (!inside)
                for (int other = 0; other < 4; other++)
                    if (other != color && DailyRules.Within(point, GetRingCenter(other), DailyRules.RingRadius))
                        failure = DropFailure.WrongRing;
            return DropClassified(color, inside, inside && DailyRules.Within(point, center, DailyRules.PerfectRadius), failure);
        }

        public BoardPoint GetRingCenter(int color)
        {
            int slot = Array.IndexOf(RingOrder, color);
            return DailyRules.RingCenter(slot, color, Score, dailySeed, FlowMode, BoardStyle, RoundElapsedMilliseconds);
        }

        public BoardPoint GetPuckHome(int color)
        {
            int slot = Array.IndexOf(PuckOrder, color);
            return DailyRules.PuckHome(slot, color, ActiveColor, Score, dailySeed, FlowMode, BoardStyle, RoundElapsedMilliseconds);
        }

        bool CanDrop(int color) => (State == RoundState.Playing || State == RoundState.Tutorial)
            && color >= 0 && color < 4;

        MatchResult DropClassified(int color, bool inside, bool perfect, DropFailure failure)
        {
            if (!CanDrop(color)) return MatchResult.Ignored;
            if (State == RoundState.Tutorial)
            {
                if (color != ActiveColor || !inside) return MatchResult.Ignored;
                State = RoundState.Menu;
                return LastResult = MatchResult.TutorialCompleted;
            }
            ClearEvent();
            if (color != ActiveColor) return LoseChance(DropFailure.InactivePuck);
            if (!inside) return LoseChance(failure);

            Score++;
            Combo++;
            BestCombo = Math.Max(Combo, BestCombo);
            LastDropPerfect = perfect;
            LastProgressEarned = perfect ? 2 : 1;
            if (perfect)
            {
                PerfectCount++;
                PerfectStreak++;
                BestPerfectStreak = Math.Max(PerfectStreak, BestPerfectStreak);
            }
            else PerfectStreak = 0;
            if (Mode == GameMode.Flow && ++cleanMatchesSinceRecovery >= 15)
            {
                cleanMatchesSinceRecovery = 0;
                if (Chances < 3 && RecoveriesUsed < 2)
                {
                    Chances++;
                    RecoveriesUsed++;
                    LastChanceRestored = true;
                }
            }

            var previousMode = FlowMode;
            var previousRings = RingOrder;
            var previousPucks = PuckOrder;
            // Daily v1 and legacy injected schedules select the target first.
            bool legacyTargetOrder = IsDaily || rhythm == null;
            if (legacyTargetOrder) ActiveColor = random.Next(4);
            rhythm?.Advance(Score);
            flowDirector?.Advance(Score);
            RotationSteps = rhythm?.RotationSteps ?? flowDirector?.RotationSteps ?? 0;
            if (!legacyTargetOrder) ActiveColor = random.Next(4);
            if (!IsDaily && previousMode != FlowMode)
            {
                // A new palette becomes the run palette until another Color Shift or a reset.
                if (FlowMode == FlowMode.ColorShift) PaletteIndex = NextPalette();
                PuckOrbitDirection = VariationMotion.OrbitsPucks(FlowMode)
                    ? (random.Next(2) == 0 ? -1 : 1) : 0;
                RingOrbitDirection = VariationMotion.OrbitsRings(FlowMode)
                    ? (random.Next(2) == 0 ? -1 : 1) : 0;
            }
            bool layoutOwned = OwnsLayout(previousMode) || OwnsLayout(FlowMode);
            // Continuous layouts own their entry, exit and in-episode transitions. No deferred shuffle.
            if (!layoutOwned)
            {
                if (RotationSteps != 0) PuckOrder = RotatedPucks(RotationSteps);
                else
                {
                    if (shouldShuffle(Score)) RingOrder = Permutation();
                    if (shouldShufflePucks(Score)) PuckOrder = DifferentPermutation(PuckOrder);
                }
            }
            else RotationSteps = 0;
            RequiredTransitionMilliseconds = previousMode != FlowMode && layoutOwned ? 750
                : RotationSteps != 0 ? 750
                : previousRings != RingOrder && previousPucks != PuckOrder ? 675
                : previousMode != FlowMode || previousRings != RingOrder || previousPucks != PuckOrder ? 450 : 240;
            DurationSeconds = secondsForScore(Score) + (rhythm?.ExtraSeconds ?? flowDirector?.ExtraSeconds ?? 0);
            ResetTimer();
            State = RoundState.Transition;
            return LastResult = MatchResult.Matched;
        }

        /// <returns>True only when a timeout ends the run. New callers use TickResult for recoverable timeouts.</returns>
        public bool Tick(float deltaSeconds) => TickResult(deltaSeconds) == MatchResult.Failed;

        public MatchResult TickResult(float deltaSeconds)
        {
            if (State != RoundState.Playing || deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
                return MatchResult.Ignored;
            return AdvanceMilliseconds(deltaSeconds * 1000.0);
        }

        public MatchResult TickMilliseconds(int milliseconds)
        {
            if (State != RoundState.Playing || milliseconds <= 0) return MatchResult.Ignored;
            return AdvanceMilliseconds(milliseconds);
        }

        MatchResult AdvanceMilliseconds(double milliseconds)
        {
            elapsedMilliseconds = Math.Min(DurationMilliseconds, elapsedMilliseconds + milliseconds);
            RemainingSeconds = (float)Math.Max(0, (DurationMilliseconds - elapsedMilliseconds) / 1000.0);
            if (elapsedMilliseconds < DurationMilliseconds) return MatchResult.Ignored;
            ClearEvent();
            return LoseChance(DropFailure.TimeExpired);
        }

        MatchResult LoseChance(DropFailure failure)
        {
            LastFailure = failure;
            Chances = Math.Max(0, Chances - 1);
            Combo = PerfectStreak = cleanMatchesSinceRecovery = RotationSteps = 0;
            if (Chances == 0)
            {
                State = RoundState.GameOver;
                return LastResult = MatchResult.Failed;
            }
            // A retry neither consumes randomness nor changes its target, layout or variation.
            ResetTimer();
            RequiredTransitionMilliseconds = 450;
            State = RoundState.Transition;
            return LastResult = MatchResult.ChanceLost;
        }

        void ClearEvent()
        {
            LastDropPerfect = LastChanceRestored = false;
            LastProgressEarned = 0;
            LastFailure = DropFailure.None;
            LastResult = MatchResult.Ignored;
        }

        void ResetTimer()
        {
            elapsedMilliseconds = 0;
            RemainingSeconds = DurationSeconds;
        }

        public void CompleteTransition()
        {
            if (State == RoundState.Transition) State = RoundState.Playing;
        }

        public void Pause()
        {
            if (State != RoundState.Playing && State != RoundState.Tutorial && State != RoundState.Transition) return;
            pausedState = State;
            State = RoundState.Paused;
        }

        public void Resume()
        {
            if (State == RoundState.Paused) State = pausedState;
        }

        public void ReturnToMenu()
        {
            flowDirector?.Reset();
            rhythm?.Reset();
            State = RoundState.Menu;
            Score = RotationSteps = 0;
            PaletteIndex = previousPalette = -1;
            PuckOrbitDirection = RingOrbitDirection = 0;
            RemainingSeconds = DurationSeconds = 0f;
            elapsedMilliseconds = 0;
        }

        static bool OwnsLayout(FlowMode mode) => VariationMotion.HasOrbit(mode);

        int NextPalette()
        {
            // Select the category first, retaining 50/25/25 weights even when avoiding repeats.
            int category = random.Next(4);
            int first = category < 2 ? 0 : category == 2 ? 2 : 4;
            int palette = first + random.Next(2);
            if (palette == previousPalette) palette = first + (1 - (palette - first));
            return previousPalette = palette;
        }

        int[] DifferentPermutation(int[] previous)
        {
            var order = Permutation();
            for (int i = 0; i < order.Length; i++) if (order[i] != previous[i]) return order;
            int swap = order[0]; order[0] = order[1]; order[1] = swap;
            return order;
        }

        int[] RotatedPucks(int steps)
        {
            int[] cycle = { 0, 1, 3, 2 };
            var order = new int[4];
            for (int i = 0; i < 4; i++) order[cycle[(i + steps + 4) % 4]] = PuckOrder[cycle[i]];
            return order;
        }

        int[] Permutation()
        {
            var order = new int[4];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i]; order[i] = order[j]; order[j] = swap;
            }
            return order;
        }
    }
}

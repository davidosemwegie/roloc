using System;

namespace Roloc.Core
{
    public enum RoundState { Menu, Tutorial, Playing, Transition, Paused, GameOver }
    public enum MatchResult { Ignored, Matched, Failed, TutorialCompleted }

    /// <summary>Frame-independent game rules. The presentation owns animation and hit testing.</summary>
    public sealed class GameSession
    {
        private readonly Random random;
        private readonly Func<int, float> secondsForScore;
        private readonly Func<int, bool> shouldShuffle;
        private readonly Func<int, bool> shouldShufflePucks;
        private readonly FlowDirector flowDirector;
        private RoundState pausedState;

        public RoundState State { get; private set; } = RoundState.Menu;
        public int Score { get; private set; }
        public int ActiveColor { get; private set; }
        // Color at each board position. Consumers must not modify these arrays.
        public int[] RingOrder { get; private set; } = new[] { 0, 1, 2, 3 };
        public int[] PuckOrder { get; private set; } = new[] { 0, 1, 2, 3 };
        public float RemainingSeconds { get; private set; }
        public float DurationSeconds { get; private set; }
        public bool WasTutorial { get; private set; }
        public FlowMode FlowMode => flowDirector?.Mode ?? Roloc.Core.FlowMode.Steady;
        public int RotationSteps => flowDirector?.RotationSteps ?? 0;

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

        public void StartGame() => Start(false);
        public void StartTutorial() => Start(true);

        private void Start(bool tutorial)
        {
            Score = 0;
            flowDirector?.Reset();
            WasTutorial = tutorial;
            RingOrder = Permutation();
            PuckOrder = Permutation();
            ActiveColor = random.Next(4);
            State = tutorial ? RoundState.Tutorial : RoundState.Playing;
            DurationSeconds = tutorial ? 0f : secondsForScore(0);
            RemainingSeconds = DurationSeconds;
        }

        public MatchResult Drop(int color, bool insideMatchingRing)
        {
            if ((State != RoundState.Playing && State != RoundState.Tutorial) || color != ActiveColor)
                return MatchResult.Ignored;

            if (State == RoundState.Tutorial)
            {
                if (!insideMatchingRing) return MatchResult.Ignored;
                State = RoundState.Menu;
                return MatchResult.TutorialCompleted;
            }

            if (!insideMatchingRing)
            {
                State = RoundState.GameOver;
                return MatchResult.Failed;
            }

            Score++;
            ActiveColor = random.Next(4);
            flowDirector?.Advance(Score);
            // A coordinated turn owns this transition; do not also scramble either board.
            if (RotationSteps != 0) PuckOrder = RotatedPucks(RotationSteps);
            else
            {
                if (shouldShuffle(Score)) RingOrder = Permutation();
                if (shouldShufflePucks(Score)) PuckOrder = DifferentPermutation(PuckOrder);
            }
            DurationSeconds = secondsForScore(Score) + (flowDirector?.ExtraSeconds ?? 0);
            RemainingSeconds = DurationSeconds;
            State = RoundState.Transition;
            return MatchResult.Matched;
        }

        /// <returns>True only on the frame that ends this run.</returns>
        public bool Tick(float deltaSeconds)
        {
            if (State != RoundState.Playing || deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
                return false;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0f) return false;
            State = RoundState.GameOver;
            return true;
        }

        public void CompleteTransition()
        {
            if (State == RoundState.Transition) State = RoundState.Playing;
        }

        public void Pause()
        {
            if (State != RoundState.Playing && State != RoundState.Tutorial && State != RoundState.Transition)
                return;
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
            State = RoundState.Menu;
            Score = 0;
            RemainingSeconds = 0f;
            DurationSeconds = 0f;
        }

        private int[] DifferentPermutation(int[] previous)
        {
            var order = Permutation();
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] != previous[i]) return order;
            }

            // A random shuffle may reproduce the same board. One swap guarantees
            // visible movement without retrying, even with a constant random source.
            int swap = order[0];
            order[0] = order[1];
            order[1] = swap;
            return order;
        }

        private int[] RotatedPucks(int steps)
        {
            // Grid order is TL, TR, BL, BR; this cycle follows the perimeter clockwise.
            int[] cycle = { 0, 1, 3, 2 };
            var order = new int[4];
            for (int i = 0; i < 4; i++)
                order[cycle[(i + steps + 4) % 4]] = PuckOrder[cycle[i]];
            return order;
        }

        private int[] Permutation()
        {
            var order = new[] { 0, 1, 2, 3 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }
            return order;
        }
    }
}

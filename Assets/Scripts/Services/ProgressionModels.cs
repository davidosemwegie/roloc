namespace Roloc.Services
{
    /// <summary>Finalization records completion; match credit is already banked while playing.</summary>
    public sealed class LocalRunSummary
    {
        public string RunId;
        public int Score;
        public int LongestCombo;
        public int LongestPerfectStreak;
        public int PerfectCount;
    }

    public readonly struct MatchCreditResult
    {
        public bool Accepted { get; }
        public int MatchPoints { get; }
        public int GoalPoints { get; }
        public int Earned => MatchPoints + GoalPoints;
        public MatchCreditResult(bool accepted, int matchPoints = 0, int goalPoints = 0)
        { Accepted = accepted; MatchPoints = matchPoints; GoalPoints = goalPoints; }
    }

    public sealed class DailyGoalProgress
    {
        public string Id { get; internal set; }
        public string Label { get; internal set; }
        public int Target { get; internal set; }
        public int Current { get; internal set; }
        public bool Awarded { get; internal set; }
        public bool Eligible { get; internal set; }
        public int Reward => 10;
    }
}

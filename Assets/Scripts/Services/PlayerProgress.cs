using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    [Serializable]
    public sealed class PlayerProgress
    {
        // Zero identifies saves written before progression was introduced.
        public int SchemaVersion;
        public int HighScore;
        public int GamesPlayed;
        public long TotalScore;
        public bool TutorialCompleted;
        public bool MusicEnabled = true;
        public bool MatchEnabled = true;
        public bool GameOverEnabled = true;
        public bool HapticsEnabled = true;
        public bool SymbolsEnabled;
        public bool ReduceEffects;
        public bool EffectsPreferenceInitialized;
        public string SelectedMode = "Flow";
        public string SelectedBoard = "Lively";
        public bool ChancesHintShown;
        public bool PerfectHintShown;
        public bool RecoveryHintShown;
        public bool RushSuggestionShown;
        public long ProgressPoints;
        public long TotalPerfects;
        public bool BadgesInitialized;
        public List<string> UnlockedBadges = new List<string>();
        public int LongestCombo;
        public int LongestPerfectStreak;
        public string EquippedPuck = "classic";
        public string EquippedTrail = "classic";
        public string EquippedRing = "classic";
        public string EquippedBackground = "classic";
        public List<ModeRecord> Records = new List<ModeRecord>();
        public string SaveId;
        public long RunSequence;
        public RunCreditLedger ActiveRun;
        public DailyGoalState DailyGoals;
    }

    [Serializable]
    public sealed class ModeRecord
    {
        public string Mode;
        public string Board;
        public int HighScore;
        public int GamesPlayed;
        public int LongestCombo;
        public int LongestPerfectStreak;
        public int MostPerfects;
    }

    [Serializable]
    public sealed class RunCreditLedger
    {
        public string RunId;
        public string Mode;
        public string Board;
        public int MatchIndex;
        public int PerfectCount;
        public int LongestCombo;
        public int LongestPerfectStreak;
        public long ProgressEarned;
        public long GoalProgressEarned;
        public bool Finalized;
        public List<string> NewBadges = new List<string>();
    }

    [Serializable]
    public sealed class DailyGoalState
    {
        // A high-water mark prevents revisiting an old device date to reclaim rewards.
        public string DayKey;
        public int Matches;
        public int Perfects;
        public int BestCombo;
        public bool FirstAwarded;
        public bool SecondAwarded;
    }
}

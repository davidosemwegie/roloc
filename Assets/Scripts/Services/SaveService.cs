using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>Small local-only save; no dependency on the previous game's storage.</summary>
    public sealed class SaveService
    {
        public const string FileName = "roloc2-progress.json";
        public const int CurrentSchemaVersion = 2;
        private readonly string directory;
        public PlayerProgress Data { get; private set; }

        public SaveService(string directory = null)
        {
            this.directory = directory ?? Application.persistentDataPath;
            Load();
        }

        public void Load()
        {
            Data = new PlayerProgress();
            Sanitize(Data);
            try
            {
                string path = Path.Combine(directory, FileName);
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path).Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                    throw new FormatException("Save must be a JSON object.");
                // Overwrite initialized defaults so newly added preferences remain enabled.
                var loaded = new PlayerProgress();
                JsonUtility.FromJsonOverwrite(json, loaded);
                Migrate(loaded);
                Sanitize(loaded);
                Data = loaded;
            }
            catch (Exception exception)
            {
                // Leave an unreadable save untouched until the next explicit save.
                Debug.LogWarning($"ROLOC could not load progress: {exception.Message}");
            }
        }

        public void Save()
        {
            string temporaryPath = null;
            try
            {
                Sanitize(Data);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, FileName);
                temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data));
                // Both files reside on the same filesystem; replace the completed write atomically.
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ROLOC could not save progress: {exception.Message}");
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                    catch (Exception) { /* Keep the game usable if storage is unavailable. */ }
                }
            }
        }

        // Writes are intentionally synchronous and ordered on Unity's main thread. Small saves are
        // completed before returning; JsonUtility is never called from a worker thread.
        public void Flush() => Save();

        public string StartRun(string mode, string board, string dayKey = null)
        {
            if (!IsMode(mode) || !IsBoard(board)) throw new ArgumentException("Unknown game mode or board style.");
            Sanitize(Data);
            if (Data.RunSequence == long.MaxValue)
            {
                Data.SaveId = Guid.NewGuid().ToString("N");
                Data.RunSequence = 0;
            }
            Data.RunSequence++;
            Data.ActiveRun = new RunCreditLedger
            {
                RunId = Data.SaveId + ":" + Data.RunSequence.ToString(CultureInfo.InvariantCulture),
                Mode = mode,
                Board = board
            };
            AdvanceGoalDay(dayKey);
            Save();
            return Data.ActiveRun.RunId;
        }

        public MatchCreditResult RecordMatch(string runId, int matchIndex, string mode, string board,
            bool perfect, int combo, int perfectStreak, string dayKey = null)
        {
            var run = Data.ActiveRun;
            if (run == null || run.Finalized || run.RunId != runId || run.Mode != mode || run.Board != board
                || run.MatchIndex == int.MaxValue || matchIndex != run.MatchIndex + 1)
                return new MatchCreditResult(false);

            run.MatchIndex = matchIndex;
            combo = Math.Max(0, Math.Min(matchIndex, combo));
            perfectStreak = perfect ? Math.Max(0, Math.Min(combo, perfectStreak)) : 0;
            if (perfect) run.PerfectCount++;
            run.LongestCombo = Math.Max(run.LongestCombo, combo);
            run.LongestPerfectStreak = Math.Max(run.LongestPerfectStreak, perfectStreak);
            int matchPoints = perfect ? 2 : 1;
            Data.TotalScore = SaturatingAdd(Data.TotalScore, 1);
            BadgeCatalog.Credit(Data);
            if (perfect) Data.TotalPerfects = SaturatingAdd(Data.TotalPerfects, 1);
            Data.LongestCombo = Math.Max(Data.LongestCombo, run.LongestCombo);
            Data.LongestPerfectStreak = Math.Max(Data.LongestPerfectStreak, run.LongestPerfectStreak);
            Data.ProgressPoints = SaturatingAdd(Data.ProgressPoints, matchPoints);
            run.ProgressEarned = SaturatingAdd(run.ProgressEarned, matchPoints);
            int goalPoints = CreditGoals(dayKey, perfect, combo);
            run.ProgressEarned = SaturatingAdd(run.ProgressEarned, goalPoints);
            run.GoalProgressEarned = SaturatingAdd(run.GoalProgressEarned, goalPoints);
            UpdateRecords(run);
            Save();
            return new MatchCreditResult(true, matchPoints, goalPoints);
        }

        public bool FinalizeRun(LocalRunSummary summary)
        {
            var run = Data.ActiveRun;
            if (summary == null || run == null || run.Finalized || run.RunId != summary.RunId) return false;
            // Trust the already credited ledger, never award from an arbitrary final score.
            run.Finalized = true;
            UpdateRecords(run);
            if (Data.GamesPlayed < int.MaxValue) Data.GamesPlayed++;
            var record = GetRecord(run.Mode, run.Board);
            if (record.GamesPlayed < int.MaxValue) record.GamesPlayed++;
            Save();
            return true;
        }

        public ModeRecord GetRecord(string mode, string board)
        {
            if (!IsMode(mode) || !IsBoard(board)) throw new ArgumentException("Unknown game mode or board style.");
            foreach (var record in Data.Records)
                if (record.Mode == mode && record.Board == board) return record;
            var added = new ModeRecord { Mode = mode, Board = board };
            Data.Records.Add(added);
            return added;
        }

        public bool ShouldSuggestRush()
        {
            if (Data.RushSuggestionShown) return false;
            var lively = GetRecord("Flow", "Lively");
            var still = GetRecord("Flow", "Still");
            return (long)lively.GamesPlayed + still.GamesPlayed >= 3
                && Math.Max(lively.HighScore, still.HighScore) >= 40;
        }

        public ProgressSnapshot GetProgressSnapshot()
        {
            var next = CosmeticCatalog.NextUnlock(Data.ProgressPoints);
            long milestone = Data.TotalScore < 100 ? 100 : Data.TotalScore < 500 ? 500
                : Data.TotalScore < 1000 ? 1000
                : Data.TotalScore > long.MaxValue - 1000 ? long.MaxValue : (Data.TotalScore / 1000 + 1) * 1000;
            return new ProgressSnapshot
            {
                Points = Data.ProgressPoints,
                NextUnlock = next,
                PointsToNextUnlock = next == null ? 0 : next.UnlockAt - Data.ProgressPoints,
                TotalMatches = Data.TotalScore,
                NextMatchMilestone = milestone
            };
        }

        public string GetEquipped(CosmeticCategory category)
        {
            switch (category)
            {
                case CosmeticCategory.Puck: return Data.EquippedPuck;
                case CosmeticCategory.Trail: return Data.EquippedTrail;
                case CosmeticCategory.Ring: return Data.EquippedRing;
                case CosmeticCategory.Background: return Data.EquippedBackground;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public bool Equip(CosmeticCategory category, string id)
        {
            if (!CosmeticCatalog.IsUnlocked(category, id, Data.ProgressPoints)) return false;
            switch (category)
            {
                case CosmeticCategory.Puck: Data.EquippedPuck = id; break;
                case CosmeticCategory.Trail: Data.EquippedTrail = id; break;
                case CosmeticCategory.Ring: Data.EquippedRing = id; break;
                case CosmeticCategory.Background: Data.EquippedBackground = id; break;
                default: return false;
            }
            Save();
            return true;
        }

        public IReadOnlyList<DailyGoalProgress> GetDailyGoals(string dayKey = null)
        {
            dayKey = ResolveDayKey(dayKey);
            if (AdvanceGoalDay(dayKey)) Save();
            int[] ids = GoalIds(dayKey);
            bool eligible = Data.DailyGoals != null && Data.DailyGoals.DayKey == dayKey;
            return new[] { GoalSnapshot(ids[0], eligible, true), GoalSnapshot(ids[1], eligible, false) };
        }

        /// <summary>Call once on a completed run, never for an abandoned run.</summary>
        public void RecordGame(int score)
        {
            Sanitize(Data);
            score = Math.Max(0, score);
            Data.HighScore = Math.Max(Data.HighScore, score);
            if (Data.GamesPlayed < int.MaxValue) Data.GamesPlayed++;
            Data.TotalScore = Data.TotalScore > long.MaxValue - score
                ? long.MaxValue : Data.TotalScore + score;
            Data.ProgressPoints = SaturatingAdd(Data.ProgressPoints, score);
            var record = GetRecord("Rush", "Lively");
            record.HighScore = Math.Max(record.HighScore, score);
            if (record.GamesPlayed < int.MaxValue) record.GamesPlayed++;
            Save();
        }

        public void CompleteTutorial()
        {
            Data.TutorialCompleted = true;
            Save();
        }

        private static void Sanitize(PlayerProgress data)
        {
            BadgeCatalog.Initialize(data);
            data.SchemaVersion = CurrentSchemaVersion;
            data.HighScore = Math.Max(0, data.HighScore);
            data.GamesPlayed = Math.Max(0, data.GamesPlayed);
            data.TotalScore = Math.Max(0L, data.TotalScore);
            data.ProgressPoints = Math.Max(0L, data.ProgressPoints);
            data.TotalPerfects = Math.Max(0L, data.TotalPerfects);
            data.LongestCombo = Math.Max(0, data.LongestCombo);
            data.LongestPerfectStreak = Math.Max(0, data.LongestPerfectStreak);
            data.RunSequence = Math.Max(0L, data.RunSequence);
            if (string.IsNullOrEmpty(data.SaveId)) data.SaveId = Guid.NewGuid().ToString("N");
            if (!IsMode(data.SelectedMode) || data.SelectedMode == "Daily") data.SelectedMode = "Flow";
            if (!IsBoard(data.SelectedBoard)) data.SelectedBoard = "Lively";
            data.Records = data.Records ?? new List<ModeRecord>();
            data.Records.RemoveAll(record => record == null || !IsMode(record.Mode) || !IsBoard(record.Board));
            var seen = new HashSet<string>();
            data.Records.RemoveAll(record => !seen.Add(record.Mode + "/" + record.Board));
            foreach (var record in data.Records)
            {
                record.HighScore = Math.Max(0, record.HighScore);
                record.GamesPlayed = Math.Max(0, record.GamesPlayed);
                record.LongestCombo = Math.Max(0, record.LongestCombo);
                record.LongestPerfectStreak = Math.Max(0, record.LongestPerfectStreak);
                record.MostPerfects = Math.Max(0, record.MostPerfects);
            }
            data.EquippedPuck = ValidEquipment(CosmeticCategory.Puck, data.EquippedPuck, data.ProgressPoints);
            data.EquippedTrail = ValidEquipment(CosmeticCategory.Trail, data.EquippedTrail, data.ProgressPoints);
            data.EquippedRing = ValidEquipment(CosmeticCategory.Ring, data.EquippedRing, data.ProgressPoints);
            data.EquippedBackground = ValidEquipment(CosmeticCategory.Background, data.EquippedBackground, data.ProgressPoints);
            var run = data.ActiveRun;
            if (run != null && (string.IsNullOrEmpty(run.RunId) || !IsMode(run.Mode) || !IsBoard(run.Board)
                || run.MatchIndex < 0)) data.ActiveRun = null;
            else if (run != null)
            {
                run.PerfectCount = Math.Max(0, Math.Min(run.MatchIndex, run.PerfectCount));
                run.LongestCombo = Math.Max(0, Math.Min(run.MatchIndex, run.LongestCombo));
                run.LongestPerfectStreak = Math.Max(0, Math.Min(run.PerfectCount, run.LongestPerfectStreak));
                run.ProgressEarned = Math.Max(0L, run.ProgressEarned);
                run.GoalProgressEarned = Math.Max(0L, run.GoalProgressEarned);
            }
            if (data.DailyGoals != null && !TryDay(data.DailyGoals.DayKey, out _)) data.DailyGoals = null;
            else if (data.DailyGoals != null)
            {
                data.DailyGoals.Matches = Math.Max(0, data.DailyGoals.Matches);
                data.DailyGoals.Perfects = Math.Max(0, data.DailyGoals.Perfects);
                data.DailyGoals.BestCombo = Math.Max(0, data.DailyGoals.BestCombo);
            }
        }

        private static void Migrate(PlayerProgress data)
        {
            if (data.SchemaVersion >= CurrentSchemaVersion) return;
            data.Records = data.Records ?? new List<ModeRecord>();
            data.Records.Add(new ModeRecord
            {
                Mode = "Rush", Board = "Lively", HighScore = Math.Max(0, data.HighScore),
                GamesPlayed = Math.Max(0, data.GamesPlayed), LongestCombo = Math.Max(0, data.HighScore)
            });
            data.ProgressPoints = Math.Max(0L, data.TotalScore);
            data.LongestCombo = Math.Max(0, data.HighScore);
            data.SchemaVersion = CurrentSchemaVersion;
        }

        private void UpdateRecords(RunCreditLedger run)
        {
            Data.HighScore = Math.Max(Data.HighScore, run.MatchIndex);
            var record = GetRecord(run.Mode, run.Board);
            record.HighScore = Math.Max(record.HighScore, run.MatchIndex);
            record.LongestCombo = Math.Max(record.LongestCombo, run.LongestCombo);
            record.LongestPerfectStreak = Math.Max(record.LongestPerfectStreak, run.LongestPerfectStreak);
            record.MostPerfects = Math.Max(record.MostPerfects, run.PerfectCount);
        }

        private int CreditGoals(string dayKey, bool perfect, int combo)
        {
            dayKey = ResolveDayKey(dayKey);
            AdvanceGoalDay(dayKey);
            var goals = Data.DailyGoals;
            if (goals == null || goals.DayKey != dayKey) return 0;
            if (goals.Matches < int.MaxValue) goals.Matches++;
            if (perfect && goals.Perfects < int.MaxValue) goals.Perfects++;
            goals.BestCombo = Math.Max(goals.BestCombo, combo);
            int[] ids = GoalIds(dayKey);
            int points = 0;
            if (!goals.FirstAwarded && GoalValue(ids[0], goals) >= GoalTarget(ids[0]))
            { goals.FirstAwarded = true; points += 10; }
            if (!goals.SecondAwarded && GoalValue(ids[1], goals) >= GoalTarget(ids[1]))
            { goals.SecondAwarded = true; points += 10; }
            Data.ProgressPoints = SaturatingAdd(Data.ProgressPoints, points);
            return points;
        }

        private bool AdvanceGoalDay(string dayKey)
        {
            dayKey = ResolveDayKey(dayKey);
            if (!TryDay(dayKey, out _)) return false;
            if (Data.DailyGoals != null && string.CompareOrdinal(Data.DailyGoals.DayKey, dayKey) >= 0) return false;
            Data.DailyGoals = new DailyGoalState { DayKey = dayKey };
            return true;
        }

        private DailyGoalProgress GoalSnapshot(int id, bool eligible, bool first)
        {
            return new DailyGoalProgress
            {
                Id = id == 0 ? "matches" : id == 1 ? "perfects" : "combo",
                Label = id == 0 ? "Land 25 matches" : id == 1 ? "Land 10 Perfects" : "Build a 10-match combo",
                Target = GoalTarget(id),
                Current = eligible ? Math.Min(GoalTarget(id), GoalValue(id, Data.DailyGoals)) : 0,
                Awarded = eligible && (first ? Data.DailyGoals.FirstAwarded : Data.DailyGoals.SecondAwarded),
                Eligible = eligible
            };
        }

        private static int[] GoalIds(string dayKey)
        {
            if (!TryDay(dayKey, out var date)) return new[] { 0, 1 };
            int omitted = (int)(date.Ticks / TimeSpan.TicksPerDay % 3);
            return omitted == 0 ? new[] { 1, 2 } : omitted == 1 ? new[] { 0, 2 } : new[] { 0, 1 };
        }

        private static int GoalTarget(int id) => id == 0 ? 25 : 10;
        private static int GoalValue(int id, DailyGoalState goals) => id == 0 ? goals.Matches : id == 1 ? goals.Perfects : goals.BestCombo;
        private static string ResolveDayKey(string dayKey) => dayKey ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        private static bool TryDay(string key, out DateTime day) => DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        private static bool IsMode(string mode) => mode == "Flow" || mode == "Rush" || mode == "Daily";
        private static bool IsBoard(string board) => board == "Lively" || board == "Still";
        private static long SaturatingAdd(long value, int add) => value > long.MaxValue - add ? long.MaxValue : value + add;
        private static string ValidEquipment(CosmeticCategory category, string id, long points) => CosmeticCatalog.IsUnlocked(category, id, points) ? id : "classic";
    }
}

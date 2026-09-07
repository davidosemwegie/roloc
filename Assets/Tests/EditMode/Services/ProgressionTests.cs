using System;
using System.IO;
using NUnit.Framework;

namespace Roloc.Services.Tests
{
    public sealed class ProgressionTests
    {
        private string directory;
        private const string Day = "2026-09-06";

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-progress-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(directory, true);

        [Test]
        public void LegacySaveMigratesOnceToRushLivelyAndRetainsPreferences()
        {
            File.WriteAllText(Path.Combine(directory, SaveService.FileName),
                "{\"HighScore\":42,\"GamesPlayed\":5,\"TotalScore\":103,\"MusicEnabled\":false,\"TutorialCompleted\":true}");
            var saves = new SaveService(directory);
            Assert.That(saves.Data.SchemaVersion, Is.EqualTo(SaveService.CurrentSchemaVersion));
            Assert.That(saves.GetRecord("Rush", "Lively").HighScore, Is.EqualTo(42));
            Assert.That(saves.GetRecord("Rush", "Lively").GamesPlayed, Is.EqualTo(5));
            Assert.That(saves.GetRecord("Flow", "Lively").HighScore, Is.Zero);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(103));
            Assert.That(saves.Data.MusicEnabled, Is.False);
            Assert.That(saves.Data.TutorialCompleted, Is.True);
            saves.Save();
            Assert.That(new SaveService(directory).Data.ProgressPoints, Is.EqualTo(103));
        }

        [Test]
        public void PreferencesDefaultToFlowAndPersistIndependently()
        {
            var saves = new SaveService(directory);
            Assert.That(saves.Data.SelectedMode, Is.EqualTo("Flow"));
            Assert.That(saves.Data.SelectedBoard, Is.EqualTo("Lively"));
            Assert.That(saves.Data.HapticsEnabled, Is.True);
            Assert.That(saves.Data.SymbolsEnabled, Is.False);
            saves.Data.SelectedMode = "Rush";
            saves.Data.SelectedBoard = "Still";
            saves.Data.HapticsEnabled = false;
            saves.Data.SymbolsEnabled = true;
            saves.Data.ReduceEffects = true;
            saves.Data.EffectsPreferenceInitialized = true;
            saves.Data.ChancesHintShown = true;
            saves.Data.PerfectHintShown = true;
            saves.Data.RecoveryHintShown = true;
            saves.Data.RushSuggestionShown = true;
            saves.Flush();
            var loaded = new SaveService(directory).Data;
            Assert.That(loaded.SelectedMode, Is.EqualTo("Rush"));
            Assert.That(loaded.SelectedBoard, Is.EqualTo("Still"));
            Assert.That(loaded.HapticsEnabled, Is.False);
            Assert.That(loaded.SymbolsEnabled && loaded.ReduceEffects && loaded.EffectsPreferenceInitialized, Is.True);
            Assert.That(loaded.ChancesHintShown && loaded.PerfectHintShown && loaded.RecoveryHintShown && loaded.RushSuggestionShown, Is.True);
        }

        [Test]
        public void EachMatchIsBankedBeforeRunCompletionAndPerfectAddsOne()
        {
            var saves = new SaveService(directory);
            var id = saves.StartRun("Flow", "Lively", Day);
            Assert.That(saves.RecordMatch(id, 1, "Flow", "Lively", false, 1, 0, Day).Earned, Is.EqualTo(1));
            Assert.That(saves.RecordMatch(id, 2, "Flow", "Lively", true, 2, 1, Day).Earned, Is.EqualTo(2));
            var loaded = new SaveService(directory);
            Assert.That(loaded.Data.ProgressPoints, Is.EqualTo(3));
            Assert.That(loaded.Data.TotalScore, Is.EqualTo(2));
            Assert.That(loaded.Data.TotalPerfects, Is.EqualTo(1));
            Assert.That(loaded.Data.GamesPlayed, Is.Zero);
            Assert.That(loaded.GetRecord("Flow", "Lively").HighScore, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateOutOfOrderAndMismatchedEventsCannotAwardProgress()
        {
            var saves = new SaveService(directory);
            var id = saves.StartRun("Flow", "Lively", Day);
            Assert.That(saves.RecordMatch(id, 2, "Flow", "Lively", true, 2, 2, Day).Accepted, Is.False);
            Assert.That(saves.RecordMatch(id, 1, "Rush", "Lively", true, 1, 1, Day).Accepted, Is.False);
            Assert.That(saves.RecordMatch(id, 1, "Flow", "Still", true, 1, 1, Day).Accepted, Is.False);
            Assert.That(saves.RecordMatch(id, 1, "Flow", "Lively", true, 1, 1, Day).Accepted, Is.True);
            var loaded = new SaveService(directory);
            Assert.That(loaded.RecordMatch(id, 1, "Flow", "Lively", true, 1, 1, Day).Accepted, Is.False);
            Assert.That(loaded.Data.ProgressPoints, Is.EqualTo(2));
        }

        [Test]
        public void StartingAnotherRunInvalidatesOldEventsWithoutLosingCredit()
        {
            var saves = new SaveService(directory);
            var oldId = saves.StartRun("Rush", "Still", Day);
            saves.RecordMatch(oldId, 1, "Rush", "Still", true, 1, 1, Day);
            var newId = saves.StartRun("Flow", "Lively", Day);
            Assert.That(newId, Is.Not.EqualTo(oldId));
            Assert.That(saves.RecordMatch(oldId, 2, "Rush", "Still", true, 2, 2, Day).Accepted, Is.False);
            Assert.That(saves.FinalizeRun(new LocalRunSummary { RunId = oldId, Score = 1 }), Is.False);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(2));
        }

        [Test]
        public void FinalizationRecordsCompletionExactlyOnceAndCannotInflateScore()
        {
            var saves = new SaveService(directory);
            var id = saves.StartRun("Daily", "Still", Day);
            saves.RecordMatch(id, 1, "Daily", "Still", true, 1, 1, Day);
            var summary = new LocalRunSummary { RunId = id, Score = 999, PerfectCount = 999 };
            Assert.That(saves.FinalizeRun(summary), Is.True);
            Assert.That(new SaveService(directory).FinalizeRun(summary), Is.False);
            Assert.That(saves.RecordMatch(id, 2, "Daily", "Still", true, 2, 2, Day).Accepted, Is.False);
            var loaded = new SaveService(directory);
            Assert.That(loaded.Data.ProgressPoints, Is.EqualTo(2));
            Assert.That(loaded.Data.TotalScore, Is.EqualTo(1));
            Assert.That(loaded.Data.GamesPlayed, Is.EqualTo(1));
            Assert.That(loaded.GetRecord("Daily", "Still").HighScore, Is.EqualTo(1));
            Assert.That(loaded.GetRecord("Daily", "Still").MostPerfects, Is.EqualTo(1));
        }

        [Test]
        public void RecordsKeepModesBoardsAndSkillBestsSeparate()
        {
            var saves = new SaveService(directory);
            var id = saves.StartRun("Flow", "Still", Day);
            saves.RecordMatch(id, 1, "Flow", "Still", true, 1, 1, Day);
            saves.RecordMatch(id, 2, "Flow", "Still", true, 2, 2, Day);
            saves.RecordMatch(id, 3, "Flow", "Still", false, 3, 0, Day);
            saves.RecordMatch(id, 4, "Flow", "Still", true, 1, 1, Day);
            saves.FinalizeRun(new LocalRunSummary { RunId = id });
            var record = saves.GetRecord("Flow", "Still");
            Assert.That(record.HighScore, Is.EqualTo(4));
            Assert.That(record.LongestCombo, Is.EqualTo(3));
            Assert.That(record.LongestPerfectStreak, Is.EqualTo(2));
            Assert.That(record.MostPerfects, Is.EqualTo(3));
            Assert.That(saves.GetRecord("Flow", "Lively").HighScore, Is.Zero);
            Assert.That(saves.GetRecord("Rush", "Still").HighScore, Is.Zero);
            Assert.That(saves.Data.LongestPerfectStreak, Is.EqualTo(2));
        }

        [Test]
        public void EquipmentRejectsLockedUnknownAndWrongCategoryIdsAndSurvivesRelaunch()
        {
            var saves = new SaveService(directory);
            Assert.That(saves.Equip(CosmeticCategory.Puck, "glass"), Is.False);
            saves.Data.ProgressPoints = 180;
            Assert.That(saves.Equip(CosmeticCategory.Puck, "glass"), Is.True);
            Assert.That(saves.Equip(CosmeticCategory.Trail, "ribbon"), Is.True);
            Assert.That(saves.Equip(CosmeticCategory.Ring, "porcelain"), Is.True);
            Assert.That(saves.Equip(CosmeticCategory.Ring, "glass"), Is.False);
            Assert.That(saves.Equip(CosmeticCategory.Background, "unknown"), Is.False);
            var loaded = new SaveService(directory);
            Assert.That(loaded.GetEquipped(CosmeticCategory.Puck), Is.EqualTo("glass"));
            Assert.That(loaded.GetEquipped(CosmeticCategory.Trail), Is.EqualTo("ribbon"));
            Assert.That(loaded.GetEquipped(CosmeticCategory.Ring), Is.EqualTo("porcelain"));
            Assert.That(loaded.GetEquipped(CosmeticCategory.Background), Is.EqualTo("classic"));
        }

        [TestCase(0, "glass", 40)]
        [TestCase(40, "ribbon", 60)]
        [TestCase(100, "porcelain", 80)]
        [TestCase(180, "dusk", 100)]
        [TestCase(280, "pearl", 120)]
        [TestCase(400, "orbit", 150)]
        public void ProgressSnapshotExplainsNextUnlock(int points, string nextId, int remaining)
        {
            var saves = new SaveService(directory);
            saves.Data.ProgressPoints = points;
            var snapshot = saves.GetProgressSnapshot();
            Assert.That(snapshot.NextUnlock.Id, Is.EqualTo(nextId));
            Assert.That(snapshot.PointsToNextUnlock, Is.EqualTo(remaining));
            Assert.That(snapshot.AllUnlocked, Is.False);
        }

        [Test]
        public void CompletedCollectionStillShowsCumulativeMatchMilestones()
        {
            var saves = new SaveService(directory);
            saves.Data.ProgressPoints = 550;
            saves.Data.TotalScore = 1001;
            var snapshot = saves.GetProgressSnapshot();
            Assert.That(snapshot.AllUnlocked, Is.True);
            Assert.That(snapshot.NextMatchMilestone, Is.EqualTo(2000));
            Assert.That(snapshot.MatchesToMilestone, Is.EqualTo(999));
        }

        [Test]
        public void DailyGoalsAreDeterministicAndEachPaysOnlyOnceAcrossRelaunch()
        {
            var saves = new SaveService(directory);
            var initialGoals = saves.GetDailyGoals(Day);
            var reloadGoals = new SaveService(directory).GetDailyGoals(Day);
            Assert.That(initialGoals.Count, Is.EqualTo(2));
            Assert.That(initialGoals[0].Id, Is.EqualTo(reloadGoals[0].Id));
            Assert.That(initialGoals[1].Id, Is.EqualTo(reloadGoals[1].Id));
            Assert.That(initialGoals[0].Id, Is.Not.EqualTo(initialGoals[1].Id));
            var id = saves.StartRun("Flow", "Lively", Day);
            for (int i = 1; i <= 25; i++) saves.RecordMatch(id, i, "Flow", "Lively", true, i, i, Day);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(70));
            saves = new SaveService(directory);
            for (int i = 26; i <= 50; i++) saves.RecordMatch(id, i, "Flow", "Lively", true, i, i, Day);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(120));
            foreach (var goal in saves.GetDailyGoals(Day)) Assert.That(goal.Awarded, Is.True);
        }

        [Test]
        public void ClockRollbackCannotReclaimHistoricalGoalRewards()
        {
            var saves = new SaveService(directory);
            var id = saves.StartRun("Flow", "Lively", Day);
            for (int i = 1; i <= 25; i++) saves.RecordMatch(id, i, "Flow", "Lively", true, i, i, Day);
            for (int i = 26; i <= 50; i++) saves.RecordMatch(id, i, "Flow", "Lively", true, i, i, "2026-09-07");
            saves = new SaveService(directory);
            for (int i = 51; i <= 75; i++) saves.RecordMatch(id, i, "Flow", "Lively", true, i, i, Day);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(190)); // 150 match credit + two days' rewards.
            foreach (var goal in saves.GetDailyGoals(Day)) Assert.That(goal.Eligible, Is.False);
        }

        [Test]
        public void OverflowClampsProgressAndMilestones()
        {
            var saves = new SaveService(directory);
            saves.Data.ProgressPoints = long.MaxValue - 1;
            saves.Data.TotalPerfects = long.MaxValue;
            saves.Data.TotalScore = long.MaxValue;
            var id = saves.StartRun("Rush", "Still", Day);
            saves.RecordMatch(id, 1, "Rush", "Still", true, 1, 1, Day);
            Assert.That(saves.Data.ProgressPoints, Is.EqualTo(long.MaxValue));
            Assert.That(saves.Data.TotalPerfects, Is.EqualTo(long.MaxValue));
            Assert.That(saves.Data.TotalScore, Is.EqualTo(long.MaxValue));
            Assert.That(saves.GetProgressSnapshot().NextMatchMilestone, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void RushSuggestionRequiresThreeFinishedFlowRunsAndARealBest()
        {
            var saves = new SaveService(directory);
            Assert.That(saves.ShouldSuggestRush(), Is.False);
            for (int runIndex = 0; runIndex < 3; runIndex++)
            {
                var id = saves.StartRun("Flow", "Still", Day);
                for (int match = 1; match <= 40; match++) saves.RecordMatch(id, match, "Flow", "Still", false, match, 0, Day);
                Assert.That(saves.ShouldSuggestRush(), Is.False);
                saves.FinalizeRun(new LocalRunSummary { RunId = id });
            }
            Assert.That(saves.ShouldSuggestRush(), Is.True);
            saves.Data.RushSuggestionShown = true;
            saves.Save();
            Assert.That(new SaveService(directory).ShouldSuggestRush(), Is.False);
        }

        [Test]
        public void InvalidModesCannotStartCreditedTutorialRuns()
        {
            var saves = new SaveService(directory);
            Assert.Throws<ArgumentException>(() => saves.StartRun("Tutorial", "Lively", Day));
            Assert.Throws<ArgumentException>(() => saves.StartRun("Flow", "Unknown", Day));
            Assert.That(saves.Data.ProgressPoints, Is.Zero);
        }
    }
}

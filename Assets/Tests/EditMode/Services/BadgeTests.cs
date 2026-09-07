using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roloc.Services;

namespace Roloc.Tests
{
    public sealed class BadgeTests
    {
        string directory;
        [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "ring-rush-badges-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test] public void CatalogHasSeparateRunAndLifetimeMilestones()
        {
            Assert.That(BadgeCatalog.All.Where(b => b.Track == BadgeTrack.Run).Select(b => b.Matches),
                Is.EqualTo(new[] {20,50,100,200,300,400,500,600,700,800,900,1000}));
            Assert.That(BadgeCatalog.All.Where(b => b.Track == BadgeTrack.Lifetime).Select(b => b.Matches),
                Is.EqualTo(new[] {100,500,1000,2000,5000,10000,25000,50000,100000}));
            Assert.That(BadgeCatalog.All.Select(b => b.Id).Distinct().Count(), Is.EqualTo(21));
        }

        [Test] public void HistoricalProgressBackfillsBothTracksWithoutNewNotifications()
        {
            var data = new PlayerProgress { HighScore = 50, TotalScore = 2100 };
            BadgeCatalog.Initialize(data);
            Assert.That(data.UnlockedBadges, Does.Contain("run-50"));
            Assert.That(data.UnlockedBadges, Does.Not.Contain("run-100"));
            Assert.That(data.UnlockedBadges, Does.Contain("lifetime-2000"));
            Assert.That(data.UnlockedBadges.Count, Is.EqualTo(6));
        }

        [Test] public void CreditedMatchUnlocksBothTracksOnceAndSurvivesRelaunch()
        {
            var saves = new SaveService(directory);
            saves.Data.TotalScore = 80;
            string run = saves.StartRun("Flow", "Lively");
            for (int i = 1; i <= 20; i++) saves.RecordMatch(run, i, "Flow", "Lively", false, i, 0);
            Assert.That(saves.Data.ActiveRun.NewBadges, Is.EquivalentTo(new[] { "run-20", "lifetime-100" }));
            Assert.That(saves.RecordMatch(run, 20, "Flow", "Lively", false, 20, 0).Accepted, Is.False);
            saves = new SaveService(directory);
            Assert.That(saves.Data.ActiveRun.NewBadges.Count, Is.EqualTo(2));
            run = saves.StartRun("Rush", "Still");
            for (int i = 1; i <= 20; i++) saves.RecordMatch(run, i, "Rush", "Still", false, i, 0);
            Assert.That(saves.Data.ActiveRun.NewBadges, Is.Empty);
            Assert.That(saves.Data.UnlockedBadges.Count, Is.EqualTo(2));
        }

        [Test] public void LargeLifetimeTotalDoesNotAwardSingleRunBadges()
        {
            var data = new PlayerProgress { BadgesInitialized = true, TotalScore = 100000, ActiveRun = new RunCreditLedger { MatchIndex = 1 } };
            BadgeCatalog.Credit(data); BadgeCatalog.Credit(data);
            Assert.That(data.UnlockedBadges.Count, Is.EqualTo(9));
            Assert.That(data.ActiveRun.NewBadges.Count, Is.EqualTo(9));
            Assert.That(data.UnlockedBadges.All(id => id.StartsWith("lifetime-")), Is.True);
        }
    }
}

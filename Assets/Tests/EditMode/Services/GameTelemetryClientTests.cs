using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Roloc.Services.Tests
{
    public sealed class GameTelemetryClientTests
    {
        private string directory;
        private DailyConnection connection;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "telemetry-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            connection = ScriptableObject.CreateInstance<DailyConnection>();
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(connection); Directory.Delete(directory, true); }
        private GameTelemetryClient Client(bool eligible = true) => (GameTelemetryClient)Activator.CreateInstance(typeof(GameTelemetryClient),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new DailyClient(connection, directory), directory, eligible }, null);
        private string PathFor(GameTelemetryClient client) => (string)typeof(GameTelemetryClient).GetField("cachePath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
        private string Saved(GameTelemetryClient client) => File.ReadAllText(PathFor(client));
        [Test] public void RelaunchAbandonsActiveRunAndPreservesLastBackgroundSnapshot()
        {
            var client = Client();
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:1", "flow");
            client.UpdateRun("c6f67d2eb65e488bb276f1bc65879cc9:1", 24, 1, 12000);
            var restored = Client();
            Assert.That(restored.PendingCount, Is.EqualTo(2));
            Assert.That(Saved(restored), Does.Contain("\"status\":\"abandoned\""));
            Assert.That(Saved(restored), Does.Contain("\"score\":24"));
            Assert.That(Saved(restored), Does.Not.Contain("\"status\":\"completed\""));
            Assert.That(Client().PendingCount, Is.EqualTo(2), "Relaunch must not add another abandonment.");
        }
        [Test] public void CompletionIsImmutableAndOptionalOptOutDoesNotDropRunHistory()
        {
            var client = Client();
            client.Capture("game_opened");
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:1", "rush");
            client.CompleteRun("c6f67d2eb65e488bb276f1bc65879cc9:1", 20, 2, 10000);
            client.CompleteRun("c6f67d2eb65e488bb276f1bc65879cc9:1", 999, 9, 99999);
            client.SetAnalyticsEnabled(false); // Local preference applies before coroutine iteration.
            client.Capture("leaderboard_viewed");
            Assert.That(client.AnalyticsEnabled, Is.False);
            Assert.That(client.PendingCount, Is.EqualTo(2));
            Assert.That(Saved(client), Does.Contain("\"score\":20"));
            Assert.That(Saved(client), Does.Not.Contain("999"));
            Assert.That(Client().AnalyticsEnabled, Is.False);
        }
        [Test] public void OfflineFlushPreservesEventsAndPendingPreference()
        {
            var client = Client();
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:1", "daily", "", "attempt");
            client.CompleteRun("c6f67d2eb65e488bb276f1bc65879cc9:1", 2, 0, 1000);
            client.SetAnalyticsEnabled(false);
            string error = null;
            Drive(client.Flush(null, value => error = value));
            Assert.That(error, Is.Not.Null);
            Assert.That(client.PendingCount, Is.EqualTo(2));
            Assert.That(Saved(client), Does.Contain("\"preferencePending\":true"));
        }
        [Test] public void QueueIsBoundedAndUsageIdsSurviveRestart()
        {
            var client = Client();
            for (int i = 0; i < 260; i++) client.Capture("game_opened");
            Assert.That(client.PendingCount, Is.EqualTo(256));
            string before = Saved(client);
            var restored = Client();
            Assert.That(restored.PendingCount, Is.EqualTo(256));
            Assert.That(Saved(restored), Is.EqualTo(before));
        }
        [Test] public void OldQueueItemsExpireAndDeploymentCachesRemainIsolated()
        {
            var client = Client();
            File.WriteAllText(PathFor(client), "{\"version\":1,\"analyticsEnabled\":false,\"pending\":[{\"usage\":{\"name\":\"game_opened\",\"occurredAt\":1}}]}");
            Assert.That(Client().PendingCount, Is.Zero);
            connection.url = "https://another.convex.cloud";
            Assert.That(Client().AnalyticsEnabled, Is.True);
        }
        [Test] public void OptOutThenReenableNeverBackfillsDeclinedRunAnalytics()
        {
            var client = Client();
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:1", "flow");
            client.SetAnalyticsEnabled(false);
            client.SetAnalyticsEnabled(true);
            client.CompleteRun("c6f67d2eb65e488bb276f1bc65879cc9:1", 20, 0, 10000);
            var saved = JsonUtility.FromJson<ConsentFixture>(Saved(client));
            Assert.That(saved.pending.Length, Is.EqualTo(2));
            Assert.That(saved.pending[0].run.analyticsEnabled, Is.False);
            Assert.That(saved.pending[1].run.analyticsEnabled, Is.False);
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:2", "rush");
            saved = JsonUtility.FromJson<ConsentFixture>(Saved(client));
            Assert.That(saved.pending[2].run.analyticsEnabled, Is.True);
        }
        [Serializable] private sealed class ConsentFixture { public ConsentItem[] pending; }
        [Serializable] private sealed class ConsentItem { public GameRunSummary run; }
        [Test] public void DevelopmentBuildKeepsHistoryWithoutChangingPreferenceOrCapturingUsage()
        {
            var client = Client(false);
            Assert.That(client.AnalyticsAvailable, Is.False);
            Assert.That(client.AnalyticsEnabled, Is.True);
            client.Capture("game_opened");
            client.StartRun("installation123:1", "flow");
            client.CompleteRun("installation123:1", 1, 0, 1000);
            var saved = JsonUtility.FromJson<ConsentFixture>(Saved(client));
            Assert.That(saved.pending.Length, Is.EqualTo(2));
            foreach (var item in saved.pending)
            {
                Assert.That(item.run.analyticsEligible, Is.False);
                Assert.That(item.run.analyticsEnabled, Is.False);
            }
            Assert.That(Client(false).AnalyticsEnabled, Is.True);
        }
        [Test] public void DevelopmentAndLegacyQueueCannotUpgradeWhenDistributionLaunches()
        {
            var dev = Client(false);
            dev.StartRun("installation123:1", "rush");
            dev.CompleteRun("installation123:1", 1, 0, 1000);
            string saved = Saved(dev).Replace("\"analyticsEligible\":false,", "").Replace(",\"analyticsEligible\":false", "");
            File.WriteAllText(PathFor(dev), saved);
            var release = Client(true);
            var restored = JsonUtility.FromJson<ConsentFixture>(Saved(release));
            foreach (var item in restored.pending) Assert.That(item.run.analyticsEligible, Is.False);
            release.Capture("game_opened");
            Assert.That(release.PendingCount, Is.EqualTo(3));
            var backToDev = Client(false);
            Assert.That(backToDev.PendingCount, Is.EqualTo(2), "Ineligible launch suppresses stored usage events.");
            Assert.That(Client(true).PendingCount, Is.EqualTo(2), "Suppressed events never return on a later release launch.");
        }
        [Test] public void MissingEligibilityUsageEventsAreDiscardedInRelease()
        {
            var client = Client(true);
            client.Capture("game_opened");
            File.WriteAllText(PathFor(client), Saved(client).Replace("\"analyticsEligible\":true,", "").Replace(",\"analyticsEligible\":true", ""));
            Assert.That(Client(true).PendingCount, Is.Zero);
        }
        [Test] public void InvalidModesAndEventNamesAreNotQueued()
        {
            var client = Client();
            client.StartRun("c6f67d2eb65e488bb276f1bc65879cc9:1", "invented");
            client.Capture("unknown_event");
            Assert.That(client.PendingCount, Is.Zero);
        }
        private static void Drive(IEnumerator coroutine)
        {
            while (coroutine.MoveNext())
                if (coroutine.Current is IEnumerator nested) Drive(nested);
                else Assert.Fail("Unexpected asynchronous operation.");
        }
    }
}

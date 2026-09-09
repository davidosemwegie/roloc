using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using Roloc.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public sealed class TelemetryPresentationTests
    {
        [Serializable] sealed class Usage { public string name; }
        [Serializable] sealed class Item { public GameRunSummary run; public Usage usage; }
        [Serializable] sealed class Snapshot { public bool hasActive; public GameRunSummary active; public List<Item> pending; }
        GameObject root;
        RolocGame game;
        GameTelemetryClient telemetry;
        DailyConnection connection;
        string directory;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-telemetry-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Telemetry test"); root.SetActive(false); root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>(); game.SaveDirectoryOverride = directory;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            root.SetActive(true); game.Saves.Data.TutorialCompleted = true;
            connection = ScriptableObject.CreateInstance<DailyConnection>();
            var daily = new DailyClient(connection, directory);
            Set("daily", daily);
            Set("leaderboards", new LeaderboardClient(daily, directory));
            telemetry = (GameTelemetryClient)Activator.CreateInstance(typeof(GameTelemetryClient),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { daily, directory, true }, null);
            Set("telemetry", telemetry);
            telemetry.Capture("game_opened");
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty); UnityEngine.Object.Destroy(connection); UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        object Invoke(string method, params object[] args) => typeof(RolocGame).GetMethod(method, Private).Invoke(game, args);
        T Get<T>(string field) => (T)typeof(RolocGame).GetField(field, Private).GetValue(game);
        void Set(string field, object value) => typeof(RolocGame).GetField(field, Private).SetValue(game, value);
        Snapshot Read() => JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(typeof(GameTelemetryClient).GetField("cache", Private).GetValue(telemetry)));

        [UnityTest] public IEnumerator EditorSettingsExplainUnavailableAnalyticsWithoutChangingPreference()
        {
            telemetry = new GameTelemetryClient(new DailyClient(connection, directory), directory);
            Set("telemetry", telemetry);
            bool preference = telemetry.AnalyticsEnabled;
            Invoke("ShowAnalyticsSettings", (Action)(() => game.ShowMenu()));
            var unavailable = root.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.GetComponentInChildren<UnityEngine.UI.Text>()?.text == "ANALYTICS UNAVAILABLE");
            Assert.That(unavailable.interactable, Is.False);
            Assert.That(telemetry.AnalyticsAvailable, Is.False);
            Assert.That(telemetry.AnalyticsEnabled, Is.EqualTo(preference));
            Assert.That(Read().pending.Any(item => item.usage != null && !string.IsNullOrEmpty(item.usage.name)), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator UnrankedRunKeepsLocalIdAndFinalizesExactlyOnceWithAnalyticsOff()
        {
            telemetry.SetAnalyticsEnabled(false);
            Invoke("BeginRunNow");
            string id = Get<string>("localRunId");
            Assert.That(Read().active.clientRunId, Is.EqualTo(id));
            Assert.That(Read().active.leaderboardRunId, Is.Empty);
            Assert.That(Get<LeaderboardTicket>("regularTicket"), Is.Null);
            game.Session.Drop(game.Session.ActiveColor, true);
            Set("telemetryRevives", 2);
            Invoke("FinishRun"); Invoke("FinishRun"); game.ShowMenu();
            var finals = Read().pending.Where(item => item.run != null && item.run.clientRunId == id && item.run.status != "started").ToArray();
            Assert.That(finals.Length, Is.EqualTo(1));
            Assert.That(finals[0].run.status, Is.EqualTo("completed"));
            Assert.That(finals[0].run.score, Is.EqualTo(1));
            Assert.That(finals[0].run.revives, Is.EqualTo(2));
            Assert.That(Read().pending.Any(item => item.usage != null && !string.IsNullOrEmpty(item.usage.name)), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator DailyPracticeAndRankedEachRecordTheirOwnHistory()
        {
            foreach (bool ranked in new[] { false, true })
            {
                Set("challenge", new DailyChallenge { id = "challenge_fixture", seed = 42, date = "2026-09-08", rulesVersion = 2,
                    variant = "lively", uploadDeadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3600000 });
                var attempt = ranked ? new DailyAttempt { attemptId = "daily_attempt_fixture", challengeId = "challenge_fixture" } : null;
                Invoke("LaunchDaily", (object)attempt);
                string id = Get<string>("localRunId");
                Assert.That(Read().active.mode, Is.EqualTo("daily"));
                Assert.That(Read().active.dailyAttemptId, Is.EqualTo(ranked ? attempt.attemptId : ""));
                game.ShowMenu(); game.ShowMenu();
                Assert.That(Read().pending.Count(item => item.run != null && item.run.clientRunId == id && item.run.status == "abandoned"), Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator BackgroundSnapshotsDoNotEndRunAndLongForegroundGapEmitsOnce()
        {
            Invoke("BeginRunNow"); game.Session.Drop(game.Session.ActiveColor, true);
            Set("applicationPaused", true); Invoke("TelemetryForegroundChanged");
            Assert.That(Read().hasActive, Is.True);
            Assert.That(Read().active.score, Is.EqualTo(1));
            Set("telemetryAwayAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 31 * 60 * 1000);
            Set("applicationPaused", false); Set("applicationFocused", true); Invoke("TelemetryForegroundChanged");
            Invoke("TelemetryForegroundChanged");
            Assert.That(Read().pending.Count(item => item.usage != null && item.usage.name == "game_opened"), Is.EqualTo(2), "One launch and one long-gap return.");
            yield return null;
        }

        [UnityTest] public IEnumerator TutorialAndReviveEventsAreBoundedAndDoNotCreateExtraRunSummaries()
        {
            game.BeginTutorial();
            Assert.That(Read().pending.Count(item => item.usage != null && item.usage.name == "tutorial_started"), Is.EqualTo(1));
            Assert.That(Read().hasActive, Is.False);
            Invoke("BeginRunNow");
            Invoke("CaptureRunTelemetry", "revive_offered");
            Invoke("CaptureRunTelemetry", "revive_requested");
            Invoke("CaptureRunTelemetry", "revive_completed");
            Assert.That(Read().pending.Count(item => item.run != null && item.run.status == "started"), Is.EqualTo(1));
            Assert.That(Read().pending.Count(item => item.usage != null && item.usage.name != null && item.usage.name.StartsWith("revive_")), Is.EqualTo(3));
            yield return null;
        }
    }
}

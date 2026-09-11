using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Roloc.Services.Tests
{
    public sealed class LeaderboardClientTests
    {
        private string directory;
        private DailyConnection connection;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "leaderboard-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            connection = ScriptableObject.CreateInstance<DailyConnection>();
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(connection); Directory.Delete(directory, true); }
        private LeaderboardClient Client() => new LeaderboardClient(new DailyClient(connection, directory), directory);
        private string CachePath(LeaderboardClient client) => (string)typeof(LeaderboardClient).GetField("cachePath", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
        private void Seed(bool ready)
        {
            File.WriteAllText(CachePath(Client()), "{\"version\":1,\"profile\":{\"nickname\":\"Player\",\"participating\":true},\"startingRequestId\":\"lost\",\"pending\":[{\"ready\":" + (ready ? "true" : "false") + ",\"score\":42,\"revives\":1,\"elapsedMs\":12000,\"ticket\":{\"runId\":\"issued\",\"mode\":\"flow\",\"status\":\"open\",\"startedAt\":1788820000000,\"uploadDeadline\":1788829200000}}]}");
        }
        [Test] public void CompletedQueueAndOptInSurviveRestartButIncompleteRunsDoNot()
        {
            Seed(true);
            Assert.That(Client().PendingCount, Is.EqualTo(1));
            Assert.That(Client().IsParticipating, Is.True);
            Seed(false);
            Assert.That(Client().PendingCount, Is.Zero);
        }
        [Test] public void OfflineRetryPreservesCompletedResultEvenBeyondItsDeadline()
        {
            Seed(true);
            string error = null;
            var client = Client();
            Drive(client.RetryPending(null, value => error = value));
            Assert.That(error, Does.Contain("not configured"));
            Assert.That(client.PendingCount, Is.EqualTo(1));
            Assert.That(Client().PendingCount, Is.EqualTo(1));
        }
        [Test] public void RetryKeepsTheOriginallyCompletedScoreAndReviveCount()
        {
            Seed(true);
            var client = Client();
            Drive(client.SubmitRun(new LeaderboardTicket { runId = "issued" }, 999, 9, 99999, null));
            string saved = File.ReadAllText(CachePath(client));
            Assert.That(saved, Does.Contain("\"score\":42"));
            Assert.That(saved, Does.Contain("\"revives\":1"));
            Assert.That(saved, Does.Not.Contain("999"));
            Assert.That(Client().PendingCount, Is.EqualTo(1));
        }
        [Test] public void UnissuedRunCannotUploadLocalHistory()
        {
            string error = null;
            var client = Client();
            Drive(client.SubmitRun(new LeaderboardTicket { runId = "invented" }, 100, 1, 10000, null, value => error = value));
            Assert.That(error, Does.Contain("server-issued"));
            Assert.That(client.PendingCount, Is.Zero);
        }
        [Test] public void OptOutDoesNotAttemptTicketStart()
        {
            string error = null;
            Drive(Client().StartRun("flow", _ => Assert.Fail(), value => error = value));
            Assert.That(error, Does.Contain("Join"));
        }
        [Test] public void OptOutAndZeroBestDoNotAttemptHistoricalUpload()
        {
            bool completed = false;
            Drive(Client().SyncBest(42, receipt => { Assert.That(receipt, Is.Null); completed = true; }, _ => Assert.Fail()));
            Assert.That(completed, Is.True);
            Seed(true); completed = false;
            Drive(Client().SyncBest(0, receipt => { Assert.That(receipt, Is.Null); completed = true; }, _ => Assert.Fail()));
            Assert.That(completed, Is.True);
        }
        [Test] public void OfflineHistoricalUploadLeavesDailyQueueAndLocalBestIntactForRetry()
        {
            Seed(true);
            var saves = new SaveService(directory);
            saves.GetRecord("Flow", "Lively").HighScore = 42;
            saves.Save();
            var client = Client(); string error = null;
            Drive(client.SyncBest(saves.GetRecord("Flow", "Lively").HighScore, _ => Assert.Fail(), value => error = value));
            Assert.That(error, Does.Contain("not configured"));
            Assert.That(client.BestSyncMessage, Does.Contain("will sync"));
            Assert.That(Client().PendingCount, Is.EqualTo(1));
            Assert.That(new SaveService(directory).GetRecord("Flow", "Lively").HighScore, Is.EqualTo(42));
        }
        [TestCase(-1)] [TestCase(65537)] public void InvalidHistoricalBestIsNotClampedOrUploaded(int score)
        {
            Seed(true); string error = null;
            Drive(Client().SyncBest(score, _ => Assert.Fail(), value => error = value));
            Assert.That(error, Does.Contain("cannot be ranked"));
        }
        [Test] public void HistoricalBestReceiptsDecodeConvexNumbersAndRejectMalformedValues()
        {
            var receipt = Read<LeaderboardBest>("{\"status\":\"success\",\"value\":{\"score\":42.0,\"status\":\"synced\"}}");
            Assert.That(receipt.score, Is.EqualTo(42)); Assert.That(receipt.status, Is.EqualTo("synced"));
            Assert.That(Read<LeaderboardBest>("{\"status\":\"success\",\"value\":{\"score\":42.0,\"status\":\"excluded\"}}").status, Is.EqualTo("excluded"));
            foreach (string value in new[] { "{\"score\":1.5,\"status\":\"synced\"}", "{\"score\":65537.0,\"status\":\"synced\"}", "{\"score\":42.0,\"status\":\"unknown\"}" })
                Assert.Throws<TargetInvocationException>(() => Read<LeaderboardBest>("{\"status\":\"success\",\"value\":" + value + "}"));
        }
        [Test] public void OfflineStartAbandonsRequestIdBeforeNextRun()
        {
            Seed(true);
            var client = Client();
            string error = null;
            Drive(client.StartRun("rush", _ => Assert.Fail(), value => error = value));
            Assert.That(error, Is.Not.Null);
            Assert.That(File.ReadAllText(CachePath(client)), Does.Not.Contain("lost"));
            Assert.That(client.PendingCount, Is.EqualTo(1));
        }
        [Test] public void MissingServerProfileDoesNotCreateAnOptedInIdentity()
        {
            Assert.That(Read<LeaderboardProfile>("{\"status\":\"success\",\"value\":null}"), Is.Null);
        }
        [TestCase(true)] [TestCase(false)] public void DelayedProfileReadsCannotUndoParticipationChanges(bool participating)
        {
            var client = Client();
            var beforeEdit = ReplyCallback<LeaderboardProfile>(client.LoadProfile(_ => { }));
            var save = ReplyCallback<LeaderboardProfile>(client.SetProfile("Player", participating, _ => { }));
            var duringEdit = ReplyCallback<LeaderboardProfile>(client.LoadProfile(_ => { }));
            save(new LeaderboardProfile { nickname = "Player", participating = participating });
            duringEdit(new LeaderboardProfile { nickname = "OldName", participating = !participating });
            beforeEdit(null);
            Assert.That(client.CachedProfile.nickname, Is.EqualTo("Player"));
            Assert.That(client.IsParticipating, Is.EqualTo(participating));
            Assert.That(Client().IsParticipating, Is.EqualTo(participating));
        }
        // Complete the actual transport callbacks in a chosen order without sending a network request.
        private static Action<T> ReplyCallback<T>(IEnumerator operation)
        {
            Assert.That(operation.MoveNext(), Is.True);
            var request = operation.Current;
            return (Action<T>)request.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(field => field.FieldType == typeof(Action<T>)).GetValue(request);
        }
        [Test] public void ConvexFloatNumbersAndNullablePersonalStandingDecode()
        {
            var board = Read<LeaderboardBoard>("{\"status\":\"success\",\"value\":{\"participants\":101.0,\"entries\":[{\"nickname\":\"Player\",\"score\":42.0,\"rank\":1.0}],\"personal\":null}}");
            Assert.That(board.participants, Is.EqualTo(101));
            Assert.That(board.entries[0].score, Is.EqualTo(42));
            Assert.That(board.entries[0].rank, Is.EqualTo(1));
            Assert.That(board.personal, Is.Null);
            var ticket = Read<LeaderboardTicket>("{\"status\":\"success\",\"value\":{\"runId\":\"issued\",\"mode\":\"flow\",\"status\":\"accepted\",\"score\":42.0,\"startedAt\":1.78882e12,\"uploadDeadline\":1788829200000.0}}");
            Assert.That(ticket.startedAt, Is.EqualTo(1788820000000d));
            Assert.That(ticket.uploadDeadline, Is.EqualTo(1788829200000d));
            Assert.That(ticket.score, Is.EqualTo(42));
            Assert.That(ticket.Terminal, Is.True);
        }
        private static T Read<T>(string json) where T : class
        {
            var method = typeof(DailyClient).GetMethod("DeserializeReply", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(typeof(T));
            var reply = method.Invoke(null, new object[] { json });
            return (T)reply.GetType().GetField("value").GetValue(reply);
        }
        private static void Drive(IEnumerator coroutine)
        {
            while (coroutine.MoveNext())
                if (coroutine.Current is IEnumerator nested) Drive(nested);
                else Assert.Fail("Unexpected asynchronous operation.");
        }
    }
}

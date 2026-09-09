using System;
using System.Collections;
using System.IO;
using System.Reflection;
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

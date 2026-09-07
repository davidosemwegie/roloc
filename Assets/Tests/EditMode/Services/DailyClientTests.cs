using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roloc.Services.Tests
{
    public sealed class DailyClientTests
    {
        private string directory;
        private DailyConnection connection;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-daily-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            connection = ScriptableObject.CreateInstance<DailyConnection>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(connection);
            Directory.Delete(directory, true);
        }

        [Test]
        public void RankedStartWirePayloadDeclaresTheRequiredFailureRulesRevision()
        {
            var argsType = typeof(DailyClient).GetNestedType("StartArgs", BindingFlags.NonPublic);
            var args = Activator.CreateInstance(argsType, true);
            argsType.GetField("challengeId").SetValue(args, "challenge");
            argsType.GetField("requestId").SetValue(args, "retry-stable-id");
            string json = JsonUtility.ToJson(args);
            Assert.That(json, Does.Contain("\"clientRulesRevision\":2"));
            Assert.That(json, Does.Contain("\"requestId\":\"retry-stable-id\""));
        }

        [Test]
        public void ChallengeRoundTripPreservesUnsignedSeedAndEpochMilliseconds()
        {
            const string json = "{\"id\":\"fixture\",\"date\":\"2026-09-06\",\"seed\":4294967295,\"rulesVersion\":1,\"variant\":\"lively\",\"opensAt\":1788652800000,\"closesAt\":1788739200000,\"uploadDeadline\":1788742800000}";
            var challenge = JsonUtility.FromJson<DailyChallenge>(json);
            var restored = JsonUtility.FromJson<DailyChallenge>(JsonUtility.ToJson(challenge));
            Assert.That(restored.seed, Is.EqualTo(4294967295L));
            Assert.That(restored.uploadDeadline, Is.EqualTo(1788742800000L));
            Assert.That(restored.Supported, Is.True);
            restored.rulesVersion = 2;
            Assert.That(restored.Supported, Is.False);
        }

        [TestCase("http://example.convex.cloud")]
        [TestCase("https://secret@example.convex.cloud")]
        [TestCase("https://example.convex.cloud/api")]
        public void InsecureOrNonDeploymentUrlsAreRejected(string url)
        {
            connection.url = url;
            Assert.That(new DailyClient(connection, directory).IsConfigured, Is.False);
        }

        [Test]
        public void MissingConnectionFailsWithoutAttemptingNetworking()
        {
            var client = new DailyClient(connection, directory);
            string error = null;
            bool completed = false;
            DriveSynchronously(client.LoadCurrent(_ => completed = true, value => error = value));
            Assert.That(error, Does.Contain("not configured"));
            Assert.That(completed, Is.False);
        }

        [Test]
        public void AnUnissuedAttemptCannotQueueAResult()
        {
            var client = new DailyClient(connection, directory);
            string error = null;
            DriveSynchronously(client.SubmitRun(new DailyAttempt { attemptId = "invented", challengeId = "fixture" },
                new[] { new DailyTraceEvent { kind = "abandon" } }, _ => Assert.Fail("Unissued attempt was accepted."), value => error = value));
            Assert.That(error, Does.Contain("server-issued"));
            Assert.That(client.PendingCount, Is.Zero);
        }

        [Test]
        public void ConvexNullableRankingFieldsDeserializeThroughTheActualGenericEnvelope()
        {
            Type wrapper = typeof(DailyClient).Assembly.GetType("Roloc.Services.DailyReply`1").MakeGenericType(typeof(DailyStanding));
            const string json = "{\"status\":\"success\",\"value\":{\"challengeId\":\"today\",\"bestScore\":null,\"percentile\":null,\"topPercent\":null,\"participants\":0,\"waiting\":true}}";
            object reply = null;
            Assert.DoesNotThrow(() => reply = JsonUtility.FromJson(json, wrapper));
            var standing = (DailyStanding)wrapper.GetField("value").GetValue(reply);
            Assert.That(standing, Is.Not.Null);
            Assert.That(standing.waiting, Is.True);
            var isNull = typeof(DailyClient).GetMethod("FieldIsNull", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(isNull.Invoke(null, new object[] { json, "bestScore" }), Is.True);
            Assert.That(isNull.Invoke(null, new object[] { json.Replace("\"bestScore\":null", "\"bestScore\":0"), "bestScore" }), Is.False);
        }

        [TestCase("1788829200000.0")]
        [TestCase("1.7888292e12")]
        [TestCase("1788829200000")]
        public void ActualConvexFloatEpochsPreserveOpenAttemptDeadline(string deadline)
        {
            // Shape and numeric spelling captured from daily:current on 2026-09-07.
            string challengeJson = "{\"status\":\"success\",\"value\":{\"closesAt\":1788825600000.0,\"date\":\"2026-09-07\",\"id\":\"fixture\",\"opensAt\":1788739200000.0,\"publicCompetitionEnabled\":false,\"rankedEnabled\":true,\"rulesVersion\":1.0,\"seed\":4294967295.0,\"serverNow\":1788742091132.0,\"uploadDeadline\":" + deadline + ",\"variant\":\"still\"}}";
            string attemptJson = "{\"status\":\"success\",\"value\":{\"attemptId\":\"issued\",\"challengeId\":\"fixture\",\"status\":\"open\",\"nextChunkIndex\":0.0,\"score\":null,\"reason\":null,\"uploadDeadline\":" + deadline + "}}";
            var challenge = ReadWireValue<DailyChallenge>(challengeJson);
            var attempt = ReadWireValue<DailyAttempt>(attemptJson);
            Assert.That(challenge.seed, Is.EqualTo(4294967295L));
            Assert.That(challenge.serverNow, Is.EqualTo(1788742091132L));
            Assert.That(challenge.opensAt, Is.EqualTo(1788739200000L));
            Assert.That(challenge.closesAt, Is.EqualTo(1788825600000L));
            Assert.That(challenge.uploadDeadline, Is.EqualTo(1788829200000L));
            Assert.That(attempt.uploadDeadline, Is.EqualTo(challenge.uploadDeadline));
            Assert.That(attempt.uploadDeadline, Is.GreaterThan(challenge.serverNow), "A new open attempt must not expire in the local upload check.");
            Assert.That(attempt.status, Is.EqualTo("open"));
        }

        [TestCase("0")]
        [TestCase("-1.0")]
        [TestCase("1788829200000.5")]
        [TestCase("1e30")]
        public void InvalidWireDeadlinesAreRejectedRatherThanDeletingTheQueuedResult(string deadline)
        {
            string json = "{\"status\":\"success\",\"value\":{\"attemptId\":\"issued\",\"status\":\"open\",\"uploadDeadline\":" + deadline + "}}";
            var exception = Assert.Throws<TargetInvocationException>(() => ReadWireValue<DailyAttempt>(json));
            Assert.That(exception.InnerException, Is.TypeOf<FormatException>());
        }

        [Test]
        public void CachedChallengeAndPendingTraceSurviveRelaunchAndStayDeploymentIsolated()
        {
            connection.url = "https://fixture.convex.cloud";
            var client = new DailyClient(connection, directory);
            string path = (string)typeof(DailyClient).GetField("cachePath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(client);
            File.WriteAllText(path, "{\"version\":1,\"challenge\":{\"id\":\"today\",\"seed\":23,\"rulesVersion\":1,\"variant\":\"still\"},\"pending\":[{\"requestId\":\"fixed-request\",\"ready\":true,\"attempt\":{\"attemptId\":\"issued\",\"challengeId\":\"today\",\"status\":\"open\"},\"events\":[{\"kind\":\"abandon\",\"round\":0,\"tMs\":0,\"elapsedMs\":0,\"color\":-1,\"xQ\":0,\"yQ\":0}]}]}");
            var restored = new DailyClient(connection, directory);
            Assert.That(restored.CachedChallenge.id, Is.EqualTo("today"));
            Assert.That(restored.CachedChallenge.Supported, Is.True);
            Assert.That(restored.PendingCount, Is.EqualTo(1));
            connection.url = "https://other-fixture.convex.cloud";
            var isolated = new DailyClient(connection, directory);
            Assert.That(isolated.CachedChallenge, Is.Null);
            Assert.That(isolated.PendingCount, Is.Zero);
        }

        // Explicitly selected only for the configured development deployment; never a production CI test.
        [UnityTest, Explicit("Requires the ignored DailyConnection asset and the development Convex deployment.")]
        public IEnumerator DevelopmentHttpGuestAttemptAndStandingRoundTrip()
        {
            var config = Resources.Load<DailyConnection>("DailyConnection");
            if (config == null || config.url.TrimEnd('/') != "https://determined-aardvark-934.convex.cloud")
                Assert.Ignore("A Ring Rush development connection is required.");
            var client = new DailyClient(config, directory);
            string error = null;
            DailyChallenge challenge = null;
            yield return Drive(client.LoadCurrent(value => challenge = value, value => error = value));
            Assert.That(error, Is.Null);
            Assert.That(challenge, Is.Not.Null);
            Assert.That(challenge.Supported, Is.True);
            Assert.That(challenge.serverNow, Is.GreaterThan(0), "Server epoch milliseconds must survive HTTP decoding.");
            Assert.That(challenge.uploadDeadline, Is.GreaterThan(challenge.serverNow));
            DailyAttempt attempt = null;
            yield return Drive(client.StartRanked(challenge, value => attempt = value, value => error = value));
            Assert.That(error, Is.Null);
            Assert.That(attempt?.status, Is.EqualTo("open"));
            Assert.That(attempt.uploadDeadline, Is.EqualTo(challenge.uploadDeadline), "The issued attempt must retain the shared upload deadline.");
            DailyAttempt submitted = null;
            yield return Drive(client.SubmitRun(attempt, new[] { new DailyTraceEvent { kind = "abandon" } },
                value => submitted = value, value => error = value));
            Assert.That(error, Is.Null);
            Assert.That(submitted?.status, Is.EqualTo("accepted"), submitted?.reason);
            Assert.That(submitted.score, Is.Zero);
            DailyStanding standing = null;
            yield return Drive(client.GetStanding(attempt.challengeId, value => standing = value, value => error = value));
            Assert.That(error, Is.Null);
            Assert.That(standing, Is.Not.Null);
            Assert.That(standing.hasBestScore, Is.True, "A legitimate zero score must not deserialize as no result.");
            Assert.That(standing.participants, Is.GreaterThan(0));
            Assert.That(client.PendingCount, Is.Zero);
        }

        private static void DriveSynchronously(IEnumerator coroutine)
        {
            while (coroutine.MoveNext())
            {
                if (coroutine.Current is IEnumerator nested) DriveSynchronously(nested);
                else Assert.Fail("This test must not perform network or asynchronous work.");
            }
        }

        private static T ReadWireValue<T>(string json) where T : class
        {
            MethodInfo deserialize = typeof(DailyClient).GetMethod("DeserializeReply", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(T));
            object reply = deserialize.Invoke(null, new object[] { json });
            return (T)reply.GetType().GetField("value").GetValue(reply);
        }

        private static IEnumerator Drive(IEnumerator coroutine)
        {
            while (coroutine.MoveNext())
            {
                if (coroutine.Current is IEnumerator nested) yield return Drive(nested);
                else if (coroutine.Current is AsyncOperation operation) { while (!operation.isDone) yield return null; }
                else yield return null;
            }
        }
    }
}

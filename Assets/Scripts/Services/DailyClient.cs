using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Roloc.Services
{
    /// <summary>Local gameplay never waits on this client. All callbacks run on Unity's coroutine thread.</summary>
    public sealed class DailyClient
    {
        public const int ChunkSize = 128;
        public const int MaxTraceEvents = ChunkSize * 512;
        private const int MaxPendingAttempts = 32;
        private readonly DailyConnection connection;
        private readonly string deploymentUrl;
        private readonly string cachePath;
        private readonly string credentialKey;
        private DailyCache cache = new DailyCache();
        private DailyTokenPair tokens;
        private bool authenticating;
        private bool uploading;
        private bool starting;
        private bool hasServerClock;
        private string lastErrorCode;
        private bool lastAuthenticationSucceeded;
        private long serverTimeAtSync;
        private double realtimeAtSync;

        public DailyChallenge CachedChallenge => cache.challenge;
        internal string CacheNamespace => DeploymentKey(deploymentUrl ?? "unconfigured");
        internal string LastErrorCode => lastErrorCode;

        // Shared authenticated transport keeps public leaderboards and Daily on one Keychain identity.
        internal IEnumerator Send<T>(string endpoint, string path, object args, Action<T> done,
            Action<string> failed, int timeoutSeconds = 20) where T : class
        {
            yield return Request(endpoint, path, args, true, done, error => failed?.Invoke(error.Replace("Daily", "Leaderboard")), true, timeoutSeconds, Time.realtimeSinceStartupAsDouble + timeoutSeconds);
        }
        public bool IsConfigured => !string.IsNullOrEmpty(deploymentUrl);
        public int PendingCount => cache.pending.FindAll(item => item.ready).Count;

        public DailyClient(DailyConnection config = null, string directory = null)
        {
            connection = config != null ? config : Resources.Load<DailyConnection>("DailyConnection");
            string candidate = connection == null ? "" : (connection.url ?? "").Trim().TrimEnd('/');
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/") deploymentUrl = candidate;
            string deploymentKey = DeploymentKey(deploymentUrl ?? "unconfigured");
            credentialKey = "ring-rush.convex." + deploymentKey;
            cachePath = Path.Combine(directory ?? Application.persistentDataPath, "ring-rush-daily-" + deploymentKey + ".json");
            LoadCache();
            try
            {
                string saved = NativeServices.ReadSecret(credentialKey);
                if (!string.IsNullOrEmpty(saved)) tokens = JsonUtility.FromJson<DailyTokenPair>(saved);
            }
            catch (Exception) { tokens = null; }
        }

        public IEnumerator LoadCurrent(Action<DailyChallenge> done, Action<string> failed = null)
        {
            yield return Request<DailyChallenge>("query", "daily:current", new EmptyArgs(), false, challenge =>
            {
                if (challenge != null)
                {
                    SyncClock(challenge.serverNow);
                    // Challenge content is immutable; a fresh DTO also refreshes ranked-enabled/server time.
                    cache.challenge = challenge;
                    Persist();
                }
                done?.Invoke(challenge);
            }, failed);
        }

        public IEnumerator StartRanked(DailyChallenge challenge, Action<DailyAttempt> done, Action<string> failed = null)
        {
            while (starting) yield return null;
            starting = true;
            try
            {
                if (challenge == null || !challenge.Supported)
                { failed?.Invoke("Update Ring Rush to play this Daily challenge."); yield break; }
                if (!challenge.rankedEnabled)
                { failed?.Invoke("Ranked Daily is temporarily unavailable. You can still practice."); yield break; }
                PruneExpired();
                // A new run abandons older unsubmitted runs; finished traces stay queued independently.
                cache.pending.RemoveAll(item => !item.ready);
                if (cache.pending.Count >= MaxPendingAttempts)
                { failed?.Invoke("Finish uploading your pending Daily results before starting another ranked run."); yield break; }
                if (cache.startingChallengeId != challenge.id || string.IsNullOrEmpty(cache.startingRequestId))
                {
                    cache.startingChallengeId = challenge.id;
                    cache.startingRequestId = Guid.NewGuid().ToString();
                    cache.startingAnalyticsEligible = DistributionAnalyticsPolicy.BuildEligible;
                }
                cache.startingAnalyticsEligible = cache.startingAnalyticsEligible && DistributionAnalyticsPolicy.BuildEligible;
                if (!Persist()) { failed?.Invoke("Daily could not save this attempt. Check available device storage."); yield break; }
                string requestId = cache.startingRequestId;
                DailyAttempt issued = null;
                yield return Request<DailyAttempt>("mutation", "daily:createAttempt",
                    new StartArgs { challengeId = challenge.id, requestId = requestId, analyticsEligible = cache.startingAnalyticsEligible }, true, value => issued = value, failed);
                if (issued == null)
                {
                    // An upgraded client must not keep retrying a pre-upgrade attempt ID.
                    if (lastErrorCode == "UPDATE_REQUIRED") ClearStartingRequest();
                    yield break;
                }
                if (issued.status != "open")
                { failed?.Invoke("This Daily attempt has already ended. Please start again."); ClearStartingRequest(); yield break; }
                var existing = cache.pending.Find(item => item.attempt.attemptId == issued.attemptId);
                if (existing == null) cache.pending.Add(new DailyPendingUpload { attempt = issued, requestId = requestId });
                cache.startingChallengeId = null;
                cache.startingRequestId = null;
                if (!Persist())
                { failed?.Invoke("Daily could not save this attempt. Check available device storage."); yield break; }
                done?.Invoke(issued);
            }
            finally { starting = false; }
        }

        public IEnumerator SubmitRun(DailyAttempt attempt, IReadOnlyList<DailyTraceEvent> trace,
            Action<DailyAttempt> done, Action<string> failed = null)
        {
            if (attempt == null || trace == null || trace.Count == 0 || trace.Count > MaxTraceEvents)
            { failed?.Invoke("This result cannot be submitted for ranking. Your local progress is saved."); yield break; }
            var item = cache.pending.Find(value => value.attempt.attemptId == attempt.attemptId
                && value.attempt.challengeId == attempt.challengeId);
            if (item == null)
            { failed?.Invoke("Only a server-issued ranked attempt can be submitted."); yield break; }
            if (!item.ready)
            {
                item.events = new List<DailyTraceEvent>(trace.Count);
                for (int i = 0; i < trace.Count; i++)
                {
                    var value = trace[i];
                    if (value == null) { failed?.Invoke("The Daily trace is incomplete."); yield break; }
                    // Snapshot mutable UI events before any coroutine yield or replay starts.
                    item.events.Add(new DailyTraceEvent { kind = value.kind, round = value.round, tMs = value.tMs,
                        elapsedMs = value.elapsedMs, color = value.color, xQ = value.xQ, yQ = value.yQ });
                }
                item.ready = true;
            }
            if (!Persist())
            { failed?.Invoke("Ranking could not be queued. Your local progress is saved."); yield break; }
            yield return Upload(item, done, failed);
        }

        public IEnumerator RetryPending(Action<int> done = null, Action<string> failed = null)
        {
            int completed = 0;
            var snapshot = cache.pending.ToArray();
            foreach (var item in snapshot)
            {
                if (!item.ready || !cache.pending.Contains(item)) continue;
                bool failedRequest = false;
                yield return Upload(item, value => { if (value != null && value.Terminal) completed++; },
                    error => { failedRequest = true; failed?.Invoke(error); });
                if (failedRequest) break; // Back off while offline; do not hammer the rest of the queue.
            }
            done?.Invoke(completed);
        }

        public IEnumerator GetStanding(string challengeId, Action<DailyStanding> done, Action<string> failed = null)
        {
            yield return Request<DailyStanding>("query", "daily:myStanding", new ChallengeArgs { challengeId = challengeId },
                true, done, failed);
        }

        public IEnumerator PollAttempt(string attemptId, Action<DailyAttempt> done, Action<string> failed = null)
        {
            DailyAttempt status = null;
            for (int i = 0; i < 20; i++)
            {
                bool requestFailed = false;
                yield return Request<DailyAttempt>("query", "daily:attemptStatus", new AttemptArgs { attemptId = attemptId }, true,
                    value => status = value, error => { requestFailed = true; failed?.Invoke(error); });
                if (requestFailed) yield break;
                if (status == null || status.Terminal || status.status == "open") break;
                yield return new WaitForSecondsRealtime(.75f);
            }
            done?.Invoke(status); // A validating result remains queued and can be retried later.
        }

        private IEnumerator Upload(DailyPendingUpload item, Action<DailyAttempt> done, Action<string> failed)
        {
            while (uploading) yield return null;
            uploading = true;
            try
            {
                DailyAttempt status = null;
                string statusError = null;
                yield return Request<DailyAttempt>("query", "daily:attemptStatus",
                    new AttemptArgs { attemptId = item.attempt.attemptId }, true, value => status = value, error => statusError = error);
                if (status == null)
                {
                    if (lastErrorCode == "NOT_FOUND")
                    {
                        item.attempt.status = "expired";
                        item.attempt.reason = "This Daily result is no longer available. Your local progress is saved.";
                        Remove(item);
                        done?.Invoke(item.attempt);
                    }
                    else failed?.Invoke(statusError ?? "Daily is temporarily unavailable.");
                    yield break;
                }
                item.attempt = status;
                if (status.Terminal) { Remove(item); done?.Invoke(status); yield break; }
                // Query first: a finalization response can be lost even though validation already began.
                if (status.status == "open" && hasServerClock && ServerNow() >= status.uploadDeadline)
                {
                    status.status = "expired";
                    status.reason = "The Daily upload window has closed. Your local progress is saved.";
                    Remove(item);
                    done?.Invoke(status);
                    yield break;
                }
                int chunks = (item.events.Count + ChunkSize - 1) / ChunkSize;
                if (status.status == "open")
                {
                    for (int index = status.nextChunkIndex; index < chunks; index++)
                    {
                        int offset = index * ChunkSize;
                        var events = item.events.GetRange(offset, Math.Min(ChunkSize, item.events.Count - offset)).ToArray();
                        DailyAttempt appended = null;
                        yield return Request<DailyAttempt>("mutation", "daily:appendChunk",
                            new ChunkArgs { attemptId = status.attemptId, index = index, events = events }, true,
                            value => appended = value, failed);
                        if (appended == null) yield break;
                        item.attempt = status = appended;
                        Persist();
                        if (status.Terminal) { Remove(item); done?.Invoke(status); yield break; }
                    }
                    DailyAttempt finalized = null;
                    yield return Request<DailyAttempt>("mutation", "daily:finalize",
                        new FinalizeArgs { attemptId = status.attemptId, chunkCount = chunks }, true,
                        value => finalized = value, failed);
                    if (finalized == null) yield break;
                    item.attempt = status = finalized;
                    Persist();
                }
                if (!status.Terminal)
                {
                    DailyAttempt polled = null;
                    yield return PollAttempt(status.attemptId, value => polled = value, failed);
                    if (polled == null) yield break;
                    item.attempt = status = polled;
                }
                if (status.Terminal) Remove(item); else Persist();
                done?.Invoke(status);
            }
            finally { uploading = false; }
        }

        private IEnumerator Request<T>(string endpoint, string path, object args, bool authenticated,
            Action<T> done, Action<string> failed, bool allowRefresh = true, int timeoutSeconds = 20, double deadline = -1) where T : class
        {
            lastErrorCode = null;
            if (!IsConfigured) { failed?.Invoke("Daily is not configured on this build. Offline modes are available."); yield break; }
            if (authenticated && (tokens == null || string.IsNullOrEmpty(tokens.token)))
            {
                bool signedIn = false;
                yield return Authenticate(false, () => signedIn = true, failed, timeoutSeconds, deadline);
                if (!signedIn) yield break;
            }
            string body = "{\"path\":\"" + path + "\",\"args\":" + JsonUtility.ToJson(args) + ",\"format\":\"json\"}";
            using (var request = new UnityWebRequest(deploymentUrl + "/api/" + endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated) request.SetRequestHeader("Authorization", "Bearer " + tokens.token);
                if (deadline > 0 && Time.realtimeSinceStartupAsDouble >= deadline)
                { failed?.Invoke("Connection timed out. Please try again."); yield break; }
                var operation = request.SendWebRequest();
                if (deadline > 0)
                {
                    while (!operation.isDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (!operation.isDone)
                    { request.Abort(); failed?.Invoke("Connection timed out. Please try again."); yield break; }
                }
                else yield return operation;
                string json = request.downloadHandler.text;
                DailyReply<T> reply = null;
                try { if (!string.IsNullOrEmpty(json)) reply = DeserializeReply<T>(json); }
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
                { /* An upstream outage or invalid wire value must not become an expired local attempt. */ }
                if (authenticated && allowRefresh && (request.responseCode == 401
                    || (reply?.errorData?.code == "UNAUTHENTICATED")
                    || (!string.IsNullOrEmpty(json) && json.Contains("UNAUTHENTICATED"))))
                {
                    bool refreshed = false;
                    yield return Authenticate(true, () => refreshed = true, failed, timeoutSeconds, deadline);
                    if (refreshed) yield return Request(endpoint, path, args, true, done, failed, false, timeoutSeconds, deadline);
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success || reply == null || reply.status != "success")
                {
                    lastErrorCode = reply?.errorData?.code;
                    failed?.Invoke(reply?.errorData?.message ?? (request.result == UnityWebRequest.Result.ConnectionError
                        ? "You’re offline. Your Daily result will retry when connected."
                        : "Daily is temporarily unavailable. Please try again."));
                    yield break;
                }
                if (reply.value is DailyStanding standing) standing.hasBestScore = !FieldIsNull(json, "bestScore");
                done?.Invoke(reply.value);
            }
        }

        private IEnumerator Authenticate(bool refresh, Action done, Action<string> failed, int timeoutSeconds = 20, double deadline = -1)
        {
            if (authenticating)
            {
                while (authenticating)
                {
                    if (deadline > 0 && Time.realtimeSinceStartupAsDouble >= deadline)
                    { failed?.Invoke("Connection timed out. Please try again."); yield break; }
                    yield return null;
                }
                if (lastAuthenticationSucceeded && tokens != null && !string.IsNullOrEmpty(tokens.token)) done?.Invoke();
                else failed?.Invoke("Daily sign-in could not finish. Please try again.");
                yield break;
            }
            authenticating = true;
            lastAuthenticationSucceeded = false;
            try
            {
                object args;
                if (tokens != null && !string.IsNullOrEmpty(tokens.refreshToken))
                    args = new RefreshArgs { refreshToken = tokens.refreshToken };
                else
                {
                    if (refresh) { failed?.Invoke("Your Daily guest session needs to be restored."); yield break; }
                    args = new AnonymousArgs { provider = "anonymous", @params = new AuthParams
                        { closedTestCode = connection == null ? "" : connection.closedTestCode } };
                }
                DailyAuthResult result = null;
                bool requestFailed = false;
                yield return Request<DailyAuthResult>("action", "auth:signIn", args, false, value => result = value,
                    error => { requestFailed = true; failed?.Invoke(error); }, true, timeoutSeconds, deadline);
                if (result?.tokens == null || string.IsNullOrEmpty(result.tokens.token))
                {
                    if (!requestFailed) failed?.Invoke("Daily sign-in did not return a guest session. Please try again.");
                    yield break;
                }
                if (!NativeServices.SaveSecret(credentialKey, JsonUtility.ToJson(result.tokens)))
                { failed?.Invoke("Daily could not securely save your guest session."); yield break; }
                tokens = result.tokens;
                lastAuthenticationSucceeded = true;
                done?.Invoke();
            }
            finally { authenticating = false; }
        }

        private void LoadCache()
        {
            try
            {
                if (!File.Exists(cachePath)) return;
                var loaded = JsonUtility.FromJson<DailyCache>(File.ReadAllText(cachePath));
                if (loaded == null || loaded.version != 1) return;
                cache = loaded;
                cache.pending = cache.pending ?? new List<DailyPendingUpload>();
                cache.pending.RemoveAll(item => item?.attempt == null || string.IsNullOrEmpty(item.attempt.attemptId)
                    || item.events == null || item.events.Count > MaxTraceEvents);
            }
            catch (Exception) { cache = new DailyCache(); }
        }

        private bool Persist()
        {
            string temporary = cachePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(temporary, JsonUtility.ToJson(cache));
                if (File.Exists(cachePath)) File.Replace(temporary, cachePath, null); else File.Move(temporary, cachePath);
                return true;
            }
            catch (Exception) { return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception) { } }
        }

        private void PruneExpired()
        {
            if (hasServerClock) cache.pending.RemoveAll(item => !item.ready && item.attempt.uploadDeadline < ServerNow()
                && item.attempt.status != "validating");
        }
        private void Remove(DailyPendingUpload item) { cache.pending.Remove(item); Persist(); }
        private void ClearStartingRequest() { cache.startingChallengeId = null; cache.startingRequestId = null; Persist(); }
        private void SyncClock(long now) { hasServerClock = true; serverTimeAtSync = now; realtimeAtSync = Time.realtimeSinceStartupAsDouble; }
        private long ServerNow() => serverTimeAtSync + (long)((Time.realtimeSinceStartupAsDouble - realtimeAtSync) * 1000);

        private static string DeploymentKey(string value)
        {
            uint hash = 2166136261;
            foreach (char character in value) hash = (hash ^ character) * 16777619;
            return hash.ToString("x8");
        }

        private static DailyReply<T> DeserializeReply<T>(string json) where T : class
        {
            var reply = JsonUtility.FromJson<DailyReply<T>>(json);
            // Some Unity serializers materialize a default nested object for a JSON null.
            if (reply != null && (typeof(T) == typeof(LeaderboardProfile) || typeof(T) == typeof(LeaderboardBoard))
                && FieldIsNull(json, "value")) reply.value = null;
            if (reply?.value is DailyChallenge challenge)
            {
                var wire = JsonUtility.FromJson<DailyReply<DailyChallengeNumbers>>(json).value;
                challenge.seed = WholeNumber(wire.seed, uint.MaxValue, "seed");
                challenge.opensAt = EpochMilliseconds(wire.opensAt);
                challenge.closesAt = EpochMilliseconds(wire.closesAt);
                challenge.uploadDeadline = EpochMilliseconds(wire.uploadDeadline);
                challenge.serverNow = EpochMilliseconds(wire.serverNow);
            }
            else if (reply?.value is DailyAttempt attempt)
            {
                var wire = JsonUtility.FromJson<DailyReply<DailyAttemptNumbers>>(json).value;
                attempt.uploadDeadline = EpochMilliseconds(wire.uploadDeadline);
            }
            if (reply?.value is LeaderboardTicket ticket)
            {
                LeaderboardWire.ValidateTicket(ticket);
                ticket.score = LeaderboardWire.Integer(JsonUtility.FromJson<DailyReply<LeaderboardTicketNumbers>>(json).value.score);
            }
            if (reply?.value is LeaderboardBest best)
            {
                best.score = LeaderboardWire.Integer(JsonUtility.FromJson<DailyReply<LeaderboardBestNumbers>>(json).value.score);
                if (best.score > 65536 || (best.status != "synced" && best.status != "excluded"))
                    throw new FormatException("Invalid high score receipt.");
            }
            if (reply?.value is LeaderboardBoard board)
            {
                var wire = JsonUtility.FromJson<DailyReply<LeaderboardBoardNumbers>>(json).value;
                if (FieldIsNull(json, "personal")) board.personal = null;
                board.participants = LeaderboardWire.Integer(wire.participants);
                for (int i = 0; i < (board.entries?.Length ?? 0); i++)
                {
                    board.entries[i].score = LeaderboardWire.Integer(wire.entries[i].score);
                    board.entries[i].rank = LeaderboardWire.Integer(wire.entries[i].rank);
                }
                if (board.personal != null && wire.personal != null)
                {
                    board.personal.score = LeaderboardWire.Integer(wire.personal.score);
                    board.personal.rank = LeaderboardWire.Integer(wire.personal.rank);
                }
            }
            return reply;
        }

        private static long EpochMilliseconds(double value)
        {
            long result = WholeNumber(value, 253402300799999L, "timestamp");
            if (result == 0) throw new FormatException("Daily timestamp is missing.");
            return result;
        }

        private static long WholeNumber(double value, long maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > maximum || value != Math.Truncate(value))
                throw new FormatException("Invalid Daily " + name + ".");
            return (long)value;
        }

        // JsonUtility maps JSON null numbers to zero. Preserve the distinction for a first personal best.
        private static bool FieldIsNull(string json, string field)
        {
            int index = json.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (index < 0) return true;
            index = json.IndexOf(':', index) + 1;
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            return index + 4 <= json.Length && string.CompareOrdinal(json, index, "null", 0, 4) == 0;
        }

        [Serializable] private sealed class EmptyArgs { }
        [Serializable] private sealed class ChallengeArgs { public string challengeId; }
        // Revision 3 supports the v2 revive protocol and the immutable v1 challenges.
        [Serializable] private sealed class StartArgs { public string challengeId; public string requestId; public int clientRulesRevision = 3; public bool analyticsEligible; }
        [Serializable] private sealed class AttemptArgs { public string attemptId; }
        [Serializable] private sealed class ChunkArgs { public string attemptId; public int index; public DailyTraceEvent[] events; }
        [Serializable] private sealed class FinalizeArgs { public string attemptId; public int chunkCount; }
        [Serializable] private sealed class RefreshArgs { public string refreshToken; }
        [Serializable] private sealed class AuthParams { public string closedTestCode; }
        [Serializable] private sealed class AnonymousArgs { public string provider; public AuthParams @params; }
    }
}

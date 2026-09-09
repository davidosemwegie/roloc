using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>Best-effort run history and optional usage capture through the shared installation identity.</summary>
    public sealed class GameTelemetryClient
    {
        private const int Capacity = 256;
        private const double RetentionMs = 7d * 24 * 60 * 60 * 1000;
        private readonly DailyClient shared;
        private readonly string cachePath;
        private GameTelemetryCache cache = new GameTelemetryCache();
        private bool flushing;
        public bool AnalyticsEnabled => cache.analyticsEnabled;
        public string PlayerId => cache.playerId;
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public int PendingCount => cache.pending.Count;
        public string LastDiagnostic { get; private set; }
        public event Action<string> Diagnostic;
        private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public GameTelemetryClient(DailyClient shared, string directory = null)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            cachePath = Path.Combine(directory ?? Application.persistentDataPath, "ring-rush-telemetry-" + shared.CacheNamespace + ".json");
            try
            {
                if (File.Exists(cachePath))
                {
                    var loaded = JsonUtility.FromJson<GameTelemetryCache>(File.ReadAllText(cachePath));
                    if (loaded != null && loaded.version == 1) cache = loaded;
                }
                cache.pending = cache.pending ?? new List<GameTelemetryItem>();
                foreach (var item in cache.pending)
                {
                    if (item == null) continue;
                    if (item.kind == "run") item.usage = null;
                    if (item.kind == "event") item.run = null;
                }
                if (!cache.hasActive) cache.active = null;
                Prune();
                if (cache.active != null)
                {
                    var abandoned = Copy(cache.active);
                    abandoned.status = "abandoned";
                    abandoned.endedAt = Math.Max(abandoned.startedAt, Now);
                    // Last observed counters only. A killed process cannot establish a final gameplay score.
                    Enqueue(new GameTelemetryItem { kind = "run", run = abandoned });
                    cache.active = null; cache.hasActive = false;
                }
                Persist();
            }
            catch (Exception) { cache = new GameTelemetryCache(); Report("Telemetry cache could not be restored."); }
        }

        public IEnumerator Initialize(Action done = null, Action<string> failed = null) => Flush(done, failed);

        // Apply locally before returning the coroutine so UI and capture honor opt-out immediately.
        public IEnumerator SetAnalyticsEnabled(bool enabled, Action done = null, Action<string> failed = null)
        {
            cache.analyticsEnabled = enabled;
            cache.preferencePending = true;
            cache.preferenceRevision++;
            if (!enabled)
            {
                cache.pending.RemoveAll(item => item.usage != null);
                foreach (var item in cache.pending) if (item.run != null) item.run.analyticsEnabled = false;
                if (cache.active != null) cache.active.analyticsEnabled = false;
            }
            Persist();
            return Flush(done, failed);
        }

        public void StartRun(string clientRunId, string mode, string leaderboardRunId = "", string dailyAttemptId = "")
        {
            if (!ValidRunId(clientRunId) || !ValidMode(mode)) { Report("Invalid run history start was discarded."); return; }
            if (cache.active?.clientRunId == clientRunId) return;
            if (cache.active != null) Finish(cache.active.clientRunId, cache.active.score, cache.active.revives, cache.active.elapsedMs, "abandoned");
            cache.active = new GameRunSummary { clientRunId = clientRunId, mode = mode, status = "started", startedAt = Now, analyticsEnabled = AnalyticsEnabled,
                leaderboardRunId = leaderboardRunId ?? "", dailyAttemptId = dailyAttemptId ?? "" };
            cache.hasActive = true;
            Enqueue(new GameTelemetryItem { kind = "run", run = Copy(cache.active) });
            Persist();
        }
        // Save only on lifecycle boundaries (for example backgrounding), never every frame.
        public void UpdateRun(string clientRunId, int score, int revives, double elapsedMs)
        {
            if (cache.active?.clientRunId != clientRunId || !ValidCounters(score, revives, elapsedMs)) return;
            cache.active.score = score; cache.active.revives = revives; cache.active.elapsedMs = Math.Floor(elapsedMs);
            Persist();
        }
        public void CompleteRun(string clientRunId, int score, int revives, double elapsedMs) => Finish(clientRunId, score, revives, elapsedMs, "completed");
        public void AbandonRun(string clientRunId, int score, int revives, double elapsedMs) => Finish(clientRunId, score, revives, elapsedMs, "abandoned");
        private void Finish(string clientRunId, int score, int revives, double elapsedMs, string status)
        {
            if (cache.active == null || cache.active.clientRunId != clientRunId) return;
            if (!ValidCounters(score, revives, elapsedMs))
            { Report("Invalid run history counters were discarded."); return; }
            var final = Copy(cache.active);
            final.analyticsEnabled = final.analyticsEnabled && AnalyticsEnabled;
            final.status = status; final.score = score; final.revives = revives; final.elapsedMs = Math.Floor(elapsedMs);
            final.endedAt = Math.Max(final.startedAt, Now);
            Enqueue(new GameTelemetryItem { kind = "run", run = final });
            cache.active = null; cache.hasActive = false;
            Persist();
        }
        public void Capture(string name, string mode = "", string clientRunId = "")
        {
            if (!AnalyticsEnabled) return;
            if (!ValidEvent(name) || (mode != "" && !ValidMode(mode)) || (clientRunId != "" && !ValidRunId(clientRunId)))
            { Report("Invalid usage event was discarded."); return; }
            Enqueue(new GameTelemetryItem { kind = "event", usage = new GameUsageEvent { eventId = Guid.NewGuid().ToString("N"), name = name,
                sessionId = SessionId, occurredAt = Now, mode = mode, clientRunId = clientRunId } });
            Persist();
        }

        public IEnumerator Flush(Action done = null, Action<string> failed = null)
        {
            while (flushing) yield return null;
            flushing = true;
            try
            {
                Prune();
                Persist();
                bool identityReady = false;
                yield return EnsureIdentity(() => identityReady = true, failed);
                if (!identityReady) yield break;
                while (cache.pending.Count > 0)
                {
                    // A choice made while a request was in flight takes priority over the next event.
                    if (cache.preferencePending)
                    {
                        identityReady = false;
                        yield return EnsureIdentity(() => identityReady = true, failed);
                        if (!identityReady) yield break;
                    }
                    var item = cache.pending[0];
                    if (item.usage != null && !AnalyticsEnabled) { cache.pending.Remove(item); Persist(); continue; }
                    bool success = false;
                    string error = null, code = null;
                    Action<string> onFailure = value => { error = value; code = shared.LastErrorCode; };
                    if (item.run != null)
                        yield return shared.Send<RecordedReply>("mutation", "telemetry:recordRuns",
                            new RunsArgs { runs = new[] { item.run } }, _ => success = true, onFailure);
                    else
                        yield return shared.Send<EmptyArgs>("mutation", "telemetry:capture", item.usage, _ => success = true, onFailure);
                    if (success) { cache.pending.Remove(item); Persist(); continue; }
                    if (Permanent(code))
                    { cache.pending.Remove(item); Persist(); Report("A rejected telemetry item was discarded (" + code + ")."); continue; }
                    failed?.Invoke(error ?? "Run history will retry when connected.");
                    yield break;
                }
                done?.Invoke();
            }
            finally { flushing = false; }
        }
        private IEnumerator EnsureIdentity(Action done, Action<string> failed)
        {
            while (cache.preferencePending)
            {
                int revision = cache.preferenceRevision;
                GameTelemetryIdentity identity = null;
                yield return shared.Send<GameTelemetryIdentity>("mutation", "telemetry:setAnalyticsEnabled",
                    new PreferenceArgs { enabled = cache.analyticsEnabled }, value => identity = value, failed);
                if (identity == null) yield break;
                cache.playerId = identity.playerId;
                if (cache.preferenceRevision == revision) { cache.preferencePending = false; cache.analyticsEnabled = identity.analyticsEnabled; }
                Persist();
            }
            if (string.IsNullOrEmpty(cache.playerId))
            {
                int revision = cache.preferenceRevision;
                GameTelemetryIdentity identity = null;
                yield return shared.Send<GameTelemetryIdentity>("query", "telemetry:identity", new EmptyArgs(), value => identity = value, failed);
                if (identity == null) yield break;
                cache.playerId = identity.playerId;
                if (!cache.preferencePending && cache.preferenceRevision == revision) cache.analyticsEnabled = identity.analyticsEnabled;
                Persist();
                if (cache.preferencePending) { yield return EnsureIdentity(done, failed); yield break; }
            }
            done?.Invoke();
        }
        private void Enqueue(GameTelemetryItem item)
        {
            Prune();
            while (cache.pending.Count >= Capacity)
            {
                int optional = cache.pending.FindIndex(value => value.usage != null);
                cache.pending.RemoveAt(optional >= 0 ? optional : 0);
                Report("The oldest pending telemetry item was discarded because the offline queue is full.");
            }
            cache.pending.Add(item);
        }
        private void Prune()
        {
            double cutoff = Now - RetentionMs;
            cache.pending.RemoveAll(item => item == null || (item.kind != "run" && item.kind != "event") || (item.run == null && item.usage == null)
                || (item.run != null ? item.run.startedAt : item.usage.occurredAt) < cutoff);
            while (cache.pending.Count > Capacity) cache.pending.RemoveAt(0);
            if (cache.active != null && cache.active.startedAt < cutoff) { cache.active = null; cache.hasActive = false; }
        }
        private static bool ValidCounters(int score, int revives, double elapsedMs) => score >= 0 && score <= 65536
            && revives >= 0 && revives <= 1312 && !double.IsNaN(elapsedMs) && !double.IsInfinity(elapsedMs)
            && elapsedMs >= 0 && elapsedMs <= 90000000;
        private static bool Permanent(string code) => code == "INVALID_ARGUMENT" || code == "INVALID_INPUT"
            || code == "BAD_REQUEST" || code == "NOT_FOUND" || code == "CONFLICT" || code == "EXPIRED" || code == "INVALID_RUN" || code == "INVALID_EVENT";
        private static bool ValidMode(string mode) => mode == "flow" || mode == "rush" || mode == "daily";
        private static bool ValidEvent(string name) => name == "game_opened" || name == "tutorial_started" || name == "tutorial_completed"
            || name == "leaderboard_viewed" || name == "revive_offered" || name == "revive_requested" || name == "revive_completed";
        private static bool ValidRunId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 8 || id.Length > 128) return false;
            foreach (char c in id) if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == ':')) return false;
            return true;
        }
        private static GameRunSummary Copy(GameRunSummary run) => JsonUtility.FromJson<GameRunSummary>(JsonUtility.ToJson(run));
        private void Report(string message) { LastDiagnostic = message; Diagnostic?.Invoke(message); }
        private void Persist()
        {
            string temporary = cachePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(temporary, JsonUtility.ToJson(cache));
                if (File.Exists(cachePath)) File.Replace(temporary, cachePath, null); else File.Move(temporary, cachePath);
            }
            catch (Exception) { Report("Run history could not be saved on this device."); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception) { } }
        }
        [Serializable] private sealed class EmptyArgs { }
        [Serializable] private sealed class RunsArgs { public GameRunSummary[] runs; }
        [Serializable] private sealed class RecordedReply { public double recorded, duplicates; }
        [Serializable] private sealed class PreferenceArgs { public bool enabled; }
    }
}

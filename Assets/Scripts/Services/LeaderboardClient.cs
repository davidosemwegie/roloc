using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>Installation-scoped rankings. Daily runs use tickets; all-time scores retry from the local save.</summary>
    public sealed class LeaderboardClient
    {
        private const int MaxPending = 32;
        private readonly DailyClient shared;
        private readonly string cachePath;
        private LeaderboardCache cache = new LeaderboardCache();
        private bool starting, uploading, syncingBest;
        public string BestSyncMessage { get; private set; }
        public LeaderboardProfile CachedProfile => cache.profile;
        public bool IsConfigured => shared.IsConfigured;
        public bool IsParticipating => cache.profile != null && cache.profile.participating;
        public int PendingCount => cache.pending.FindAll(item => item.ready).Count;
        public event Action<LeaderboardTicket> ResultChanged;

        public LeaderboardClient(DailyClient shared, string directory = null)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            cachePath = Path.Combine(directory ?? Application.persistentDataPath,
                "ring-rush-leaderboard-" + shared.CacheNamespace + ".json");
            try
            {
                if (File.Exists(cachePath))
                {
                    var loaded = JsonUtility.FromJson<LeaderboardCache>(File.ReadAllText(cachePath));
                    if (loaded != null && loaded.version == 1) cache = loaded;
                }
                cache.pending = cache.pending ?? new List<LeaderboardPending>();
                cache.pending.RemoveAll(item => item?.ticket == null || !item.ready || string.IsNullOrEmpty(item.ticket.runId));
                if (cache.pending.Count > MaxPending) cache.pending.RemoveRange(MaxPending, cache.pending.Count - MaxPending);
                // A crashed/incomplete run never resumes as another ranked run.
                cache.startingRequestId = null;
            }
            catch (Exception) { cache = new LeaderboardCache(); }
        }

        public IEnumerator LoadProfile(Action<LeaderboardProfile> done, Action<string> failed = null)
        {
            yield return shared.Send<LeaderboardProfile>("query", "leaderboard:profile", new EmptyArgs(), value =>
            { cache.profile = value; Persist(); done?.Invoke(value); }, failed);
        }
        public IEnumerator SetProfile(string nickname, bool participating, Action<LeaderboardProfile> done, Action<string> failed = null)
        {
            yield return shared.Send<LeaderboardProfile>("mutation", "leaderboard:setProfile",
                new ProfileArgs { nickname = nickname, participating = participating, analyticsEligible = DistributionAnalyticsPolicy.BuildEligible }, value =>
                { cache.profile = value; Persist(); done?.Invoke(value); }, failed);
        }
        public IEnumerator GetBoard(string mode, string day, Action<LeaderboardBoard> done, Action<string> failed = null)
        {
            yield return shared.Send("query", "leaderboard:board", new BoardArgs { mode = mode, day = day }, done, failed);
        }
        public IEnumerator GetAllTimeBoard(int localBest, Action<LeaderboardBoard> done, Action<string> failed = null)
        {
            // Refresh opt-in before publishing a historical score, including after a cache loss or nickname reset.
            bool loaded = false;
            yield return LoadProfile(_ => loaded = true, failed);
            if (!loaded) yield break;
            yield return SyncBest(localBest);
            // A paused or rejected upload must not prevent reading the existing board.
            yield return shared.Send("query", "leaderboard:allTimeBoard", new EmptyArgs(), done, failed);
        }
        public IEnumerator SyncBest(int score, Action<LeaderboardBest> done = null, Action<string> failed = null)
        {
            while (syncingBest) yield return null;
            BestSyncMessage = null;
            if (!IsParticipating || score == 0) { done?.Invoke(null); yield break; }
            if (score < 0 || score > 65536)
            {
                BestSyncMessage = "This saved score cannot be ranked.";
                failed?.Invoke(BestSyncMessage); yield break;
            }
            syncingBest = true;
            try
            {
                yield return shared.Send<LeaderboardBest>("mutation", "leaderboard:syncBest", new BestArgs { score = score }, value => {
                    BestSyncMessage = value.status == "excluded" ? "Your high score is not eligible for ranking." : null;
                    done?.Invoke(value);
                }, error => {
                    BestSyncMessage = "Saved high score will sync when available.";
                    failed?.Invoke(error);
                });
            }
            finally { syncingBest = false; }
        }
        public IEnumerator StartRun(string mode, Action<LeaderboardTicket> done, Action<string> failed = null)
        {
            if (starting) { failed?.Invoke("A leaderboard run is already starting."); yield break; }
            if (!IsParticipating) { failed?.Invoke("Join the leaderboards to rank this run."); yield break; }
            if (mode != "flow" && mode != "rush") { failed?.Invoke("This mode cannot be ranked."); yield break; }
            starting = true;
            try
            {
                cache.pending.RemoveAll(item => !item.ready);
                if (cache.pending.Count >= MaxPending)
                { failed?.Invoke("Upload pending results before ranking another run. This run is unranked."); yield break; }
                cache.startingRequestId = Guid.NewGuid().ToString("N");
                if (!Persist()) { failed?.Invoke("Could not save the ranking ticket. This run is unranked."); yield break; }
                LeaderboardTicket ticket = null;
                yield return shared.Send<LeaderboardTicket>("mutation", "leaderboard:start",
                    new StartArgs { mode = mode, requestId = cache.startingRequestId, analyticsEligible = DistributionAnalyticsPolicy.BuildEligible }, value => ticket = value, failed, 3);
                if (ticket == null) yield break;
                if (ticket.status != "open") { failed?.Invoke("This ranking ticket has ended. This run is unranked."); yield break; }
                cache.pending.Add(new LeaderboardPending { ticket = ticket });
                if (!Persist())
                { cache.pending.RemoveAll(item => item.ticket.runId == ticket.runId); failed?.Invoke("Could not save the ranking ticket. This run is unranked."); yield break; }
                done?.Invoke(ticket);
            }
            finally
            {
                // Never reuse an uncertain server response for the next local run.
                cache.startingRequestId = null;
                Persist();
                starting = false;
            }
        }
        public IEnumerator SubmitRun(LeaderboardTicket ticket, int score, int revives, double elapsedMs,
            Action<LeaderboardTicket> done, Action<string> failed = null)
        {
            var item = cache.pending.Find(value => value.ticket.runId == ticket?.runId);
            if (item == null) { failed?.Invoke("Only a server-issued run can be submitted. Your local progress is saved."); yield break; }
            if (!item.ready)
            {
                if (score < 0 || score > 65536 || revives < 0 || revives > 1312 || double.IsNaN(elapsedMs)
                    || double.IsInfinity(elapsedMs) || elapsedMs < 0 || elapsedMs > 90000000)
                { failed?.Invoke("This result cannot be ranked. Your local progress is saved."); yield break; }
                item.score = score; item.revives = revives; item.elapsedMs = Math.Floor(elapsedMs); item.ready = true;
            }
            if (!Persist()) { failed?.Invoke("Ranking could not be queued. Your local progress is saved."); yield break; }
            yield return Upload(item, done, failed);
        }
        public IEnumerator RetryPending(Action<int> done = null, Action<string> failed = null)
        {
            int completed = 0;
            foreach (var item in cache.pending.ToArray())
            {
                if (!item.ready || !cache.pending.Contains(item)) continue;
                bool requestFailed = false;
                yield return Upload(item, value => { if (value.Terminal) completed++; },
                    error => { requestFailed = true; failed?.Invoke(error); });
                if (requestFailed) break;
            }
            done?.Invoke(completed);
        }
        private IEnumerator Upload(LeaderboardPending item, Action<LeaderboardTicket> done, Action<string> failed)
        {
            while (uploading) yield return null;
            if (!cache.pending.Contains(item)) { done?.Invoke(item.ticket); yield break; }
            uploading = true;
            try
            {
                LeaderboardTicket status = null;
                string error = null;
                string errorCode = null;
                // Ask the server even after the local deadline: the prior success response may have been lost.
                yield return shared.Send<LeaderboardTicket>("query", "leaderboard:status", new RunArgs { runId = item.ticket.runId },
                    value => status = value, value => { error = value; errorCode = shared.LastErrorCode; });
                if (status == null)
                {
                    if (errorCode == "NOT_FOUND")
                    { status = item.ticket; status.status = "expired"; status.reason = "This result has expired. Your local progress is saved."; }
                    else { failed?.Invoke(error ?? "Leaderboard unavailable. Your result remains queued."); yield break; }
                }
                if (!status.Terminal)
                {
                    LeaderboardTicket submitted = null;
                    yield return shared.Send<LeaderboardTicket>("mutation", "leaderboard:submit",
                        new SubmitArgs { runId = status.runId, score = item.score, revives = item.revives, elapsedMs = item.elapsedMs },
                        value => submitted = value, failed);
                    if (submitted == null) yield break;
                    status = submitted;
                }
                item.ticket = status;
                if (status.Terminal) cache.pending.Remove(item);
                Persist();
                if (status.Terminal) ResultChanged?.Invoke(status);
                done?.Invoke(status);
            }
            finally { uploading = false; }
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
        [Serializable] private sealed class EmptyArgs { }
        [Serializable] private sealed class ProfileArgs { public string nickname; public bool participating, analyticsEligible; }
        [Serializable] private sealed class BoardArgs { public string mode, day; }
        [Serializable] private sealed class BestArgs { public int score; }
        [Serializable] private sealed class StartArgs { public string mode, requestId; public int clientRulesRevision = 1; public bool analyticsEligible; }
        [Serializable] private sealed class RunArgs { public string runId; }
        [Serializable] private sealed class SubmitArgs { public string runId; public int score, revives; public double elapsedMs; }
    }
}

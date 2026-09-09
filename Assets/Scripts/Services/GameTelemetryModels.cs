using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    [Serializable] public sealed class GameTelemetryIdentity { public string playerId; public bool analyticsEnabled = true; }
    [Serializable] public sealed class GameRunSummary
    {
        public string clientRunId, mode, status, leaderboardRunId = "", dailyAttemptId = "";
        public double startedAt, endedAt, elapsedMs;
        public bool analyticsEnabled;
        public int score, revives;
    }
    [Serializable] internal sealed class GameUsageEvent
    {
        public string eventId, name, sessionId, mode, clientRunId;
        public double occurredAt;
    }
    [Serializable] internal sealed class GameTelemetryItem
    {
        public string kind;
        public GameRunSummary run;
        public GameUsageEvent usage;
    }
    [Serializable] internal sealed class GameTelemetryCache
    {
        public int version = 1;
        public string playerId;
        public bool analyticsEnabled = true, preferencePending;
        public int preferenceRevision;
        public bool hasActive;
        public GameRunSummary active;
        public List<GameTelemetryItem> pending = new List<GameTelemetryItem>();
    }
}

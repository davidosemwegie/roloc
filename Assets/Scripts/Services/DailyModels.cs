using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    [Serializable]
    public sealed class DailyChallenge
    {
        public string id;
        public string date;
        public long seed;
        public int rulesVersion;
        public string variant;
        public long opensAt;
        public long closesAt;
        public long uploadDeadline;
        public long serverNow;
        public bool rankedEnabled;
        public bool publicCompetitionEnabled;
        public bool Supported => (rulesVersion == 1 || rulesVersion == 2) && (variant == "lively" || variant == "still");
    }

    [Serializable]
    public sealed class DailyAttempt
    {
        public string attemptId;
        public string challengeId;
        public string status;
        public int nextChunkIndex;
        public int score;
        public string reason;
        public long uploadDeadline;
        public bool Terminal => status == "accepted" || status == "rejected" || status == "expired";
    }

    [Serializable]
    public sealed class DailyStanding
    {
        public string challengeId;
        public string date;
        public string variant;
        public int bestScore;
        public int participants;
        public double percentile;
        public double topPercent;
        public bool provisional;
        public bool early;
        public bool waiting;
        public bool excluded;
        [NonSerialized] public bool hasBestScore;
    }

    [Serializable]
    public sealed class DailyTraceEvent
    {
        public string kind;
        public int round;
        public int tMs;
        public int elapsedMs;
        public int color = -1;
        public int xQ;
        public int yQ;
    }

    [Serializable] internal sealed class DailyTokenPair { public string token; public string refreshToken; }
    [Serializable] internal sealed class DailyAuthResult { public DailyTokenPair tokens; }
    [Serializable] internal sealed class DailyError { public string code; public string message; }
    [Serializable] internal sealed class DailyReply<T> { public string status; public T value; public string errorMessage; public DailyError errorData; }
    // Convex's HTTP encoder writes ordinary numbers as floating-point literals (for example
    // 1788829200000.0). JsonUtility can zero a long field when its JSON token is floating point.
    // Read those wire fields as doubles, then validate and convert before exposing domain DTOs.
    [Serializable] internal sealed class DailyChallengeNumbers
    {
        public double seed;
        public double opensAt;
        public double closesAt;
        public double uploadDeadline;
        public double serverNow;
    }
    [Serializable] internal sealed class DailyAttemptNumbers { public double uploadDeadline; }
    [Serializable] internal sealed class DailyCache
    {
        public int version = 1;
        public DailyChallenge challenge;
        public string startingChallengeId;
        public string startingRequestId;
        public List<DailyPendingUpload> pending = new List<DailyPendingUpload>();
    }
    [Serializable] internal sealed class DailyPendingUpload
    {
        public DailyAttempt attempt;
        public string requestId;
        public bool ready;
        public List<DailyTraceEvent> events = new List<DailyTraceEvent>();
    }
}

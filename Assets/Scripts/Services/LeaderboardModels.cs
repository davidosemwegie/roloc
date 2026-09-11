using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    [Serializable] public sealed class LeaderboardProfile { public string nickname; public bool participating; }
    [Serializable] public sealed class LeaderboardTicket
    {
        public string runId, mode, date, status, reason;
        public double startedAt, uploadDeadline;
        public int score;
        public bool Terminal => status == "accepted" || status == "rejected" || status == "expired";
    }
    [Serializable] public sealed class LeaderboardEntry { public string nickname; public int score, rank; public bool isMe; }
    [Serializable] public sealed class LeaderboardBest { public int score; public string status; }
    [Serializable] public sealed class LeaderboardBoard
    {
        public string date, mode;
        public int participants;
        public bool provisional, enabled;
        public LeaderboardEntry[] entries;
        public LeaderboardEntry personal;
    }
    [Serializable] internal sealed class LeaderboardCache
    {
        public int version = 1;
        public LeaderboardProfile profile;
        public string startingRequestId;
        public List<LeaderboardPending> pending = new List<LeaderboardPending>();
    }
    [Serializable] internal sealed class LeaderboardPending
    {
        public LeaderboardTicket ticket;
        public bool ready;
        public int score, revives;
        public double elapsedMs;
    }
    [Serializable] internal sealed class LeaderboardTicketNumbers { public double score; }
    [Serializable] internal sealed class LeaderboardBestNumbers { public double score; }
    [Serializable] internal sealed class LeaderboardEntryNumbers { public double score, rank; }
    [Serializable] internal sealed class LeaderboardBoardNumbers
    {
        public double participants;
        public LeaderboardEntryNumbers[] entries;
        public LeaderboardEntryNumbers personal;
    }
    internal static class LeaderboardWire
    {
        internal static int Integer(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > int.MaxValue || value != Math.Truncate(value))
                throw new FormatException("Invalid leaderboard number.");
            return (int)value;
        }
        internal static void ValidateTicket(LeaderboardTicket ticket)
        {
            if (string.IsNullOrEmpty(ticket.runId) || (ticket.mode != "flow" && ticket.mode != "rush")
                || (ticket.status != "open" && !ticket.Terminal)
                || !Epoch(ticket.startedAt) || !Epoch(ticket.uploadDeadline) || ticket.uploadDeadline <= ticket.startedAt)
                throw new FormatException("Invalid leaderboard ticket.");
        }
        private static bool Epoch(double value) => !double.IsNaN(value) && !double.IsInfinity(value)
            && value > 0 && value <= 253402300799999d && value == Math.Truncate(value);
    }
}

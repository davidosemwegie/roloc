using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    public enum BadgeTrack { Run, Lifetime }

    public sealed class BadgeDefinition
    {
        public readonly int Matches;
        public readonly string Name;
        public readonly BadgeTrack Track;
        public string Id => (Track == BadgeTrack.Run ? "run-" : "lifetime-") + Matches;
        public string ShortValue => Matches >= 1000 ? (Matches / 1000) + "k" : Matches.ToString();
        public BadgeDefinition(int matches, string name, BadgeTrack track = BadgeTrack.Run)
        { Matches = matches; Name = name; Track = track; }
    }

    public static class BadgeCatalog
    {
        public static readonly BadgeDefinition[] All = {
            new BadgeDefinition(20, "Warm Up"), new BadgeDefinition(50, "On a Roll"),
            new BadgeDefinition(100, "Century"), new BadgeDefinition(200, "Double Up"),
            new BadgeDefinition(300, "Hat Trick"), new BadgeDefinition(400, "Full Circle"),
            new BadgeDefinition(500, "High Five"), new BadgeDefinition(600, "Full Tilt"),
            new BadgeDefinition(700, "In Orbit"), new BadgeDefinition(800, "Afterburn"),
            new BadgeDefinition(900, "Rush Royalty"), new BadgeDefinition(1000, "Ring Legend"),
            new BadgeDefinition(100, "First Hundred", BadgeTrack.Lifetime),
            new BadgeDefinition(500, "Regular", BadgeTrack.Lifetime),
            new BadgeDefinition(1000, "One Thousand", BadgeTrack.Lifetime),
            new BadgeDefinition(2000, "Double Thousand", BadgeTrack.Lifetime),
            new BadgeDefinition(5000, "Dedicated", BadgeTrack.Lifetime),
            new BadgeDefinition(10000, "Ten Thousand", BadgeTrack.Lifetime),
            new BadgeDefinition(25000, "Mainstay", BadgeTrack.Lifetime),
            new BadgeDefinition(50000, "Veteran", BadgeTrack.Lifetime),
            new BadgeDefinition(100000, "Ring Rush Forever", BadgeTrack.Lifetime) };

        public static BadgeDefinition Find(string id) => Array.Find(All, badge => badge.Id == id);

        public static void Initialize(PlayerProgress data)
        {
            data.UnlockedBadges = Clean(data.UnlockedBadges);
            if (data.ActiveRun != null) data.ActiveRun.NewBadges = Clean(data.ActiveRun.NewBadges);
            if (data.BadgesInitialized) return;
            // Backfill historical progress quietly; these weren't earned in the current run.
            foreach (var badge in All)
                if (HistoricalProgress(data, badge.Track) >= badge.Matches && !data.UnlockedBadges.Contains(badge.Id)) data.UnlockedBadges.Add(badge.Id);
            data.BadgesInitialized = true;
        }

        public static void Credit(PlayerProgress data)
        {
            Initialize(data);
            foreach (var badge in All)
            {
                long progress = badge.Track == BadgeTrack.Lifetime ? data.TotalScore : data.ActiveRun?.MatchIndex ?? 0;
                if (progress < badge.Matches || data.UnlockedBadges.Contains(badge.Id)) continue;
                data.UnlockedBadges.Add(badge.Id);
                data.ActiveRun?.NewBadges.Add(badge.Id);
            }
        }

        static long HistoricalProgress(PlayerProgress data, BadgeTrack track)
        {
            if (track == BadgeTrack.Lifetime) return data.TotalScore;
            int best = Math.Max(data.HighScore, data.ActiveRun?.MatchIndex ?? 0);
            if (data.Records != null) foreach (var record in data.Records)
                if (record != null) best = Math.Max(best, record.HighScore);
            return best;
        }

        static List<string> Clean(List<string> ids)
        {
            var result = new List<string>();
            if (ids != null) foreach (var id in ids)
                if (Find(id) != null && !result.Contains(id)) result.Add(id);
            return result;
        }
    }
}

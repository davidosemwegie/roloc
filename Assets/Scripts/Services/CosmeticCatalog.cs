using System;
using System.Collections.Generic;

namespace Roloc.Services
{
    public enum CosmeticCategory { Puck, Trail, Ring, Background }

    public sealed class CosmeticDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CosmeticCategory Category { get; }
        public long UnlockAt { get; }

        public CosmeticDefinition(string id, string name, CosmeticCategory category, long unlockAt)
        { Id = id; Name = name; Category = category; UnlockAt = unlockAt; }
    }

    /// <summary>Pure presentation-independent collection data. IDs are persisted in saves.</summary>
    public static class CosmeticCatalog
    {
        private static readonly CosmeticDefinition[] unlocks =
        {
            new CosmeticDefinition("glass", "Glass", CosmeticCategory.Puck, 40),
            new CosmeticDefinition("ribbon", "Ribbon", CosmeticCategory.Trail, 100),
            new CosmeticDefinition("porcelain", "Porcelain", CosmeticCategory.Ring, 180),
            new CosmeticDefinition("dusk", "Dusk", CosmeticCategory.Background, 280),
            new CosmeticDefinition("pearl", "Pearl", CosmeticCategory.Puck, 400),
            new CosmeticDefinition("orbit", "Orbit", CosmeticCategory.Ring, 550)
        };

        public static IReadOnlyList<CosmeticDefinition> Unlocks { get; } = Array.AsReadOnly(unlocks);

        public static CosmeticDefinition Find(CosmeticCategory category, string id)
        {
            if (id == "classic")
                return new CosmeticDefinition("classic", category == CosmeticCategory.Trail ? "None" : "Classic", category, 0);
            foreach (var cosmetic in unlocks)
                if (cosmetic.Category == category && cosmetic.Id == id) return cosmetic;
            return null;
        }

        public static CosmeticDefinition NextUnlock(long points)
        {
            foreach (var cosmetic in unlocks)
                if (points < cosmetic.UnlockAt) return cosmetic;
            return null;
        }

        public static bool IsUnlocked(CosmeticCategory category, string id, long points)
        {
            var cosmetic = Find(category, id);
            return cosmetic != null && points >= cosmetic.UnlockAt;
        }
    }

    public sealed class ProgressSnapshot
    {
        public long Points { get; internal set; }
        public CosmeticDefinition NextUnlock { get; internal set; }
        public long PointsToNextUnlock { get; internal set; }
        public bool AllUnlocked => NextUnlock == null;
        public long TotalMatches { get; internal set; }
        public long NextMatchMilestone { get; internal set; }
        public long MatchesToMilestone => Math.Max(0, NextMatchMilestone - TotalMatches);
    }
}

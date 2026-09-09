import { v } from "convex/values";
export const historyMode = v.union(v.literal("flow"), v.literal("rush"), v.literal("daily"));
export const historyStatus = v.union(v.literal("started"), v.literal("completed"), v.literal("abandoned"));
export const captureName = v.union(v.literal("game_opened"), v.literal("tutorial_started"), v.literal("tutorial_completed"), v.literal("leaderboard_viewed"), v.literal("revive_offered"), v.literal("revive_requested"), v.literal("revive_completed"));
export const analyticsProperties = v.object({
  mode: v.optional(v.string()), clientRunId: v.optional(v.string()), sessionId: v.optional(v.string()),
  runId: v.optional(v.string()), score: v.optional(v.number()), revives: v.optional(v.number()), elapsedMs: v.optional(v.number()),
  status: v.optional(v.string()), participating: v.optional(v.boolean()), source: v.optional(v.string()),
});
export const historyInput = v.object({ analyticsEligible: v.optional(v.boolean()), analyticsEnabled: v.optional(v.boolean()), clientRunId: v.string(), mode: historyMode, startedAt: v.number(), endedAt: v.number(), score: v.number(), revives: v.number(), elapsedMs: v.number(), status: historyStatus, leaderboardRunId: v.string(), dailyAttemptId: v.string() });
export const identityValue = v.object({ playerId: v.string(), analyticsEnabled: v.boolean() });
export const claimedEvent = v.object({ id: v.id("analyticsOutbox"), leaseToken: v.string(), environment: v.string(), uuid: v.string(), name: v.string(), occurredAt: v.number(), distinctId: v.string(), properties: analyticsProperties });

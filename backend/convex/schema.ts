import { defineSchema, defineTable } from "convex/server";
import { authTables } from "@convex-dev/auth/server";
import { v } from "convex/values";
import { analyticsProperties, historyMode, historyStatus } from "./telemetryValidators";
import { boardVariant, traceEvent, attemptState } from "./validators";

export default defineSchema({
  ...authTables,
  runHistory: defineTable({
    userId: v.id("users"), clientRunId: v.string(), mode: historyMode, status: historyStatus,
    startedAt: v.number(), endedAt: v.number(), score: v.number(), revives: v.number(), elapsedMs: v.number(),
    leaderboardRunId: v.optional(v.id("leaderboardRuns")), dailyAttemptId: v.optional(v.id("attempts")),
    receivedAt: v.number(), updatedAt: v.number(), rankingStatus: v.optional(v.string()), rankedScore: v.optional(v.number()),
  }).index("by_userId_and_clientRunId", ["userId", "clientRunId"]).index("by_userId_and_startedAt", ["userId", "startedAt"]).index("by_leaderboardRunId", ["leaderboardRunId"]).index("by_dailyAttemptId", ["dailyAttemptId"]),
  analyticsReceipts: defineTable({ userId: v.id("users"), eventId: v.string(), expiresAt: v.number() })
    .index("by_userId_and_eventId", ["userId", "eventId"]).index("by_expiresAt", ["expiresAt"]).index("by_userId", ["userId"]),
  analyticsOutbox: defineTable({
    userId: v.id("users"), uuid: v.string(), name: v.string(), occurredAt: v.number(), distinctId: v.string(),
    properties: analyticsProperties, consentRevision: v.number(), attempts: v.number(), nextAttemptAt: v.number(),
    expiresAt: v.number(), leaseToken: v.optional(v.string()), analyticsEligible: v.optional(v.boolean()), environment: v.optional(v.string()),
  }).index("by_userId_and_consentRevision", ["userId", "consentRevision"]).index("by_nextAttemptAt", ["nextAttemptAt"]).index("by_expiresAt", ["expiresAt"]).index("by_userId", ["userId"]),
  leaderboardProfiles: defineTable({ userId: v.id("users"), nickname: v.string(), nicknameKey: v.string(), participating: v.boolean(), hasJoinedLeaderboard: v.optional(v.boolean()), updatedAt: v.number() })
    .index("by_userId", ["userId"]).index("by_nicknameKey", ["nicknameKey"]),
  leaderboardControls: defineTable({ key: v.literal("public"), enabled: v.boolean(), updatedAt: v.number() }).index("by_key", ["key"]),
  leaderboardRuns: defineTable({
    userId: v.id("users"), requestId: v.string(), mode: v.union(v.literal("flow"), v.literal("rush")), date: v.string(),
    status: v.union(v.literal("open"), v.literal("accepted"), v.literal("rejected"), v.literal("expired")),
    analyticsEligible: v.optional(v.boolean()), clientRulesRevision: v.number(), startedAt: v.number(), uploadDeadline: v.number(), purgeAt: v.number(),
    score: v.optional(v.number()), revives: v.optional(v.number()), elapsedMs: v.optional(v.number()), reason: v.optional(v.string()),
  }).index("by_userId_and_requestId", ["userId", "requestId"]).index("by_status_and_uploadDeadline", ["status", "uploadDeadline"]).index("by_purgeAt", ["purgeAt"]),
  leaderboardBests: defineTable({
    userId: v.id("users"), mode: v.union(v.literal("flow"), v.literal("rush")), date: v.string(), score: v.number(),
    negativeScore: v.number(), tieKey: v.string(), achievedAt: v.number(), runId: v.id("leaderboardRuns"), excluded: v.boolean(),
    exclusionReason: v.optional(v.string()), expiresAt: v.number(),
  }).index("by_date_and_mode_and_userId", ["date", "mode", "userId"])
    .index("by_date_and_mode_and_excluded_and_negativeScore_and_achievedAt", ["date", "mode", "excluded", "negativeScore", "achievedAt", "tieKey"])
    .index("by_expiresAt", ["expiresAt"]),
  users: defineTable({ ...authTables.users.validator.fields, closedTestEpoch: v.optional(v.string()), analyticsEnabled: v.optional(v.boolean()), analyticsConsentRevision: v.optional(v.number()) }).index("email", ["email"]).index("phone", ["phone"]),
  challenges: defineTable({
    date: v.string(), seed: v.number(), rulesVersion: v.number(), variant: boardVariant,
    opensAt: v.number(), closesAt: v.number(), uploadDeadline: v.number(), expiresAt: v.number(), sealedAt: v.optional(v.number()),
  }).index("by_date", ["date"]).index("by_expiresAt", ["expiresAt"]).index("by_uploadDeadline", ["uploadDeadline"]),
  controls: defineTable({ key: v.literal("daily"), rankedEnabled: v.boolean(), publicCompetitionEnabled: v.literal(false), updatedAt: v.number() })
    .index("by_key", ["key"]),
  attempts: defineTable({
    userId: v.id("users"), challengeId: v.id("challenges"), requestId: v.string(), status: attemptState,
    startedAt: v.number(), uploadDeadline: v.number(), nextChunkIndex: v.number(), eventCount: v.number(),
    submittedAt: v.optional(v.number()), finalizedAt: v.optional(v.number()), score: v.optional(v.number()), reason: v.optional(v.string()),
    analyticsEligible: v.optional(v.boolean()), workflowId: v.optional(v.string()), purgeAt: v.number(), clientRulesRevision: v.optional(v.number()),
  }).index("by_userId_and_requestId", ["userId", "requestId"])
    .index("by_challengeId_and_userId", ["challengeId", "userId"])
    .index("by_challengeId_and_status", ["challengeId", "status"])
    .index("by_status_and_submittedAt", ["status", "submittedAt"])
    .index("by_purgeAt", ["purgeAt"]).index("by_status_and_uploadDeadline", ["status", "uploadDeadline"]),
  traceChunks: defineTable({ attemptId: v.id("attempts"), index: v.number(), events: v.array(traceEvent) })
    .index("by_attemptId_and_index", ["attemptId", "index"]),
  dailyBests: defineTable({
    challengeId: v.id("challenges"), userId: v.id("users"), score: v.number(), attemptId: v.id("attempts"),
    updatedAt: v.number(), expiresAt: v.number(), excluded: v.boolean(), exclusionReason: v.optional(v.string()),
  }).index("by_challengeId_and_userId", ["challengeId", "userId"])
    .index("by_expiresAt", ["expiresAt"]),
});

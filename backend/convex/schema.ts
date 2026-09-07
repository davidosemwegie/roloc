import { defineSchema, defineTable } from "convex/server";
import { authTables } from "@convex-dev/auth/server";
import { v } from "convex/values";
import { boardVariant, traceEvent, attemptState } from "./validators";

export default defineSchema({
  ...authTables,
  users: defineTable({ ...authTables.users.validator.fields, closedTestEpoch: v.optional(v.string()) }).index("email", ["email"]).index("phone", ["phone"]),
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
    workflowId: v.optional(v.string()), purgeAt: v.number(),
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

import { v } from "convex/values";
export const boardVariant = v.union(v.literal("lively"), v.literal("still"));
export const traceEvent = v.object({
  kind: v.union(v.literal("drop"), v.literal("timeout"), v.literal("pause"), v.literal("resume"), v.literal("abandon")),
  round: v.number(), tMs: v.number(), elapsedMs: v.number(), color: v.number(), xQ: v.number(), yQ: v.number(),
});
export const challengePublic = v.object({
  id: v.id("challenges"), date: v.string(), seed: v.number(), rulesVersion: v.number(), variant: boardVariant,
  opensAt: v.number(), closesAt: v.number(), uploadDeadline: v.number(), serverNow: v.number(), rankedEnabled: v.boolean(),
  publicCompetitionEnabled: v.literal(false),
});
export const attemptState = v.union(v.literal("open"), v.literal("validating"), v.literal("accepted"), v.literal("rejected"), v.literal("expired"));
export const attemptPublic = v.object({
  attemptId: v.id("attempts"), challengeId: v.id("challenges"), status: attemptState, nextChunkIndex: v.number(),
  score: v.union(v.number(), v.null()), reason: v.union(v.string(), v.null()), uploadDeadline: v.number(),
});
export const standingPublic = v.object({
  challengeId: v.id("challenges"), date: v.string(), variant: boardVariant, bestScore: v.union(v.number(), v.null()),
  participants: v.number(), percentile: v.union(v.number(), v.null()), topPercent: v.union(v.number(), v.null()),
  provisional: v.boolean(), early: v.boolean(), waiting: v.boolean(), excluded: v.boolean(),
});

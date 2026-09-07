import { getAuthUserId } from "@convex-dev/auth/server";
import { ConvexError } from "convex/values";
import { TableAggregate } from "@convex-dev/aggregate";
import { RateLimiter, MINUTE } from "@convex-dev/rate-limiter";
import { components } from "./_generated/api";
import type { DataModel, Doc, Id } from "./_generated/dataModel";
import type { QueryCtx, MutationCtx } from "./_generated/server";

export const DAY = 86_400_000;
export const MAX_CHUNK_EVENTS = 128;
export const MAX_CHUNKS = 512;
export const scores = new TableAggregate<{ Namespace: Id<"challenges">; Key: number; DataModel: DataModel; TableName: "dailyBests" }>(components.dailyScores, {
  namespace: (doc) => doc.challengeId, sortKey: (doc) => doc.score,
});
export const limits = new RateLimiter(components.rateLimiter, {
  starts: { kind: "token bucket", rate: 30, period: MINUTE, capacity: 10 },
  uploads: { kind: "token bucket", rate: 120, period: MINUTE, capacity: 128 },
  finishes: { kind: "token bucket", rate: 30, period: MINUTE, capacity: 10 },
});
export function fail(code: string, message: string): never { throw new ConvexError({ code, message }); }
export function integer(value: number, min: number, max: number, name: string) {
  if (!Number.isSafeInteger(value) || value < min || value > max) fail("INVALID_ARGUMENT", `Invalid ${name}.`);
}
export async function requireGuest(ctx: QueryCtx | MutationCtx) {
  const id = await getAuthUserId(ctx);
  if (!id) fail("UNAUTHENTICATED", "Sign in to enter ranked Daily.");
  const user = await ctx.db.get(id);
  if (!user || !process.env.RING_RUSH_CLOSED_TEST_CODE || user.closedTestEpoch !== (process.env.RING_RUSH_CLOSED_TEST_EPOCH ?? "1"))
    fail("CLOSED_TEST", "Your access to this closed test is no longer active.");
  return id;
}
export async function ownedAttempt(ctx: QueryCtx | MutationCtx, id: Id<"attempts">) {
  const userId = await requireGuest(ctx);
  const attempt = await ctx.db.get(id);
  if (!attempt || attempt.userId !== userId) fail("NOT_FOUND", "Attempt not found.");
  return attempt;
}
export async function rankedEnabled(ctx: QueryCtx | MutationCtx) {
  const controls = await ctx.db.query("controls").withIndex("by_key", q => q.eq("key", "daily")).unique();
  return controls?.rankedEnabled ?? true;
}
export function attemptDto(attempt: Doc<"attempts">) {
  return { attemptId: attempt._id, challengeId: attempt.challengeId, status: attempt.status,
    nextChunkIndex: attempt.nextChunkIndex, score: attempt.score ?? null, reason: attempt.reason ?? null, uploadDeadline: attempt.uploadDeadline };
}
export function utcDate(time: number) { return new Date(time).toISOString().slice(0, 10); }
export function midnight(time: number) { return Math.floor(time / DAY) * DAY; }
export function standingNumbers(lower: number, tied: number, participants: number) {
  const percentile = participants > 1 ? 100 * (lower + .5 * tied) / participants : null;
  return { percentile, topPercent: percentile === null ? null : Math.max(1, Math.ceil(100 - percentile)), early: participants < 20, waiting: participants <= 1 };
}

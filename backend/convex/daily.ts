import { v } from "convex/values";
import { query, mutation } from "./_generated/server";
import { internal } from "./_generated/api";
import { attemptPublic, challengePublic, standingPublic, traceEvent } from "./validators";
import { attemptDto, DAY, fail, integer, limits, MAX_CHUNK_EVENTS, MAX_CHUNKS, ownedAttempt, rankedEnabled, requireGuest, scores, standingNumbers, utcDate } from "./model";
import { workflow } from "./validation";
import type { Infer } from "convex/values";

export const current = query({
  args: {}, returns: v.union(challengePublic, v.null()),
  handler: async (ctx) => {
    const now = Date.now();
    const challenge = await ctx.db.query("challenges").withIndex("by_date", q => q.eq("date", utcDate(now))).unique();
    if (!challenge || now < challenge.opensAt || now >= challenge.closesAt) return null;
    return { id: challenge._id, date: challenge.date, seed: challenge.seed, rulesVersion: challenge.rulesVersion,
      variant: challenge.variant, opensAt: challenge.opensAt, closesAt: challenge.closesAt, uploadDeadline: challenge.uploadDeadline,
      serverNow: now, rankedEnabled: await rankedEnabled(ctx), publicCompetitionEnabled: false as const };
  },
});
export const createAttempt = mutation({
  args: { challengeId: v.id("challenges"), requestId: v.string() }, returns: attemptPublic,
  handler: async (ctx, args) => {
    const userId = await requireGuest(ctx);
    if (!/^[A-Za-z0-9_-]{8,80}$/.test(args.requestId)) fail("INVALID_ARGUMENT", "Supply a unique request ID.");
    const existing = await ctx.db.query("attempts").withIndex("by_userId_and_requestId", q => q.eq("userId", userId).eq("requestId", args.requestId)).unique();
    if (existing) {
      if (existing.challengeId !== args.challengeId) fail("IDEMPOTENCY_CONFLICT", "This request ID belongs to another challenge.");
      return attemptDto(existing);
    }
    if (!(await rankedEnabled(ctx))) fail("RANKED_PAUSED", "Ranked submissions are temporarily paused. You can play offline practice.");
    const challenge = await ctx.db.get(args.challengeId), now = Date.now();
    if (!challenge || now < challenge.opensAt || now >= challenge.closesAt) fail("CHALLENGE_CLOSED", "This challenge is not open for new attempts.");
    if (challenge.rulesVersion !== 1) fail("UPDATE_REQUIRED", "Update Ring Rush to play this Daily.");
    await limits.limit(ctx, "starts", { key: userId, throws: true });
    const id = await ctx.db.insert("attempts", { userId, challengeId: args.challengeId, requestId: args.requestId,
      status: "open", startedAt: now, uploadDeadline: challenge.uploadDeadline, nextChunkIndex: 0, eventCount: 0,
      purgeAt: challenge.uploadDeadline + 7 * DAY });
    return attemptDto((await ctx.db.get(id))!);
  },
});
export const appendChunk = mutation({
  args: { attemptId: v.id("attempts"), index: v.number(), events: v.array(traceEvent) }, returns: attemptPublic,
  handler: async (ctx, args) => {
    const attempt = await ownedAttempt(ctx, args.attemptId);
    integer(args.index, 0, MAX_CHUNKS - 1, "chunk index");
    if (args.events.length < 1 || args.events.length > MAX_CHUNK_EVENTS) fail("INVALID_ARGUMENT", `Each chunk must contain 1–${MAX_CHUNK_EVENTS} events.`);
    // Reject malformed/oversized numeric payloads before storing, then replay semantics in the workflow.
    for (const event of args.events) {
      integer(event.round, 0, MAX_CHUNKS * MAX_CHUNK_EVENTS, "round");
      integer(event.tMs, 0, 90_000_000, "run time"); integer(event.elapsedMs, 0, 4000, "round time");
      integer(event.color, -1, 3, "color"); integer(event.xQ, -1000000, 1000000, "x coordinate"); integer(event.yQ, -1000000, 1000000, "y coordinate");
    }
    const existing = await ctx.db.query("traceChunks").withIndex("by_attemptId_and_index", q => q.eq("attemptId", args.attemptId).eq("index", args.index)).unique();
    if (existing) {
      if (!sameEvents(existing.events, args.events)) fail("IDEMPOTENCY_CONFLICT", "This chunk was already uploaded with different events.");
      return attemptDto(attempt);
    }
    if (attempt.status !== "open") fail("ATTEMPT_CLOSED", "This attempt is no longer accepting events.");
    if (Date.now() >= attempt.uploadDeadline) {
      await ctx.db.patch(attempt._id, { status: "expired", reason: "The upload deadline passed.", finalizedAt: Date.now() });
      return attemptDto((await ctx.db.get(attempt._id))!);
    }
    if (!(await rankedEnabled(ctx))) fail("RANKED_PAUSED", "Ranked submissions are temporarily paused.");
    if (args.index !== attempt.nextChunkIndex) fail("CHUNK_ORDER", `Upload chunk ${attempt.nextChunkIndex} next.`);
    await limits.limit(ctx, "uploads", { key: attempt.userId, throws: true });
    await ctx.db.insert("traceChunks", { attemptId: attempt._id, index: args.index, events: args.events });
    await ctx.db.patch(attempt._id, { nextChunkIndex: args.index + 1, eventCount: attempt.eventCount + args.events.length });
    return attemptDto((await ctx.db.get(attempt._id))!);
  },
});
export const finalize = mutation({
  args: { attemptId: v.id("attempts"), chunkCount: v.number() }, returns: attemptPublic,
  handler: async (ctx, args): Promise<Infer<typeof attemptPublic>> => {
    const attempt = await ownedAttempt(ctx, args.attemptId);
    integer(args.chunkCount, 1, MAX_CHUNKS, "chunk count");
    if (args.chunkCount !== attempt.nextChunkIndex) fail("MISSING_CHUNKS", "Upload all chunks before finishing the attempt.");
    if (attempt.status !== "open") return attemptDto(attempt);
    if (Date.now() >= attempt.uploadDeadline) {
      await ctx.db.patch(attempt._id, { status: "expired", reason: "The upload deadline passed.", finalizedAt: Date.now() });
      return attemptDto((await ctx.db.get(attempt._id))!);
    }
    if (!(await rankedEnabled(ctx))) fail("RANKED_PAUSED", "Ranked submissions are temporarily paused.");
    await limits.limit(ctx, "finishes", { key: attempt.userId, throws: true });
    await ctx.db.patch(attempt._id, { status: "validating", submittedAt: Date.now() });
    const workflowId = await workflow.start(ctx, internal.validation.validateAttempt, { attemptId: attempt._id, chunkCount: args.chunkCount },
      { onComplete: internal.validation.onComplete, context: { attemptId: attempt._id } });
    await ctx.db.patch(attempt._id, { workflowId });
    return attemptDto((await ctx.db.get(attempt._id))!);
  },
});
export const attemptStatus = query({
  args: { attemptId: v.id("attempts") }, returns: attemptPublic,
  handler: async (ctx, args) => attemptDto(await ownedAttempt(ctx, args.attemptId)),
});
export const myStanding = query({
  args: { challengeId: v.id("challenges") }, returns: standingPublic,
  handler: async (ctx, args) => {
    const userId = await requireGuest(ctx);
    const challenge = await ctx.db.get(args.challengeId);
    if (!challenge || Date.now() < challenge.opensAt || Date.now() >= challenge.uploadDeadline + 365 * DAY) fail("NOT_FOUND", "Challenge not found.");
    const best = await ctx.db.query("dailyBests").withIndex("by_challengeId_and_userId", q => q.eq("challengeId", args.challengeId).eq("userId", userId)).unique();
    const participants = await scores.count(ctx, { namespace: args.challengeId });
    let lower = 0, tied = 0;
    if (best && !best.excluded) {
      lower = await scores.count(ctx, { namespace: args.challengeId, bounds: { upper: { key: best.score, inclusive: false } } });
      tied = await scores.count(ctx, { namespace: args.challengeId, bounds: { lower: { key: best.score, inclusive: true }, upper: { key: best.score, inclusive: true } } });
    }
    const numbers = standingNumbers(lower, tied, participants);
    return { challengeId: args.challengeId, date: challenge.date, variant: challenge.variant, bestScore: best?.score ?? null,
      participants, ...numbers, percentile: best && !best.excluded ? numbers.percentile : null,
      topPercent: best && !best.excluded ? numbers.topPercent : null, provisional: challenge.sealedAt === undefined, excluded: best?.excluded ?? false };
  },
});

function sameEvents(a: Infer<typeof traceEvent>[], b: Infer<typeof traceEvent>[]) {
  return a.length === b.length && a.every((event, i) => {
    const other = b[i];
    return event.kind === other.kind && event.round === other.round && event.tMs === other.tMs && event.elapsedMs === other.elapsedMs
      && event.color === other.color && event.xQ === other.xQ && event.yQ === other.yQ;
  });
}

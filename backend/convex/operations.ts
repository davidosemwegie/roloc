import { v } from "convex/values";
import { internalMutation, internalQuery } from "./_generated/server";
import { internal } from "./_generated/api";
import { cleanup, vWorkflowId } from "@convex-dev/workflow";
import { components } from "./_generated/api";
import { DAY, utcDate, rankedEnabled, scores, fail } from "./model";

export const health = internalQuery({
  args: {}, returns: v.object({ ready: v.boolean(), todayAvailable: v.boolean(), tomorrowAvailable: v.boolean(), validationStalled: v.boolean(), rankedEnabled: v.boolean(), publicCompetitionEnabled: v.literal(false) }),
  handler: async (ctx) => {
    const today = await ctx.db.query("challenges").withIndex("by_date", q => q.eq("date", utcDate(Date.now()))).unique();
    const tomorrow = await ctx.db.query("challenges").withIndex("by_date", q => q.eq("date", utcDate(Date.now() + DAY))).unique();
    const stalled = await ctx.db.query("attempts").withIndex("by_status_and_submittedAt", q => q.eq("status", "validating").lte("submittedAt", Date.now() - 300_000)).first();
    return { ready: !!today && !!tomorrow && !stalled, validationStalled: !!stalled, todayAvailable: !!today, tomorrowAvailable: !!tomorrow, rankedEnabled: await rankedEnabled(ctx), publicCompetitionEnabled: false as const };
  },
});
export const setRankedEnabled = internalMutation({
  args: { enabled: v.boolean() }, returns: v.null(),
  handler: async (ctx, args) => {
    const row = await ctx.db.query("controls").withIndex("by_key", q => q.eq("key", "daily")).unique();
    const value = { key: "daily" as const, rankedEnabled: args.enabled, publicCompetitionEnabled: false as const, updatedAt: Date.now() };
    if (row) await ctx.db.replace(row._id, value); else await ctx.db.insert("controls", value);
    return null;
  },
});
export const excludeStanding = internalMutation({
  args: { challengeId: v.id("challenges"), userId: v.id("users"), reason: v.string() }, returns: v.null(),
  handler: async (ctx, args) => {
    if (!args.reason.trim() || args.reason.length > 240) fail("INVALID_ARGUMENT", "Supply a brief exclusion reason.");
    const challenge = await ctx.db.get(args.challengeId);
    if (!challenge) fail("NOT_FOUND", "Challenge not found.");
    if (challenge.sealedAt !== undefined) fail("FROZEN", "Finalized standings are immutable.");
    const best = await ctx.db.query("dailyBests").withIndex("by_challengeId_and_userId", q => q.eq("challengeId", args.challengeId).eq("userId", args.userId)).unique();
    if (best && !best.excluded) {
      await scores.delete(ctx, best);
      await ctx.db.patch(best._id, { excluded: true, exclusionReason: args.reason });
    }
    return null;
  },
});
export const sealDue = internalMutation({
  args: {}, returns: v.number(),
  handler: async (ctx) => {
    // Look back seven days so interrupted validation can finish after the upload window.
    const due = await ctx.db.query("challenges").withIndex("by_uploadDeadline", q => q.gte("uploadDeadline", Date.now() - 7 * DAY).lte("uploadDeadline", Date.now())).take(8);
    let sealed = 0;
    for (const challenge of due) {
      if (challenge.sealedAt !== undefined) continue;
      const pending = await ctx.db.query("attempts").withIndex("by_challengeId_and_status", q => q.eq("challengeId", challenge._id).eq("status", "validating")).first();
      if (!pending) { await ctx.db.patch(challenge._id, { sealedAt: Date.now() }); sealed++; }
    }
    return sealed;
  },
});
export const expireOpen = internalMutation({
  args: {}, returns: v.number(),
  handler: async (ctx) => {
    const rows = await ctx.db.query("attempts").withIndex("by_status_and_uploadDeadline", q => q.eq("status", "open").lte("uploadDeadline", Date.now())).take(100);
    for (const row of rows) await ctx.db.patch(row._id, { status: "expired", reason: "The upload deadline passed.", finalizedAt: Date.now() });
    if (rows.length === 100) await ctx.scheduler.runAfter(0, internal.operations.expireOpen, {});
    return rows.length;
  },
});
export const cleanupWorkflow = internalMutation({
  args: { workflowId: vWorkflowId }, returns: v.null(),
  handler: async (ctx, args) => { await cleanup(ctx, components.workflow, args.workflowId); return null; },
});
export const purge = internalMutation({
  args: {}, returns: v.number(),
  handler: async (ctx) => {
    let count = 0;
    const attempts = await ctx.db.query("attempts").withIndex("by_purgeAt", q => q.lte("purgeAt", Date.now())).take(20);
    for (const attempt of attempts) {
      const chunks = await ctx.db.query("traceChunks").withIndex("by_attemptId_and_index", q => q.eq("attemptId", attempt._id)).take(100);
      for (const chunk of chunks) await ctx.db.delete(chunk._id);
      count += chunks.length;
      if (chunks.length < 100) { await ctx.db.delete(attempt._id); count++; }
    }
    const bests = await ctx.db.query("dailyBests").withIndex("by_expiresAt", q => q.lte("expiresAt", Date.now())).take(100);
    for (const best of bests) { if (!best.excluded) await scores.delete(ctx, best); await ctx.db.delete(best._id); count++; }
    const challenges = await ctx.db.query("challenges").withIndex("by_expiresAt", q => q.lte("expiresAt", Date.now())).take(10);
    for (const challenge of challenges) {
      const best = await ctx.db.query("dailyBests").withIndex("by_challengeId_and_userId", q => q.eq("challengeId", challenge._id)).first();
      if (!best) { await ctx.db.delete(challenge._id); count++; }
    }
    if (attempts.length === 20 || bests.length === 100 || count >= 100) await ctx.scheduler.runAfter(0, internal.operations.purge, {});
    return count;
  },
});

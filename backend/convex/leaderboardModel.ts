import { getAuthUserId } from "@convex-dev/auth/server";
import { TableAggregate } from "@convex-dev/aggregate";
import { RateLimiter, MINUTE, HOUR } from "@convex-dev/rate-limiter";
import { v } from "convex/values";
import { components } from "./_generated/api";
import type { DataModel, Doc, Id } from "./_generated/dataModel";
import type { MutationCtx, QueryCtx } from "./_generated/server";
import { fail } from "./model";
export const mode = v.union(v.literal("flow"), v.literal("rush"));
export const profileValue = v.object({ nickname: v.string(), participating: v.boolean() });
export const ticketValue = v.object({ runId: v.id("leaderboardRuns"), mode, date: v.string(), status: v.union(v.literal("open"), v.literal("accepted"), v.literal("rejected"), v.literal("expired")), startedAt: v.number(), uploadDeadline: v.number(), score: v.union(v.number(), v.null()), reason: v.union(v.string(), v.null()) });
export const entryValue = v.object({ nickname: v.string(), score: v.number(), rank: v.number(), isMe: v.boolean() });
export const publicScores = new TableAggregate<{ Namespace: string; Key: number; DataModel: DataModel; TableName: "leaderboardBests" }>(components.leaderboardScores, {
  namespace: doc => `${doc.date}/${doc.mode}`, sortKey: doc => doc.score,
});
export const publicLimits = new RateLimiter(components.rateLimiter, {
  registrations: { kind: "token bucket", rate: 1000, period: HOUR, capacity: 100 },
  leaderboardStarts: { kind: "token bucket", rate: 30, period: MINUTE, capacity: 10 },
  leaderboardSubmissions: { kind: "token bucket", rate: 60, period: MINUTE, capacity: 20 },
  nicknameChanges: { kind: "token bucket", rate: 5, period: HOUR, capacity: 3 },
});
export async function publicUser(ctx: QueryCtx | MutationCtx) {
  const id = await getAuthUserId(ctx);
  if (!id || !await ctx.db.get(id)) fail("UNAUTHENTICATED", "Sign in to use leaderboards.");
  return id;
}
export async function enabled(ctx: QueryCtx | MutationCtx) {
  return (await ctx.db.query("leaderboardControls").withIndex("by_key", q => q.eq("key", "public")).unique())?.enabled ?? false;
}
export async function ownedRun(ctx: QueryCtx | MutationCtx, runId: Id<"leaderboardRuns">) {
  const userId = await publicUser(ctx), run = await ctx.db.get(runId);
  if (!run || run.userId !== userId) fail("NOT_FOUND", "Run not found.");
  return run;
}
export function ticket(run: Doc<"leaderboardRuns">) {
  const expired = run.status === "open" && Date.now() >= run.uploadDeadline;
  return { runId: run._id, mode: run.mode, date: run.date, status: expired ? "expired" as const : run.status, startedAt: run.startedAt, uploadDeadline: run.uploadDeadline, score: run.score ?? null, reason: expired ? "The upload deadline passed." : run.reason ?? null };
}

export function registrationProfile(closedTestCode: unknown): { isAnonymous: true; closedTestEpoch?: string } {
  if (closedTestCode === undefined || closedTestCode === "") return { isAnonymous: true };
  const expected = process.env.RING_RUSH_CLOSED_TEST_CODE;
  if (!expected || typeof closedTestCode !== "string" || closedTestCode !== expected)
    fail("CLOSED_TEST", "This Daily is currently available to invited testers.");
  return { isAnonymous: true, closedTestEpoch: process.env.RING_RUSH_CLOSED_TEST_EPOCH ?? "1" };
}

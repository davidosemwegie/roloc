import { RateLimiter, MINUTE } from "@convex-dev/rate-limiter";
import type { Infer } from "convex/values";
import type { Id } from "./_generated/dataModel";
import type { MutationCtx } from "./_generated/server";
import { components } from "./_generated/api";
import { DAY, fail, integer } from "./model";
import { analyticsProperties } from "./telemetryValidators";

export const telemetryLimits = new RateLimiter(components.rateLimiter, {
  telemetryRuns: { kind: "token bucket", rate: 120, period: MINUTE, capacity: 100 },
  telemetryEvents: { kind: "token bucket", rate: 120, period: MINUTE, capacity: 60 },
});
export function playerIdentity(userId: Id<"users">) { return `${process.env.CONVEX_SITE_URL ?? "local"}:${userId}`; }
export function posthogConfig() {
  const token = process.env.POSTHOG_PROJECT_TOKEN, host = process.env.POSTHOG_HOST;
  if (!token || !host || !["https://us.i.posthog.com", "https://eu.i.posthog.com"].includes(host)) return null;
  return { token, host };
}
export function uuid() {
  // Convex mutations seed Math.random transactionally; persist once for every retry.
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
    const value = Math.floor(Math.random() * 16); return (c === "x" ? value : (value & 3) | 8).toString(16);
  });
}
export function boundedId(value: string, field: string, optional = false, allowColon = false) {
  if (optional && value === "") return;
  if (!(allowColon ? /^[A-Za-z0-9_:-]{8,128}$/ : /^[A-Za-z0-9_-]{8,128}$/).test(value)) fail("INVALID_ARGUMENT", `Invalid ${field}.`);
}
export function clientTimestamp(value: number, field: string) {
  integer(value, Date.now() - 7 * DAY, Date.now() + 300_000, field);
}
export async function enqueue(ctx: MutationCtx, userId: Id<"users">, eventId: string, name: string, occurredAt: number, properties: Infer<typeof analyticsProperties>, analyticsEnabledAtOccurrence = true) {
  const prior = await ctx.db.query("analyticsReceipts").withIndex("by_userId_and_eventId", q => q.eq("userId", userId).eq("eventId", eventId)).unique();
  if (prior) return false;
  // Keep receipts even when opted out/unconfigured: later retries cannot backfill.
  await ctx.db.insert("analyticsReceipts", { userId, eventId, expiresAt: Date.now() + 8 * DAY });
  const user = await ctx.db.get(userId);
  if (!analyticsEnabledAtOccurrence || !user || user.analyticsEnabled === false || !posthogConfig()) return true;
  await ctx.db.insert("analyticsOutbox", { userId, uuid: uuid(), name, occurredAt, distinctId: playerIdentity(userId), properties,
    consentRevision: user.analyticsConsentRevision ?? 0, attempts: 0, nextAttemptAt: Date.now(), expiresAt: Date.now() + 7 * DAY });
  return true;
}

export async function rankingOutcome(ctx: MutationCtx, userId: Id<"users">, runId: Id<"leaderboardRuns"> | Id<"attempts">, mode: "flow" | "rush" | "daily", status: "accepted" | "rejected" | "expired", score?: number) {
  const history = mode === "daily"
    ? await ctx.db.query("runHistory").withIndex("by_dailyAttemptId", q => q.eq("dailyAttemptId", runId as Id<"attempts">)).unique()
    : await ctx.db.query("runHistory").withIndex("by_leaderboardRunId", q => q.eq("leaderboardRunId", runId as Id<"leaderboardRuns">)).unique();
  if (history) await ctx.db.patch(history._id, { rankingStatus: status, ...(status === "accepted" && score !== undefined ? { rankedScore: score } : {}), updatedAt: Date.now() });
  await enqueue(ctx, userId, `ranking:${runId}:${status}`, `leaderboard_score_${status}`, Date.now(), { mode, runId, status, ...(score === undefined ? {} : { score }), source: "server_validation" });
}

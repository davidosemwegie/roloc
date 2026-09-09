import { v } from "convex/values";
import { internalAction, internalMutation, internalQuery } from "./_generated/server";
import { internal } from "./_generated/api";
import { claimedEvent } from "./telemetryValidators";
import { posthogConfig, uuid } from "./telemetryModel";

export const claim = internalMutation({ args: {}, returns: v.array(claimedEvent), handler: async ctx => {
  if (!posthogConfig()) return [];
  const due = await ctx.db.query("analyticsOutbox").withIndex("by_nextAttemptAt", q => q.lte("nextAttemptAt", Date.now())).take(50);
  const result = [];
  for (const row of due) {
    const user = await ctx.db.get(row.userId);
    if (!user || user.analyticsEnabled === false || (user.analyticsConsentRevision ?? 0) !== row.consentRevision || row.expiresAt <= Date.now()) { await ctx.db.delete(row._id); continue; }
    const leaseToken = uuid();
    await ctx.db.patch(row._id, { leaseToken, attempts: row.attempts + 1, nextAttemptAt: Date.now() + 120_000 });
    result.push({ id: row._id, leaseToken, uuid: row.uuid, name: row.name, occurredAt: row.occurredAt, distinctId: row.distinctId, properties: row.properties });
  }
  return result;
} });
export const eligible = internalQuery({ args: { events: v.array(claimedEvent) }, returns: v.array(claimedEvent), handler: async (ctx, args) => {
  const result = [];
  for (const event of args.events) {
    const row = await ctx.db.get(event.id), user = row ? await ctx.db.get(row.userId) : null;
    if (row?.leaseToken === event.leaseToken && user && user.analyticsEnabled !== false && (user.analyticsConsentRevision ?? 0) === row.consentRevision && row.expiresAt > Date.now()) result.push(event);
  }
  return result;
} });
export const acknowledge = internalMutation({ args: { events: v.array(claimedEvent), success: v.boolean() }, returns: v.null(), handler: async (ctx, args) => {
  for (const event of args.events) {
    const row = await ctx.db.get(event.id);
    if (!row || row.leaseToken !== event.leaseToken) continue;
    if (args.success || row.expiresAt <= Date.now()) await ctx.db.delete(row._id);
    else await ctx.db.patch(row._id, { leaseToken: undefined, nextAttemptAt: Date.now() + Math.min(3_600_000, 30_000 * 2 ** Math.min(row.attempts, 7)) });
  }
  return null;
} });
export const deliver = internalAction({ args: {}, returns: v.number(), handler: async (ctx): Promise<number> => {
  const config = posthogConfig(); if (!config) return 0;
  const claimed = await ctx.runMutation(internal.telemetryDelivery.claim, {});
  const events = await ctx.runQuery(internal.telemetryDelivery.eligible, { events: claimed });
  if (!events.length) return 0;
  let success = false;
  try {
    const response = await fetch(`${config.host}/batch/`, { method: "POST", headers: { "Content-Type": "application/json" }, signal: AbortSignal.timeout(20_000), body: JSON.stringify({ api_key: config.token, batch: events.map(event => ({
      uuid: event.uuid, event: event.name, timestamp: new Date(event.occurredAt).toISOString(),
      properties: { mode: event.properties.mode, score: event.properties.score, revives: event.properties.revives, elapsed_ms: event.properties.elapsedMs,
        client_run_id: event.properties.clientRunId, session_id: event.properties.sessionId, run_id: event.properties.runId,
        status: event.properties.status, participating: event.properties.participating, distinct_id: event.distinctId,
        environment: process.env.RING_RUSH_ENVIRONMENT ?? "development", source: event.properties.source ?? "server_validation", $geoip_disable: true },
    })) }) });
    success = response.ok;
  } catch { /* Retry without logging player data, payloads, or project credentials. */ }
  await ctx.runMutation(internal.telemetryDelivery.acknowledge, { events, success });
  if (!success) console.warn("Usage analytics delivery failed; queued events will retry.");
  if (claimed.length === 50) await ctx.scheduler.runAfter(1000, internal.telemetryDelivery.deliver, {});
  return success ? events.length : 0;
} });
export const purgeOptedOut = internalMutation({ args: { userId: v.id("users"), beforeRevision: v.number() }, returns: v.number(), handler: async (ctx, args) => {
  const rows = await ctx.db.query("analyticsOutbox").withIndex("by_userId_and_consentRevision", q => q.eq("userId", args.userId).lt("consentRevision", args.beforeRevision)).take(100);
  for (const row of rows) await ctx.db.delete(row._id);
  if (rows.length === 100) await ctx.scheduler.runAfter(0, internal.telemetryDelivery.purgeOptedOut, args);
  return rows.length;
} });
export const purge = internalMutation({ args: {}, returns: v.number(), handler: async ctx => {
  const rows = await ctx.db.query("analyticsOutbox").withIndex("by_expiresAt", q => q.lte("expiresAt", Date.now())).take(100);
  const receipts = await ctx.db.query("analyticsReceipts").withIndex("by_expiresAt", q => q.lte("expiresAt", Date.now())).take(100);
  for (const row of rows) await ctx.db.delete(row._id);
  for (const receipt of receipts) await ctx.db.delete(receipt._id);
  if (rows.length === 100 || receipts.length === 100) await ctx.scheduler.runAfter(0, internal.telemetryDelivery.purge, {});
  return rows.length + receipts.length;
} });
export const deletePlayerHistory = internalMutation({ args: { userId: v.id("users") }, returns: v.number(), handler: async (ctx, args) => {
  const user = await ctx.db.get(args.userId);
  if (user) await ctx.db.patch(user._id, { analyticsEnabled: false, analyticsConsentRevision: (user.analyticsConsentRevision ?? 0) + 1 });
  const runs = await ctx.db.query("runHistory").withIndex("by_userId_and_startedAt", q => q.eq("userId", args.userId)).take(100);
  const events = await ctx.db.query("analyticsOutbox").withIndex("by_userId", q => q.eq("userId", args.userId)).take(100);
  const receipts = await ctx.db.query("analyticsReceipts").withIndex("by_userId", q => q.eq("userId", args.userId)).take(100);
  for (const row of [...runs, ...events, ...receipts]) await ctx.db.delete(row._id);
  if (runs.length === 100 || events.length === 100 || receipts.length === 100) await ctx.scheduler.runAfter(0, internal.telemetryDelivery.deletePlayerHistory, args);
  return runs.length + events.length + receipts.length;
} });

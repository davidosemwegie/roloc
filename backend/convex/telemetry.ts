import { v } from "convex/values";
import { mutation, query } from "./_generated/server";
import { internal } from "./_generated/api";
import { publicUser } from "./leaderboardModel";
import { fail, integer } from "./model";
import { boundedId, clientTimestamp, enqueue, playerIdentity, telemetryLimits } from "./telemetryModel";
import { captureName, historyInput, identityValue } from "./telemetryValidators";

export const identity = query({ args: {}, returns: identityValue, handler: async ctx => {
  const id = await publicUser(ctx), user = (await ctx.db.get(id))!;
  return { playerId: playerIdentity(id), analyticsEnabled: user.analyticsEnabled !== false };
} });
export const setAnalyticsEnabled = mutation({ args: { enabled: v.boolean() }, returns: identityValue, handler: async (ctx, args) => {
  const id = await publicUser(ctx), user = (await ctx.db.get(id))!;
  if ((user.analyticsEnabled !== false) !== args.enabled) {
    const revision = (user.analyticsConsentRevision ?? 0) + 1;
    await ctx.db.patch(id, { analyticsEnabled: args.enabled, analyticsConsentRevision: revision });
    if (!args.enabled) await ctx.scheduler.runAfter(0, internal.telemetryDelivery.purgeOptedOut, { userId: id, beforeRevision: revision });
  }
  return { playerId: playerIdentity(id), analyticsEnabled: args.enabled };
} });
export const recordRuns = mutation({ args: { runs: v.array(historyInput) }, returns: v.object({ recorded: v.number(), duplicates: v.number() }), handler: async (ctx, args) => {
  const userId = await publicUser(ctx);
  if (args.runs.length < 1 || args.runs.length > 20) fail("INVALID_ARGUMENT", "Send 1–20 run summaries.");
  let recorded = 0, duplicates = 0;
  for (const run of args.runs) {
    boundedId(run.clientRunId, "run ID", false, true);
    const previous = await ctx.db.query("runHistory").withIndex("by_userId_and_clientRunId", q => q.eq("userId", userId).eq("clientRunId", run.clientRunId)).unique();
    // An authenticated exact retry must remain successful after its original
    // upload window or temporary ranking ticket has expired.
    if (previous) {
      if (previous.mode !== run.mode || previous.startedAt !== run.startedAt) fail("CONFLICT", "Run ID already used for another run.");
      if (previous.status !== "started" && run.status === "started") { duplicates++; continue; }
      if (previous.status === run.status) {
        if (previous.endedAt !== run.endedAt || previous.score !== run.score || previous.revives !== run.revives || previous.elapsedMs !== run.elapsedMs || (previous.leaderboardRunId ?? "") !== run.leaderboardRunId || (previous.dailyAttemptId ?? "") !== run.dailyAttemptId) fail("CONFLICT", "Run summary already recorded with different results.");
        duplicates++; continue;
      }
      if (previous.status !== "started") fail("CONFLICT", "Final run summaries cannot change.");
    }
    clientTimestamp(run.startedAt, "start time");
    integer(run.score, 0, 65536, "score"); integer(run.revives, 0, 1312, "revives"); integer(run.elapsedMs, 0, 90_000_000, "elapsed time");
    if (run.status === "started") {
      if (run.endedAt !== 0 || run.score !== 0 || run.revives !== 0 || run.elapsedMs !== 0) fail("INVALID_ARGUMENT", "Started summaries cannot contain a final result.");
    } else {
      clientTimestamp(run.endedAt, "end time");
      if (run.endedAt < run.startedAt || run.elapsedMs > run.endedAt - run.startedAt + 5000) fail("INVALID_ARGUMENT", "Invalid run duration.");
    }
    if (run.leaderboardRunId.length > 128 || run.dailyAttemptId.length > 128) fail("INVALID_ARGUMENT", "Invalid linked run ID.");
    const leaderboardRunId = run.leaderboardRunId ? ctx.db.normalizeId("leaderboardRuns", run.leaderboardRunId) : null;
    const dailyAttemptId = run.dailyAttemptId ? ctx.db.normalizeId("attempts", run.dailyAttemptId) : null;
    if (run.leaderboardRunId && !leaderboardRunId || run.dailyAttemptId && !dailyAttemptId) fail("INVALID_ARGUMENT", "Invalid linked run ID.");
    if (leaderboardRunId && dailyAttemptId) fail("INVALID_ARGUMENT", "A run can link only one ranking attempt.");
    let rankingStatus: string | undefined, rankedScore: number | undefined;
    if (leaderboardRunId) {
      const ranked = await ctx.db.get(leaderboardRunId);
      if (!ranked || ranked.userId !== userId || ranked.mode !== run.mode) fail("NOT_FOUND", "Linked leaderboard run not found.");
      rankingStatus = ranked.status; rankedScore = ranked.status === "accepted" ? ranked.score : undefined;
      const linked = await ctx.db.query("runHistory").withIndex("by_leaderboardRunId", q => q.eq("leaderboardRunId", leaderboardRunId)).unique();
      if (linked && linked.clientRunId !== run.clientRunId) fail("CONFLICT", "Ranking attempt already linked to another run.");
    }
    if (dailyAttemptId) {
      const daily = await ctx.db.get(dailyAttemptId);
      if (!daily || daily.userId !== userId || run.mode !== "daily") fail("NOT_FOUND", "Linked Daily attempt not found.");
      rankingStatus = daily.status; rankedScore = daily.status === "accepted" ? daily.score : undefined;
      const linked = await ctx.db.query("runHistory").withIndex("by_dailyAttemptId", q => q.eq("dailyAttemptId", dailyAttemptId)).unique();
      if (linked && linked.clientRunId !== run.clientRunId) fail("CONFLICT", "Ranking attempt already linked to another run.");
    }
    const value = { userId, clientRunId: run.clientRunId, mode: run.mode, status: run.status, startedAt: run.startedAt, endedAt: run.endedAt, score: run.score, revives: run.revives, elapsedMs: run.elapsedMs,
      ...(rankingStatus ? { rankingStatus } : {}), ...(rankedScore !== undefined ? { rankedScore } : {}),
      ...(leaderboardRunId ? { leaderboardRunId } : {}), ...(dailyAttemptId ? { dailyAttemptId } : {}) };
    if (previous && (previous.leaderboardRunId && previous.leaderboardRunId !== leaderboardRunId || previous.dailyAttemptId && previous.dailyAttemptId !== dailyAttemptId)) fail("CONFLICT", "Linked ranking attempt cannot change.");
    await telemetryLimits.limit(ctx, "telemetryRuns", { key: userId, throws: true });
    if (previous) await ctx.db.replace(previous._id, { ...value, receivedAt: previous.receivedAt, updatedAt: Date.now() });
    else await ctx.db.insert("runHistory", { ...value, receivedAt: Date.now(), updatedAt: Date.now() });
    // A final summary arriving alone still proves that a run started.
    if (!previous) await enqueue(ctx, userId, `run:${run.clientRunId}:started`, "run_started", run.startedAt, { mode: run.mode, clientRunId: run.clientRunId, source: "client_summary" }, run.analyticsEnabled !== false, run.analyticsEligible === true);
    if (run.status !== "started") await enqueue(ctx, userId, `run:${run.clientRunId}:final`, `run_${run.status}`, run.endedAt, { mode: run.mode, clientRunId: run.clientRunId, score: run.score, revives: run.revives, elapsedMs: run.elapsedMs, status: run.status, source: "client_summary" }, run.analyticsEnabled !== false, run.analyticsEligible === true);
    recorded++;
  }
  return { recorded, duplicates };
} });
export const capture = mutation({ args: { analyticsEligible: v.optional(v.boolean()), eventId: v.string(), name: captureName, occurredAt: v.number(), sessionId: v.string(), mode: v.string(), clientRunId: v.string() }, returns: v.null(), handler: async (ctx, args) => {
  const userId = await publicUser(ctx);
  boundedId(args.eventId, "event ID"); boundedId(args.sessionId, "session ID"); boundedId(args.clientRunId, "run ID", true, true);
  if (!["", "flow", "rush", "daily"].includes(args.mode)) fail("INVALID_ARGUMENT", "Invalid game mode.");
  const receipt = await ctx.db.query("analyticsReceipts").withIndex("by_userId_and_eventId", q => q.eq("userId", userId).eq("eventId", `client:${args.eventId}`)).unique();
  if (receipt) return null;
  clientTimestamp(args.occurredAt, "event time");
  await telemetryLimits.limit(ctx, "telemetryEvents", { key: userId, throws: true });
  await enqueue(ctx, userId, `client:${args.eventId}`, args.name, args.occurredAt, { sessionId: args.sessionId, mode: args.mode, clientRunId: args.clientRunId, source: "client" }, true, args.analyticsEligible === true);
  return null;
} });

import { enqueue, rankingOutcome, uuid } from "./telemetryModel";
import { v } from "convex/values";
import { mutation, query, internalMutation } from "./_generated/server";
import { internal } from "./_generated/api";
import { DAY, midnight, utcDate, fail, integer } from "./model";
import { mode, profileValue, ticketValue, entryValue, publicScores, allTimeScores, publicLimits, publicUser, enabled, ownedRun, ticket } from "./leaderboardModel";

// Local Flow/Lively records have no date or run ticket. Keep them separate from daily results.
export const syncBest = mutation({ args: { score: v.number() }, returns: v.object({ score: v.number(), status: v.union(v.literal("synced"), v.literal("excluded")) }), handler: async (ctx, args) => {
  const userId = await publicUser(ctx);
  integer(args.score, 0, 65536, "score");
  const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  if (!profile?.participating || !profile.nickname) fail("OPT_IN_REQUIRED", "Choose a nickname and enable leaderboard participation.");
  const best = await ctx.db.query("leaderboardAllTimeBests").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  if (best?.excluded) return { score: best.score, status: "excluded" as const };
  // Retried or older uploads are read-only, even while new submissions are paused.
  if (args.score <= (best?.score ?? 0)) return { score: best?.score ?? 0, status: "synced" as const };
  if (!await enabled(ctx)) fail("PAUSED", "Leaderboard submissions are paused. Your saved high score will retry later.");
  await publicLimits.limit(ctx, "bestSyncs", { key: userId, throws: true });
  const value = { userId, score: args.score, negativeScore: -args.score, receivedAt: Date.now(), tieKey: best?.tieKey ?? "", excluded: false };
  if (best) {
    await allTimeScores.delete(ctx, best);
    await ctx.db.replace(best._id, value);
    await allTimeScores.insert(ctx, (await ctx.db.get(best._id))!);
  } else {
    const id = await ctx.db.insert("leaderboardAllTimeBests", value);
    await ctx.db.patch(id, { tieKey: id });
    await allTimeScores.insert(ctx, (await ctx.db.get(id))!);
  }
  return { score: args.score, status: "synced" as const };
} });

export const allTimeBoard = query({ args: {}, returns: v.object({ date: v.literal("all-time"), mode: v.literal("flow"), participants: v.number(), provisional: v.boolean(), enabled: v.boolean(), entries: v.array(entryValue), personal: v.union(entryValue, v.null()) }), handler: async ctx => {
  const userId = await publicUser(ctx);
  const bests = await ctx.db.query("leaderboardAllTimeBests").withIndex("by_excluded_and_score", q => q.eq("excluded", false)).take(100);
  const own = await ctx.db.query("leaderboardAllTimeBests").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  const entries = await Promise.all(bests.map(async best => {
    const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", best.userId)).unique();
    return { nickname: profile?.nickname || "Player", score: best.score, rank: bests.findIndex(row => row.score === best.score) + 1, isMe: best.userId === userId };
  }));
  let personal = entries.find(entry => entry.isMe) ?? null;
  if (!personal && own && !own.excluded) {
    const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
    personal = { nickname: profile?.nickname || "Player", score: own.score, rank: 1 + await allTimeScores.count(ctx, { bounds: { lower: { key: own.score, inclusive: false } } }), isMe: true };
  }
  return { date: "all-time" as const, mode: "flow" as const, participants: await allTimeScores.count(ctx), provisional: false, enabled: await enabled(ctx), entries, personal };
} });

export const excludeAllTime = internalMutation({ args: { userId: v.id("users"), reason: v.string() }, returns: v.null(), handler: async (ctx, args) => {
  if (!args.reason.trim() || args.reason.length > 240) fail("INVALID_ARGUMENT", "Supply a brief exclusion reason.");
  const best = await ctx.db.query("leaderboardAllTimeBests").withIndex("by_userId", q => q.eq("userId", args.userId)).unique();
  if (best && !best.excluded) { await allTimeScores.delete(ctx, best); await ctx.db.patch(best._id, { excluded: true, exclusionReason: args.reason }); }
  return null;
} });

export const profile = query({ args: {}, returns: v.union(profileValue, v.null()), handler: async ctx => {
  const userId = await publicUser(ctx);
  const row = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  return row ? { nickname: row.nickname, participating: row.participating } : null;
} });
export const setProfile = mutation({ args: { nickname: v.string(), participating: v.boolean(), analyticsEligible: v.optional(v.boolean()) }, returns: profileValue, handler: async (ctx, args) => {
  const userId = await publicUser(ctx);
  if (!/^[A-Za-z0-9_]{3,16}$/.test(args.nickname)) fail("INVALID_NICKNAME", "Use 3–16 letters, numbers, or underscores.");
  const row = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  if (row?.nickname === args.nickname && row.participating === args.participating) return { nickname: args.nickname, participating: args.participating };
  if (row?.nickname !== args.nickname) await publicLimits.limit(ctx, "nicknameChanges", { key: userId, throws: true });
  const nicknameKey = args.nickname.toLowerCase();
  const reserved = await ctx.db.query("leaderboardProfiles").withIndex("by_nicknameKey", q => q.eq("nicknameKey", nicknameKey)).unique();
  if (reserved && reserved.userId !== userId) fail("NICKNAME_TAKEN", "That nickname is already taken.");
  const alreadyJoined = row?.hasJoinedLeaderboard ?? row?.participating ?? false;
  const joined = args.participating && !alreadyJoined;
  const value = { userId, nickname: args.nickname, participating: args.participating, nicknameKey, hasJoinedLeaderboard: alreadyJoined || args.participating, updatedAt: Date.now() };
  if (row) await ctx.db.replace(row._id, value); else await ctx.db.insert("leaderboardProfiles", value);
  await enqueue(ctx, userId, joined ? `leaderboard-joined:${userId}` : `leaderboard-profile:${uuid()}`, joined ? "leaderboard_joined" : "leaderboard_profile_updated", Date.now(), { participating: args.participating, source: "server_validation" }, true, args.analyticsEligible === true);
  return { nickname: args.nickname, participating: args.participating };
} });
export const start = mutation({ args: { mode, requestId: v.string(), clientRulesRevision: v.number(), analyticsEligible: v.optional(v.boolean()) }, returns: ticketValue, handler: async (ctx, args) => {
  const userId = await publicUser(ctx);
  if (!/^[A-Za-z0-9_-]{8,128}$/.test(args.requestId)) fail("INVALID_ARGUMENT", "Invalid request ID.");
  if (args.clientRulesRevision !== 1) fail("UPDATE_REQUIRED", "Update Ring Rush to enter leaderboards.");
  const prior = await ctx.db.query("leaderboardRuns").withIndex("by_userId_and_requestId", q => q.eq("userId", userId).eq("requestId", args.requestId)).unique();
  if (prior) { if (prior.mode !== args.mode) fail("CONFLICT", "Request ID already used for another mode."); return ticket(prior); }
  if (!await enabled(ctx)) fail("PAUSED", "Leaderboard submissions are paused. Play continues unranked.");
  const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
  if (!profile?.participating) fail("OPT_IN_REQUIRED", "Choose a nickname and enable leaderboard participation.");
  await publicLimits.limit(ctx, "leaderboardStarts", { key: userId, throws: true });
  const startedAt = Date.now(), uploadDeadline = midnight(startedAt) + DAY + 3_600_000;
  const id = await ctx.db.insert("leaderboardRuns", { ...args, analyticsEligible: args.analyticsEligible === true, userId, date: utcDate(startedAt), status: "open", startedAt, uploadDeadline, purgeAt: uploadDeadline + 7 * DAY });
  return ticket((await ctx.db.get(id))!);
} });
export const status = query({ args: { runId: v.id("leaderboardRuns") }, returns: ticketValue, handler: async (ctx, args) => ticket(await ownedRun(ctx, args.runId)) });
export const submit = mutation({ args: { runId: v.id("leaderboardRuns"), score: v.number(), revives: v.number(), elapsedMs: v.number() }, returns: ticketValue, handler: async (ctx, args) => {
  const run = await ownedRun(ctx, args.runId);
  integer(args.score, 0, 65536, "score"); integer(args.revives, 0, 1312, "revives"); integer(args.elapsedMs, 0, 90_000_000, "elapsed time");
  if (run.status !== "open") {
    if (run.score !== undefined && (run.score !== args.score || run.revives !== args.revives || run.elapsedMs !== args.elapsedMs)) fail("CONFLICT", "Run already submitted with different results.");
    return ticket(run);
  }
  if (Date.now() >= run.uploadDeadline) {
    await ctx.db.patch(run._id, { status: "expired", reason: "The upload deadline passed." });
    await rankingOutcome(ctx, run.userId, run._id, run.mode, "expired");
    return ticket((await ctx.db.get(run._id))!);
  }
  if (!await enabled(ctx)) fail("PAUSED", "Leaderboard submissions are paused. Your result will retry later.");
  await publicLimits.limit(ctx, "leaderboardSubmissions", { key: run.userId, throws: true });
  const earnedRevives = args.score < 20 ? 0 : args.score < 50 ? 1 : args.score < 100 ? 2 : 3 + Math.floor((args.score - 100) / 50);
  const reason = run.clientRulesRevision !== 1 ? "Update Ring Rush to enter leaderboards."
    : args.revives > earnedRevives || args.elapsedMs < args.revives * 3000 || args.score > args.elapsedMs / 10 || args.elapsedMs > Date.now() - run.startedAt + 5000 ? "Run timing is inconsistent with this score." : undefined;
  await ctx.db.patch(run._id, { score: args.score, revives: args.revives, elapsedMs: args.elapsedMs, status: reason ? "rejected" : "accepted", reason });
  if (!reason) {
    const best = await ctx.db.query("leaderboardBests").withIndex("by_date_and_mode_and_userId", q => q.eq("date", run.date).eq("mode", run.mode).eq("userId", run.userId)).unique();
    // An exclusion remains effective for the entire player's board day.
    if (!best || (!best.excluded && args.score > best.score)) {
      const value = { userId: run.userId, mode: run.mode, date: run.date, score: args.score, negativeScore: -args.score, tieKey: best?._id ?? "", achievedAt: Date.now(), runId: run._id, excluded: false, expiresAt: run.uploadDeadline + 365 * DAY };
      if (best) { await publicScores.delete(ctx, best); await ctx.db.replace(best._id, value); await publicScores.insert(ctx, (await ctx.db.get(best._id))!); }
      else { const id = await ctx.db.insert("leaderboardBests", value); await ctx.db.patch(id, { tieKey: id }); await publicScores.insert(ctx, (await ctx.db.get(id))!); }
    }
  }
  await rankingOutcome(ctx, run.userId, run._id, run.mode, reason ? "rejected" : "accepted", args.score);
  return ticket((await ctx.db.get(run._id))!);
} });
export const board = query({ args: { mode, day: v.union(v.literal("today"), v.literal("yesterday")) }, returns: v.object({ date: v.string(), mode, participants: v.number(), provisional: v.boolean(), enabled: v.boolean(), entries: v.array(entryValue), personal: v.union(entryValue, v.null()) }), handler: async (ctx, args) => {
  const userId = await publicUser(ctx), dayStart = midnight(Date.now()) - (args.day === "yesterday" ? DAY : 0), date = utcDate(dayStart), namespace = `${date}/${args.mode}`;
  const bests = await ctx.db.query("leaderboardBests").withIndex("by_date_and_mode_and_excluded_and_negativeScore_and_achievedAt", q => q.eq("date", date).eq("mode", args.mode).eq("excluded", false)).take(100);
  const own = await ctx.db.query("leaderboardBests").withIndex("by_date_and_mode_and_userId", q => q.eq("date", date).eq("mode", args.mode).eq("userId", userId)).unique();
  const entries = await Promise.all(bests.map(async (best) => {
    const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", best.userId)).unique();
    const firstTie = bests.findIndex(row => row.score === best.score);
    return { nickname: profile?.nickname || "Player", score: best.score, rank: firstTie + 1, isMe: best.userId === userId };
  }));
  let personal = entries.find(entry => entry.isMe) ?? null;
  if (!personal && own && !own.excluded) {
    const profile = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", userId)).unique();
    personal = { nickname: profile?.nickname || "Player", score: own.score, rank: 1 + await publicScores.count(ctx, { namespace, bounds: { lower: { key: own.score, inclusive: false } } }), isMe: true };
  }
  return { date, mode: args.mode, participants: await publicScores.count(ctx, { namespace }), provisional: Date.now() < dayStart + DAY + 3_600_000, enabled: await enabled(ctx), entries, personal };
} });
export const setEnabled = internalMutation({ args: { enabled: v.boolean() }, returns: v.null(), handler: async (ctx, args) => {
  const row = await ctx.db.query("leaderboardControls").withIndex("by_key", q => q.eq("key", "public")).unique();
  const value = { key: "public" as const, enabled: args.enabled, updatedAt: Date.now() };
  if (row) await ctx.db.replace(row._id, value); else await ctx.db.insert("leaderboardControls", value);
  return null;
} });
export const exclude = internalMutation({ args: { date: v.string(), mode, userId: v.id("users"), reason: v.string() }, returns: v.null(), handler: async (ctx, args) => {
  if (!args.reason.trim() || args.reason.length > 240) fail("INVALID_ARGUMENT", "Supply a brief exclusion reason.");
  const best = await ctx.db.query("leaderboardBests").withIndex("by_date_and_mode_and_userId", q => q.eq("date", args.date).eq("mode", args.mode).eq("userId", args.userId)).unique();
  if (best && !best.excluded) { await publicScores.delete(ctx, best); await ctx.db.patch(best._id, { excluded: true, exclusionReason: args.reason }); }
  return null;
} });
export const resetNickname = internalMutation({ args: { userId: v.id("users") }, returns: v.null(), handler: async (ctx, args) => {
  const row = await ctx.db.query("leaderboardProfiles").withIndex("by_userId", q => q.eq("userId", args.userId)).unique();
  if (row) await ctx.db.patch(row._id, { nickname: "", nicknameKey: `reset:${row._id}`, participating: false, updatedAt: Date.now() });
  return null;
} });
export const purge = internalMutation({ args: {}, returns: v.number(), handler: async ctx => {
  const runs = await ctx.db.query("leaderboardRuns").withIndex("by_purgeAt", q => q.lte("purgeAt", Date.now())).take(100);
  for (const run of runs) await ctx.db.delete(run._id);
  const bests = await ctx.db.query("leaderboardBests").withIndex("by_expiresAt", q => q.lte("expiresAt", Date.now())).take(100);
  for (const best of bests) { if (!best.excluded) await publicScores.delete(ctx, best); await ctx.db.delete(best._id); }
  if (runs.length === 100 || bests.length === 100) await ctx.scheduler.runAfter(0, internal.leaderboard.purge, {});
  return runs.length + bests.length;
} });

export const expireOpen = internalMutation({ args: {}, returns: v.number(), handler: async ctx => {
  const rows = await ctx.db.query("leaderboardRuns").withIndex("by_status_and_uploadDeadline", q => q.eq("status", "open").lte("uploadDeadline", Date.now())).take(100);
  for (const row of rows) {
    await ctx.db.patch(row._id, { status: "expired", reason: "The upload deadline passed." });
    await rankingOutcome(ctx, row.userId, row._id, row.mode, "expired");
  }
  if (rows.length === 100) await ctx.scheduler.runAfter(0, internal.leaderboard.expireOpen, {});
  return rows.length;
} });

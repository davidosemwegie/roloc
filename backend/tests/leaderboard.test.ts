import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { convexTest } from "convex-test";
import aggregate from "@convex-dev/aggregate/test";
import rateLimiter from "@convex-dev/rate-limiter/test";
import schema from "../convex/schema";
import { api, internal } from "../convex/_generated/api";
import { publicScores, registrationProfile, publicLimits } from "../convex/leaderboardModel";
import { DAY } from "../convex/model";
const modules = import.meta.glob("../convex/**/*.ts");
function setup() { const t = convexTest(schema, modules); aggregate.register(t, "leaderboardScores"); rateLimiter.register(t); return t; }
type Test = ReturnType<typeof setup>;
async function player(t: Test, nickname = "PlayerOne") {
  const id = await t.run(ctx => ctx.db.insert("users", { isAnonymous: true }));
  const client = t.withIdentity({ subject: `${id}|session` });
  await client.mutation(api.leaderboard.setProfile, { nickname, participating: true });
  return { id, client };
}
async function start(client: ReturnType<Test["withIdentity"]>, requestId = "request-0001", mode: "flow" | "rush" = "flow") {
  return client.mutation(api.leaderboard.start, { mode, requestId, clientRulesRevision: 1 });
}
async function record(client: ReturnType<Test["withIdentity"]>, score: number, requestId = "request-0001", mode: "flow" | "rush" = "flow") {
  const run = await start(client, requestId, mode);
  return client.mutation(api.leaderboard.submit, { runId: run.runId, score, revives: 0, elapsedMs: score * 10 });
}
beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(new Date("2026-09-08T12:00:00Z")); vi.stubEnv("RING_RUSH_CLOSED_TEST_CODE", "invite"); });
afterEach(() => { vi.useRealTimers(); vi.unstubAllEnvs(); });
it("defaults paused and keeps public installations outside invited Daily", async () => {
  const t = setup(), p = await player(t);
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "today" })).enabled).toBe(false);
  await expect(start(p.client)).rejects.toThrow("PAUSED");
  await expect(p.client.query(api.daily.myStanding, { challengeId: await t.run(ctx => ctx.db.insert("challenges", { date: "2026-09-08", seed: 1, rulesVersion: 2, variant: "still", opensAt: Date.now(), closesAt: Date.now() + DAY, uploadDeadline: Date.now() + DAY, expiresAt: Date.now() + DAY })) })).rejects.toThrow("CLOSED_TEST");
});
it("reserves case-insensitive nicknames and validates edits and opt-out", async () => {
  const t = setup(), a = await player(t), b = await player(t, "PlayerTwo");
  await expect(b.client.mutation(api.leaderboard.setProfile, { nickname: "playerone", participating: true })).rejects.toThrow("NICKNAME_TAKEN");
  for (const nickname of ["ab", "name with space", "ééé", "a".repeat(17)]) await expect(a.client.mutation(api.leaderboard.setProfile, { nickname, participating: true })).rejects.toThrow("INVALID_NICKNAME");
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const run = await start(a.client);
  await a.client.mutation(api.leaderboard.setProfile, { nickname: "PlayerOne", participating: false });
  await expect(start(a.client, "request-0002")).rejects.toThrow("OPT_IN_REQUIRED");
  expect((await a.client.mutation(api.leaderboard.submit, { runId: run.runId, score: 1, revives: 0, elapsedMs: 100 })).status).toBe("accepted");
});
it("enforces ownership, idempotent starts and submissions, and score plausibility", async () => {
  const t = setup(), a = await player(t), b = await player(t, "PlayerTwo");
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const run = await start(a.client); expect(await start(a.client)).toEqual(run);
  await expect(start(a.client, "request-0001", "rush")).rejects.toThrow("CONFLICT");
  await expect(b.client.query(api.leaderboard.status, { runId: run.runId })).rejects.toThrow("NOT_FOUND");
  const args = { runId: run.runId, score: 5, revives: 0, elapsedMs: 100 };
  await expect(b.client.mutation(api.leaderboard.submit, args)).rejects.toThrow("NOT_FOUND");
  for (const score of [-1, 65537, 1.5, Infinity, NaN]) await expect(a.client.mutation(api.leaderboard.submit, { ...args, score })).rejects.toThrow();
  const result = await a.client.mutation(api.leaderboard.submit, args);
  expect(result.status).toBe("accepted"); expect(await a.client.mutation(api.leaderboard.submit, args)).toEqual(result);
  await expect(a.client.mutation(api.leaderboard.submit, { ...args, score: 6 })).rejects.toThrow("CONFLICT");
  const bad = await start(a.client, "request-0002");
  expect((await a.client.mutation(api.leaderboard.submit, { ...args, runId: bad.runId, score: 100 })).status).toBe("rejected");
  const future = await start(a.client, "request-0003");
  expect((await a.client.mutation(api.leaderboard.submit, { ...args, runId: future.runId, elapsedMs: 10000 })).status).toBe("rejected");
});
it("keeps one best per mode/day, shared competition ranks, and earliest tie order", async () => {
  const t = setup(), a = await player(t), b = await player(t, "PlayerTwo"), c = await player(t, "PlayerThree");
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await record(a.client, 10); vi.setSystemTime(Date.now() + 1); await record(b.client, 10); await record(c.client, 5);
  let board = await c.client.query(api.leaderboard.board, { mode: "flow", day: "today" });
  expect(board.entries.map(e => [e.nickname, e.rank])).toEqual([["PlayerOne", 1], ["PlayerTwo", 1], ["PlayerThree", 3]]);
  expect(board.participants).toBe(3); expect(board.personal?.rank).toBe(3);
  await record(c.client, 20, "request-0002"); await record(c.client, 0, "request-0003");
  board = await c.client.query(api.leaderboard.board, { mode: "flow", day: "today" });
  expect(board.personal?.score).toBe(20); expect(board.participants).toBe(3);
  expect((await c.client.query(api.leaderboard.board, { mode: "rush", day: "today" })).participants).toBe(0);
  expect(Object.keys(board.entries[0]).sort()).toEqual(["isMe", "nickname", "rank", "score"]);
});
it("returns personal rank beyond the top 100 using aggregate counts", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const run = await record(p.client, 1);
  await t.run(async ctx => {
    for (let i = 0; i < 105; i++) {
      const userId = await ctx.db.insert("users", { isAnonymous: true });
      const id = await ctx.db.insert("leaderboardBests", { userId, date: run.date, mode: "flow", score: 2, negativeScore: -2, tieKey: String(i), achievedAt: Date.now(), runId: run.runId, excluded: false, expiresAt: Date.now() + DAY });
      await publicScores.insert(ctx, (await ctx.db.get(id))!);
    }
  });
  const board = await p.client.query(api.leaderboard.board, { mode: "flow", day: "today" });
  expect(board.entries).toHaveLength(100); expect(board.personal?.rank).toBe(106); expect(board.participants).toBe(106);
});
it("uses server UTC start day, accepts just before cutoff and expires at cutoff", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  vi.setSystemTime(new Date("2026-09-08T23:59:59Z")); const run = await start(p.client), late = await start(p.client, "request-0002");
  vi.setSystemTime(run.uploadDeadline - 1);
  expect((await p.client.mutation(api.leaderboard.submit, { runId: run.runId, score: 1, revives: 0, elapsedMs: 100 })).status).toBe("accepted");
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "yesterday" })).provisional).toBe(true);
  vi.setSystemTime(run.uploadDeadline);
  expect((await p.client.query(api.leaderboard.status, { runId: late.runId })).status).toBe("expired");
  expect((await p.client.mutation(api.leaderboard.submit, { runId: late.runId, score: 1, revives: 0, elapsedMs: 100 })).status).toBe("expired");
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "yesterday" })).provisional).toBe(false);
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "today" })).participants).toBe(0);
});
it("supports moderation after cutoff and purges tickets and bests on separate schedules", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const run = await record(p.client, 1);
  vi.setSystemTime(run.uploadDeadline);
  await t.mutation(internal.leaderboard.resetNickname, { userId: p.id });
  expect(await p.client.query(api.leaderboard.profile, {})).toEqual({ nickname: "", participating: false });
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "yesterday" })).entries[0].nickname).toBe("Player");
  await t.mutation(internal.leaderboard.exclude, { date: run.date, mode: "flow", userId: p.id, reason: "Invalid score" });
  expect((await p.client.query(api.leaderboard.board, { mode: "flow", day: "yesterday" })).participants).toBe(0);
  vi.setSystemTime(run.uploadDeadline + 7 * DAY); await t.mutation(internal.leaderboard.purge, {});
  expect(await t.run(ctx => ctx.db.get(run.runId))).toBeNull();
  expect(await t.run(ctx => ctx.db.query("leaderboardBests").take(1))).toHaveLength(1);
  vi.setSystemTime(run.uploadDeadline + 365 * DAY); await t.mutation(internal.leaderboard.purge, {});
  expect(await t.run(ctx => ctx.db.query("leaderboardBests").take(1))).toHaveLength(0);
});

it("only grants a closed-test epoch for a valid invitation", () => {
  expect(registrationProfile(undefined)).toEqual({ isAnonymous: true });
  expect(registrationProfile("")).toEqual({ isAnonymous: true });
  expect(registrationProfile("invite")).toEqual({ isAnonymous: true, closedTestEpoch: "1" });
  for (const code of [null, 123, "wrong"]) expect(() => registrationProfile(code)).toThrow("CLOSED_TEST");
  vi.stubEnv("RING_RUSH_CLOSED_TEST_CODE", "");
  expect(() => registrationProfile("invite")).toThrow("CLOSED_TEST");
});
it("allows exactly one winner in a concurrent case-insensitive nickname reservation", async () => {
  const t = setup(), a = await player(t), b = await player(t, "PlayerTwo");
  const results = await Promise.allSettled([
    a.client.mutation(api.leaderboard.setProfile, { nickname: "Contended", participating: true }),
    b.client.mutation(api.leaderboard.setProfile, { nickname: "contended", participating: true }),
  ]);
  expect(results.filter(r => r.status === "fulfilled")).toHaveLength(1);
  expect(await t.run(ctx => ctx.db.query("leaderboardProfiles").withIndex("by_nicknameKey", q => q.eq("nicknameKey", "contended")).take(2))).toHaveLength(1);
});
it("accepts earned revives and rejects impossible revive counters", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const valid = await start(p.client); vi.setSystemTime(Date.now() + 3000);
  expect((await p.client.mutation(api.leaderboard.submit, { runId: valid.runId, score: 20, revives: 1, elapsedMs: 3000 })).status).toBe("accepted");
  const invalid = await start(p.client, "request-0002");
  expect((await p.client.mutation(api.leaderboard.submit, { runId: invalid.runId, score: 19, revives: 1, elapsedMs: 3000 })).status).toBe("rejected");
});
it("rate limits registrations and nickname changes", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "ChangedOne", participating: true });
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "ChangedTwo", participating: true });
  await expect(p.client.mutation(api.leaderboard.setProfile, { nickname: "ChangedThree", participating: true })).rejects.toThrow();
  await t.run(ctx => publicLimits.limit(ctx, "registrations", { key: "global", count: 100, throws: true }));
  await expect(t.run(ctx => publicLimits.limit(ctx, "registrations", { key: "global", throws: true }))).rejects.toThrow();
});

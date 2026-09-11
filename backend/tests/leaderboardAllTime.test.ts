import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { convexTest } from "convex-test";
import aggregate from "@convex-dev/aggregate/test";
import rateLimiter from "@convex-dev/rate-limiter/test";
import schema from "../convex/schema";
import { api, internal } from "../convex/_generated/api";
import { allTimeScores, publicLimits } from "../convex/leaderboardModel";
import { DAY } from "../convex/model";
const modules = import.meta.glob("../convex/**/*.ts");
function setup() {
  const t = convexTest(schema, modules);
  aggregate.register(t, "leaderboardScores"); aggregate.register(t, "leaderboardAllTimeScores"); rateLimiter.register(t);
  return t;
}
type Test = ReturnType<typeof setup>;
async function player(t: Test, nickname = "PlayerOne") {
  const id = await t.run(ctx => ctx.db.insert("users", { isAnonymous: true }));
  const client = t.withIdentity({ subject: `${id}|session` });
  await client.mutation(api.leaderboard.setProfile, { nickname, participating: true });
  return { id, client };
}
beforeEach(() => { vi.useFakeTimers(); vi.setSystemTime(new Date("2026-09-11T12:00:00Z")); });
afterEach(() => vi.useRealTimers());

it("requires an authenticated opted-in nickname and validates local scores", async () => {
  const t = setup();
  await expect(t.mutation(api.leaderboard.syncBest, { score: 10 })).rejects.toThrow("UNAUTHENTICATED");
  await expect(t.query(api.leaderboard.allTimeBoard, {})).rejects.toThrow("UNAUTHENTICATED");
  const id = await t.run(ctx => ctx.db.insert("users", { isAnonymous: true }));
  const client = t.withIdentity({ subject: `${id}|session` });
  await expect(client.mutation(api.leaderboard.syncBest, { score: 10 })).rejects.toThrow("OPT_IN_REQUIRED");
  await client.mutation(api.leaderboard.setProfile, { nickname: "PlayerOne", participating: false });
  await expect(client.mutation(api.leaderboard.syncBest, { score: 10 })).rejects.toThrow("OPT_IN_REQUIRED");
  await client.mutation(api.leaderboard.setProfile, { nickname: "PlayerOne", participating: true });
  for (const score of [-1, 65537, 1.5, Infinity, NaN])
    await expect(client.mutation(api.leaderboard.syncBest, { score })).rejects.toThrow();
  await t.mutation(internal.leaderboard.resetNickname, { userId: id });
  await expect(client.mutation(api.leaderboard.syncBest, { score: 10 })).rejects.toThrow("OPT_IN_REQUIRED");
});

it("keeps a monotonic best across offline retries, pause and UTC rollover without adding daily runs", async () => {
  const t = setup(), p = await player(t);
  await expect(p.client.mutation(api.leaderboard.syncBest, { score: 42 })).rejects.toThrow("PAUSED");
  expect(await p.client.mutation(api.leaderboard.syncBest, { score: 0 })).toEqual({ score: 0, status: "synced" });
  expect((await p.client.query(api.leaderboard.allTimeBoard, {})).participants).toBe(0);
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  expect(await p.client.mutation(api.leaderboard.syncBest, { score: 42 })).toEqual({ score: 42, status: "synced" });
  const original = await t.run(ctx => ctx.db.query("leaderboardAllTimeBests").withIndex("by_userId", q => q.eq("userId", p.id)).unique());
  vi.setSystemTime(Date.now() + DAY);
  await t.mutation(internal.leaderboard.setEnabled, { enabled: false });
  for (const score of [42, 5, 0]) expect(await p.client.mutation(api.leaderboard.syncBest, { score })).toEqual({ score: 42, status: "synced" });
  expect(await t.run(ctx => ctx.db.get(original!._id))).toEqual(original);
  await expect(p.client.mutation(api.leaderboard.syncBest, { score: 43 })).rejects.toThrow("PAUSED");
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await p.client.mutation(api.leaderboard.syncBest, { score: 43 });
  expect(await p.client.query(api.leaderboard.allTimeBoard, {})).toMatchObject({ date: "all-time", mode: "flow", provisional: false, participants: 1, personal: { score: 43, rank: 1 } });
  for (const day of ["today", "yesterday"] as const)
    expect((await p.client.query(api.leaderboard.board, { mode: "flow", day })).participants).toBe(0);
  expect(await t.run(ctx => ctx.db.query("leaderboardRuns").take(1))).toHaveLength(0);
  expect(await t.run(ctx => ctx.db.query("runHistory").take(1))).toHaveLength(0);
  vi.setSystemTime(Date.now() + 366 * DAY);
  await t.mutation(internal.leaderboard.purge, {});
  expect((await p.client.query(api.leaderboard.allTimeBoard, {})).personal?.score).toBe(43);
});

it("merges concurrent first uploads and improvements into one maximum", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await Promise.all([10, 20, 15].map(score => p.client.mutation(api.leaderboard.syncBest, { score })));
  await Promise.all([40, 20, 30].map(score => p.client.mutation(api.leaderboard.syncBest, { score })));
  expect((await p.client.query(api.leaderboard.allTimeBoard, {}))).toMatchObject({ participants: 1, personal: { score: 40 } });
  expect(await t.run(ctx => ctx.db.query("leaderboardAllTimeBests").withIndex("by_userId", q => q.eq("userId", p.id)).take(2))).toHaveLength(1);
});

it("uses shared competition ranks and receipt order, with personal ranks beyond the top 100", async () => {
  const t = setup(), a = await player(t), b = await player(t, "PlayerTwo"), c = await player(t, "PlayerThree");
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await a.client.mutation(api.leaderboard.syncBest, { score: 10 });
  vi.setSystemTime(Date.now() + 1); await b.client.mutation(api.leaderboard.syncBest, { score: 10 });
  await c.client.mutation(api.leaderboard.syncBest, { score: 1 });
  let board = await c.client.query(api.leaderboard.allTimeBoard, {});
  expect(board.entries.map(e => [e.nickname, e.rank])).toEqual([["PlayerOne", 1], ["PlayerTwo", 1], ["PlayerThree", 3]]);
  expect(Object.keys(board.entries[0]).sort()).toEqual(["isMe", "nickname", "rank", "score"]);
  await t.run(async ctx => {
    for (let i = 0; i < 100; i++) {
      const userId = await ctx.db.insert("users", { isAnonymous: true });
      const id = await ctx.db.insert("leaderboardAllTimeBests", { userId, score: 2, negativeScore: -2, receivedAt: Date.now(), tieKey: String(i), excluded: false });
      await allTimeScores.insert(ctx, (await ctx.db.get(id))!);
    }
  });
  board = await c.client.query(api.leaderboard.allTimeBoard, {});
  expect(board.entries).toHaveLength(100); expect(board.participants).toBe(103); expect(board.personal?.rank).toBe(103);
  expect(board.entries[99].rank).toBe(3);
});

it("preserves exclusion across higher uploads, opt-out, nickname reset and rejoining", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await p.client.mutation(api.leaderboard.syncBest, { score: 10 });
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "PlayerOne", participating: false });
  expect((await p.client.query(api.leaderboard.allTimeBoard, {})).personal?.score).toBe(10);
  await expect(p.client.mutation(api.leaderboard.syncBest, { score: 20 })).rejects.toThrow("OPT_IN_REQUIRED");
  await t.mutation(internal.leaderboard.excludeAllTime, { userId: p.id, reason: "Invalid score" });
  await t.mutation(internal.leaderboard.resetNickname, { userId: p.id });
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "Renamed", participating: true });
  expect(await p.client.mutation(api.leaderboard.syncBest, { score: 20 })).toEqual({ score: 10, status: "excluded" });
  await t.mutation(internal.leaderboard.excludeAllTime, { userId: p.id, reason: "Still excluded" });
  expect(await p.client.query(api.leaderboard.allTimeBoard, {})).toMatchObject({ participants: 0, entries: [], personal: null });
});

it("rate limits increases without charging retries or blocking existing board reads", async () => {
  const t = setup(), p = await player(t); await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  await p.client.mutation(api.leaderboard.syncBest, { score: 10 });
  await t.run(ctx => publicLimits.limit(ctx, "bestSyncs", { key: p.id, count: 19, throws: true }));
  await expect(p.client.mutation(api.leaderboard.syncBest, { score: 11 })).rejects.toThrow();
  expect(await p.client.mutation(api.leaderboard.syncBest, { score: 10 })).toEqual({ score: 10, status: "synced" });
  expect((await p.client.query(api.leaderboard.allTimeBoard, {})).personal?.score).toBe(10);
});

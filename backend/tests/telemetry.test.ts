import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { convexTest } from "convex-test";
import aggregate from "@convex-dev/aggregate/test";
import rateLimiter from "@convex-dev/rate-limiter/test";
import schema from "../convex/schema";
import { api, internal } from "../convex/_generated/api";
import { rankingOutcome } from "../convex/telemetryModel";
import { DAY } from "../convex/model";
const modules = import.meta.glob("../convex/**/*.ts");
function setup() { const t = convexTest(schema, modules); aggregate.register(t, "leaderboardScores"); rateLimiter.register(t); return t; }
type Test = ReturnType<typeof setup>;
async function player(t: Test) {
  const id = await t.run(ctx => ctx.db.insert("users", { isAnonymous: true }));
  return { id, client: t.withIdentity({ subject: `${id}|session` }) };
}
function summary() { return { analyticsEligible: true, clientRunId: "run-00000001", mode: "flow" as const, startedAt: Date.now() - 1000, endedAt: Date.now(), score: 5, revives: 0, elapsedMs: 1000, status: "completed" as const, leaderboardRunId: "", dailyAttemptId: "" }; }
function event() { return { analyticsEligible: true, eventId: "event-00000001", name: "game_opened" as const, occurredAt: Date.now(), sessionId: "session-00000001", mode: "", clientRunId: "" }; }
beforeEach(() => {
  vi.useFakeTimers(); vi.setSystemTime(new Date("2026-09-08T12:00:00Z"));
  vi.stubEnv("POSTHOG_PROJECT_TOKEN", "phc_test_only"); vi.stubEnv("POSTHOG_HOST", "https://us.i.posthog.com");
  vi.stubEnv("CONVEX_SITE_URL", "https://test.convex.site"); vi.stubEnv("RING_RUSH_ENVIRONMENT", "beta");
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllEnvs(); vi.unstubAllGlobals(); });
it("shares one canonical identity with auth and defaults analytics enabled", async () => {
  const t = setup(), p = await player(t);
  expect(await p.client.query(api.telemetry.identity, {})).toEqual({ playerId: `https://test.convex.site:${p.id}`, analyticsEnabled: true });
  await expect(t.query(api.telemetry.identity, {})).rejects.toThrow("UNAUTHENTICATED");
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true });
  expect((await p.client.query(api.telemetry.identity, {})).playerId).toBe(`https://test.convex.site:${p.id}`);
});
it("stores offline final summaries once and emits one start and completion", async () => {
  const t = setup(), p = await player(t), run = summary();
  expect(await p.client.mutation(api.telemetry.recordRuns, { runs: [run] })).toEqual({ recorded: 1, duplicates: 0 });
  expect(await p.client.mutation(api.telemetry.recordRuns, { runs: [run] })).toEqual({ recorded: 0, duplicates: 1 });
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  expect((await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).map(e => e.name).sort()).toEqual(["run_completed", "run_started"]);
  await expect(p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, score: 6 }] })).rejects.toThrow("CONFLICT");
});
it("supports monotonic start-to-final and ignores delayed start retries", async () => {
  const t = setup(), p = await player(t), completed = summary();
  const started = { ...completed, status: "started" as const, endedAt: 0, score: 0, revives: 0, elapsedMs: 0 };
  await p.client.mutation(api.telemetry.recordRuns, { runs: [started] });
  await p.client.mutation(api.telemetry.recordRuns, { runs: [completed] });
  expect(await p.client.mutation(api.telemetry.recordRuns, { runs: [started] })).toEqual({ recorded: 0, duplicates: 1 });
  expect((await t.run(ctx => ctx.db.query("runHistory").first()))?.score).toBe(5);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(2);
});
it("bounds run/event input and verifies linked ticket ownership", async () => {
  const t = setup(), a = await player(t), b = await player(t), run = summary();
  for (const invalid of [{ ...run, score: NaN }, { ...run, score: 65537 }, { ...run, startedAt: Date.now() - 8 * DAY }, { ...run, endedAt: Date.now() + 300001 }, { ...run, elapsedMs: 90000001 }]) await expect(a.client.mutation(api.telemetry.recordRuns, { runs: [invalid] })).rejects.toThrow("INVALID_ARGUMENT");
  await expect(a.client.mutation(api.telemetry.recordRuns, { runs: Array(21).fill(run) })).rejects.toThrow("INVALID_ARGUMENT");
  await expect(a.client.mutation(api.telemetry.capture, { ...event(), mode: "arbitrary" })).rejects.toThrow("INVALID_ARGUMENT");
  await a.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true });
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const ticket = await a.client.mutation(api.leaderboard.start, { mode: "flow", requestId: "ticket-00000001", clientRulesRevision: 1, analyticsEligible: true });
  await expect(b.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, leaderboardRunId: ticket.runId }] })).rejects.toThrow("NOT_FOUND");
  await a.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, leaderboardRunId: ticket.runId }] });
  await expect(a.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, clientRunId: "run-00000002", leaderboardRunId: ticket.runId }] })).rejects.toThrow("CONFLICT");
});
it("records history while opted out and prevents backfill after re-enabling", async () => {
  const t = setup(), p = await player(t), run = summary(), e = event();
  await p.client.mutation(api.telemetry.setAnalyticsEnabled, { enabled: false });
  await p.client.mutation(api.telemetry.recordRuns, { runs: [run] }); await p.client.mutation(api.telemetry.capture, e);
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  await p.client.mutation(api.telemetry.setAnalyticsEnabled, { enabled: true });
  await p.client.mutation(api.telemetry.recordRuns, { runs: [run] }); await p.client.mutation(api.telemetry.capture, e);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("discards leased old-consent events even if player re-enables analytics", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.telemetry.capture, event());
  const claimed = await t.mutation(internal.telemetryDelivery.claim, {}); expect(claimed).toHaveLength(1);
  await p.client.mutation(api.telemetry.setAnalyticsEnabled, { enabled: false });
  await p.client.mutation(api.telemetry.setAnalyticsEnabled, { enabled: true });
  expect(await t.query(internal.telemetryDelivery.eligible, { events: claimed })).toHaveLength(0);
  await t.mutation(internal.telemetryDelivery.purgeOptedOut, { userId: p.id, beforeRevision: 1 });
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("survives failed delivery and expired leases with stable UUID and timestamp", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.telemetry.capture, event());
  const first = await t.mutation(internal.telemetryDelivery.claim, {});
  expect(await t.mutation(internal.telemetryDelivery.claim, {})).toHaveLength(0);
  vi.setSystemTime(Date.now() + 120000);
  const retry = await t.mutation(internal.telemetryDelivery.claim, {});
  expect(retry[0].uuid).toBe(first[0].uuid); expect(retry[0].occurredAt).toBe(first[0].occurredAt); expect(retry[0].distinctId).toBe(first[0].distinctId);
  await t.mutation(internal.telemetryDelivery.acknowledge, { events: first, success: true });
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(1);
  await t.mutation(internal.telemetryDelivery.acknowledge, { events: retry, success: false });
  expect(await t.mutation(internal.telemetryDelivery.claim, {})).toHaveLength(0);
  vi.setSystemTime(Date.now() + 3600000);
  const final = await t.mutation(internal.telemetryDelivery.claim, {});
  await t.mutation(internal.telemetryDelivery.acknowledge, { events: final, success: true });
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("sends only allowlisted properties and deletes delivered events", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.telemetry.recordRuns, { runs: [summary()] });
  const fetchMock = vi.fn().mockResolvedValue(new Response("{}", { status: 200 })); vi.stubGlobal("fetch", fetchMock);
  expect(await t.action(internal.telemetryDelivery.deliver, {})).toBe(2);
  const payload = JSON.parse(fetchMock.mock.calls[0][1].body);
  expect(fetchMock.mock.calls[0][0]).toBe("https://us.i.posthog.com/batch/");
  expect(payload.batch[1].properties).toMatchObject({ elapsed_ms: 1000, source: "client_summary", environment: "beta", $geoip_disable: true });
  expect(payload.batch[1].properties).not.toHaveProperty("nickname"); expect(payload.batch[1].properties).not.toHaveProperty("userId");
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("handles missing capture configuration without losing history", async () => {
  const t = setup(), p = await player(t); vi.stubEnv("POSTHOG_PROJECT_TOKEN", "");
  await p.client.mutation(api.telemetry.recordRuns, { runs: [summary()] });
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  expect(await t.action(internal.telemetryDelivery.deliver, {})).toBe(0);
});
it("persists summaries while purging temporary payloads and supports explicit deletion", async () => {
  const t = setup(), p = await player(t); await p.client.mutation(api.telemetry.recordRuns, { runs: [summary()] });
  vi.setSystemTime(Date.now() + 8 * DAY); await t.mutation(internal.telemetryDelivery.purge, {});
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  expect(await t.run(ctx => ctx.db.query("analyticsReceipts").take(10))).toHaveLength(0);
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  await t.mutation(internal.telemetryDelivery.deletePlayerHistory, { userId: p.id });
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(0);
});
it("records authoritative outcomes once and never lets summary scores alter ranking", async () => {
  const t = setup(), p = await player(t); await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true });
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const ticket = await p.client.mutation(api.leaderboard.start, { mode: "flow", requestId: "ticket-00000001", clientRulesRevision: 1, analyticsEligible: true });
  await p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...summary(), leaderboardRunId: ticket.runId, score: 100 }] });
  const args = { runId: ticket.runId, score: 5, revives: 0, elapsedMs: 1000 };
  await p.client.mutation(api.leaderboard.submit, args); await p.client.mutation(api.leaderboard.submit, args);
  const history = (await t.run(ctx => ctx.db.query("runHistory").first()))!;
  expect(history.score).toBe(100); expect(history.rankedScore).toBe(5); expect(history.rankingStatus).toBe("accepted");
  const events = await t.run(ctx => ctx.db.query("analyticsOutbox").take(20));
  expect(events.filter(e => e.name === "leaderboard_score_accepted")).toHaveLength(1);
});
it("keeps Daily outcome events idempotent and ignores repeated profile updates", async () => {
  const t = setup(), p = await player(t);
  const challengeId = await t.run(ctx => ctx.db.insert("challenges", { date: "2026-09-08", seed: 1, rulesVersion: 2, variant: "still", opensAt: Date.now(), closesAt: Date.now() + DAY, uploadDeadline: Date.now() + DAY, expiresAt: Date.now() + DAY }));
  const attemptId = await t.run(ctx => ctx.db.insert("attempts", { userId: p.id, challengeId, requestId: "daily-0000001", analyticsEligible: true, status: "rejected", startedAt: Date.now(), uploadDeadline: Date.now() + DAY, nextChunkIndex: 0, eventCount: 0, purgeAt: Date.now() + DAY }));
  await t.run(ctx => rankingOutcome(ctx, p.id, attemptId, "daily", "rejected")); await t.run(ctx => rankingOutcome(ctx, p.id, attemptId, "daily", "rejected"));
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true }); await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true });
  const events = await t.run(ctx => ctx.db.query("analyticsOutbox").take(20));
  expect(events.filter(e => e.name === "leaderboard_score_rejected")).toHaveLength(1); expect(events.filter(e => e.name === "leaderboard_joined")).toHaveLength(1);
});

it("acknowledges exact old retries after linked tickets are purged", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true, analyticsEligible: true });
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const ticket = await p.client.mutation(api.leaderboard.start, { mode: "flow", requestId: "ticket-00000001", clientRulesRevision: 1, analyticsEligible: true });
  const run = { ...summary(), leaderboardRunId: ticket.runId };
  await p.client.mutation(api.telemetry.recordRuns, { runs: [run] });
  vi.setSystemTime(Date.now() + 9 * DAY);
  await t.mutation(internal.leaderboard.purge, {});
  expect(await p.client.mutation(api.telemetry.recordRuns, { runs: [run] })).toEqual({ recorded: 0, duplicates: 1 });
  await expect(p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, score: 99 }] })).rejects.toThrow("CONFLICT");
  const stranger = await player(t);
  await expect(stranger.client.mutation(api.telemetry.recordRuns, { runs: [run] })).rejects.toThrow("INVALID_ARGUMENT");
});

it("preserves disabled-at-occurrence consent for offline summaries after re-enabling", async () => {
  const t = setup(), p = await player(t);
  const run = { ...summary(), analyticsEnabled: false };
  // Current server preference is enabled, but this offline run happened disabled.
  await p.client.mutation(api.telemetry.recordRuns, { runs: [run] });
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  expect(await t.run(ctx => ctx.db.query("analyticsReceipts").take(10))).toHaveLength(2);
  await p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...run, analyticsEnabled: true }] });
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("accepts SaveService installation:counter run IDs without loosening event IDs", async () => {
  const t = setup(), p = await player(t), clientRunId = "e9c0a610-c6c9-45bd-9bea-3c55c430c5df:123";
  expect(await p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...summary(), clientRunId }] })).toEqual({ recorded: 1, duplicates: 0 });
  await p.client.mutation(api.telemetry.capture, { ...event(), clientRunId });
  await expect(p.client.mutation(api.telemetry.capture, { ...event(), eventId: "invalid:event" })).rejects.toThrow("INVALID_ARGUMENT");
});
it("requires distribution environment even with a configured project token", async () => {
  for (const environment of ["development", "", "staging"]) {
    vi.stubEnv("RING_RUSH_ENVIRONMENT", environment);
    const t = setup(), p = await player(t);
    await p.client.mutation(api.telemetry.recordRuns, { runs: [summary()] });
    await p.client.mutation(api.telemetry.capture, event());
    expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
    expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
    expect(await t.action(internal.telemetryDelivery.deliver, {})).toBe(0);
  }
});
it("keeps legacy and development-build clients silent on beta while retaining history", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.telemetry.recordRuns, { runs: [{ ...summary(), analyticsEligible: undefined }] });
  await p.client.mutation(api.telemetry.capture, { ...event(), analyticsEligible: false });
  const profile = await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true });
  expect(profile).toEqual({ nickname: "Example", participating: true });
  expect(await t.run(ctx => ctx.db.query("runHistory").take(10))).toHaveLength(1);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "ExampleNew", participating: true, analyticsEligible: true });
  const events = await t.run(ctx => ctx.db.query("analyticsOutbox").take(10));
  expect(events.map(e => e.name)).toEqual(["leaderboard_profile_updated"]);
  expect(await t.run(ctx => ctx.db.query("leaderboardProfiles").first())).not.toHaveProperty("analyticsEligible");
});
it("freezes ticket eligibility at creation and ignores retry upgrades", async () => {
  const t = setup(), p = await player(t);
  await p.client.mutation(api.leaderboard.setProfile, { nickname: "Example", participating: true });
  await t.mutation(internal.leaderboard.setEnabled, { enabled: true });
  const args = { mode: "flow" as const, requestId: "ticket-00000001", clientRulesRevision: 1 };
  const ticket = await p.client.mutation(api.leaderboard.start, args);
  await p.client.mutation(api.leaderboard.start, { ...args, analyticsEligible: true });
  await p.client.mutation(api.leaderboard.submit, { runId: ticket.runId, score: 1, revives: 0, elapsedMs: 100 });
  expect((await t.run(ctx => ctx.db.get(ticket.runId)))?.analyticsEligible).toBe(false);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
});
it("uses Daily ticket eligibility without requiring a history record", async () => {
  const t = setup(), p = await player(t);
  vi.stubEnv("RING_RUSH_CLOSED_TEST_CODE", "invite"); vi.stubEnv("RING_RUSH_CLOSED_TEST_EPOCH", "1");
  await t.run(ctx => ctx.db.patch(p.id, { closedTestEpoch: "1" }));
  const challengeId = await t.run(ctx => ctx.db.insert("challenges", { date: "2026-09-08", seed: 1, rulesVersion: 2, variant: "still", opensAt: Date.now(), closesAt: Date.now() + DAY, uploadDeadline: Date.now() + DAY, expiresAt: Date.now() + DAY }));
  const args = { challengeId, requestId: "daily-gate-0001", clientRulesRevision: 3 };
  const legacy = await p.client.mutation(api.daily.createAttempt, args);
  await p.client.mutation(api.daily.createAttempt, { ...args, analyticsEligible: true });
  await t.run(ctx => rankingOutcome(ctx, p.id, legacy.attemptId, "daily", "rejected"));
  const eligible = await p.client.mutation(api.daily.createAttempt, { ...args, requestId: "daily-gate-0002", analyticsEligible: true });
  await t.run(ctx => rankingOutcome(ctx, p.id, eligible.attemptId, "daily", "accepted", 1));
  expect((await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).map(e => e.name)).toEqual(["leaderboard_score_accepted"]);
});
it("drops legacy/leased rows and preserves originating environment during retries", async () => {
  const t = setup(), p = await player(t); await p.client.mutation(api.telemetry.capture, event());
  const claimed = await t.mutation(internal.telemetryDelivery.claim, {});
  expect(claimed[0].environment).toBe("beta");
  vi.stubEnv("RING_RUSH_ENVIRONMENT", "production");
  expect((await t.query(internal.telemetryDelivery.eligible, { events: claimed }))[0].environment).toBe("beta");
  await t.run(ctx => ctx.db.patch(claimed[0].id, { analyticsEligible: undefined }));
  expect(await t.query(internal.telemetryDelivery.eligible, { events: claimed })).toHaveLength(0);
  vi.setSystemTime(Date.now() + 120000);
  expect(await t.mutation(internal.telemetryDelivery.claim, {})).toHaveLength(0);
  expect(await t.run(ctx => ctx.db.query("analyticsOutbox").take(10))).toHaveLength(0);
  await p.client.mutation(api.telemetry.capture, { ...event(), eventId: "event-00000002" });
  vi.stubEnv("RING_RUSH_ENVIRONMENT", "development");
  expect(await t.mutation(internal.telemetryDelivery.discardIneligiblePending, {})).toBe(1);
});

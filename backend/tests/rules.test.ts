import { describe, expect, it } from "vitest";
import fixture from "../../Assets/Tests/EditMode/Core/DailyFixtures.json";
import { advance, initialReplay, initialRules, nextUInt, PERFECT_RADIUS, replayEvents, ringCenter, RING_RADIUS, within, type TraceEvent } from "../convex/rules";

function drop(state: ReturnType<typeof initialReplay>, elapsedMs = 400): TraceEvent {
  const [xQ, yQ] = ringCenter(state.rules, state.rules.active, elapsedMs);
  return { kind: "drop", round: state.rules.score, tMs: state.roundStartTMs + state.roundPausedMs + elapsedMs, elapsedMs, color: state.rules.active, xQ, yQ };
}
describe("Daily v1 compatibility", () => {
  it("matches all 101 C# fixture rounds including drift, repeated colors, rotation, and shuffles", () => {
    const state = initialRules(fixture.seed, "lively");
    const modes = ["Steady", "Floating", "Drifting", "Breather", "Rotation"], phases = ["Calm", "Challenge", "Recovery"];
    for (const row of fixture.rounds) {
      expect(state.score).toBe(row.round); expect(state.active).toBe(row.activeColor);
      expect(state.rings).toEqual(row.ringOrder); expect(state.pucks).toEqual(row.puckOrder);
      expect(state.mode).toBe(modes[row.flowMode]); expect(state.phase).toBe(phases[row.rhythmPhase]);
      expect(state.rotationSteps).toBe(row.rotationSteps); expect(state.durationMs).toBe(row.durationMs); expect(state.transitionMs).toBe(row.transitionMs);
      for (let color = 0; color < 4; color++) expect(ringCenter(state, color, fixture.elapsedMs)).toEqual([row.rings[color].X, row.rings[color].Y]);
      advance(state);
    }
  });
  it("restarts seed zero and unsigned seeds deterministically", () => {
    for (const seed of [0, 1, 42, 0xFFFFFFFF]) {
      const a = initialRules(seed, "lively"), b = initialRules(seed, "lively");
      for (let i = 0; i < 500; i++) { advance(a); advance(b); expect(a).toEqual(b); }
    }
    expect(nextUInt({ rng: 0x6D2B79F5 })).toBe(nextUInt({ rng: 0x6D2B79F5 }));
  });
  it("keeps Still and Lively sequences identical while Still removes drift", () => {
    const a = initialRules(42, "lively"), b = initialRules(42, "still");
    let driftSeen = false;
    for (let i = 0; i < 100; i++) {
      expect(a.rings).toEqual(b.rings); expect(a.pucks).toEqual(b.pucks); expect(a.mode).toEqual(b.mode);
      if (a.mode === "Drifting") { driftSeen = true; expect(ringCenter(a, a.active, 1370)).not.toEqual(ringCenter(b, b.active, 1370)); }
      advance(a); advance(b);
    }
    expect(driftSeen).toBe(true);
  });
  it("includes exact outer and Perfect boundaries without cosmetic scale", () => {
    expect(within(RING_RADIUS, 0, [0, 0], RING_RADIUS)).toBe(true);
    expect(within(RING_RADIUS + 1, 0, [0, 0], RING_RADIUS)).toBe(false);
    expect(within(PERFECT_RADIUS, 0, [0, 0], PERFECT_RADIUS)).toBe(true);
    expect(within(PERFECT_RADIUS + 1, 0, [0, 0], PERFECT_RADIUS)).toBe(false);
  });
});
describe("Trace legality", () => {
  it("replays a long trace through checkpoints and derives earned score", () => {
    const state = initialReplay(42, "lively");
    for (let i = 0; i < 500; i++) expect(replayEvents(state, [drop(state)])).toBeNull();
    const end = { ...drop(state), kind: "abandon" as const, color: -1, xQ: 0, yQ: 0 };
    expect(replayEvents(state, [end])).toBeNull(); expect(state.rules.score).toBe(500); expect(state.perfects).toBe(500); expect(state.terminal).toBe(true);
  });
  it("ordinary valid drops keep scoring without counting Perfect", () => {
    const state = initialReplay(42, "still"), event = drop(state); event.xQ += PERFECT_RADIUS + 1;
    expect(replayEvents(state, [event])).toBeNull(); expect(state.rules.score).toBe(1); expect(state.perfects).toBe(0);
  });
  it("a wrong ring ends the run without awarding another match", () => {
    const state = initialReplay(42, "lively"), event = drop(state); event.xQ = 0; event.yQ = 0;
    expect(replayEvents(state, [event])).toBeNull(); expect(state.terminal).toBe(true); expect(state.rules.score).toBe(0);
    expect(replayEvents(state, [event])).toContain("after the run ended");
  });
  it("rejects an inactive puck, expired drop, early timeout, and skipped round", () => {
    for (const edit of [ {color: 1}, {elapsedMs: 3000, tMs: 3000}, {kind: "timeout" as const}, {round: 3} ]) {
      const state = initialReplay(42, "still"); expect(replayEvents(state, [{...drop(state), ...edit}])).not.toBeNull();
    }
  });
  it("rejects forged speed and noninteger coordinates", () => {
    const state = initialReplay(42, "still");
    expect(replayEvents(state, [{ ...drop(state), tMs: 1 }])).toContain("faster");
    expect(replayEvents(initialReplay(42, "still"), [{ ...drop(state), xQ: .5 }])).toContain("invalid");
  });
  it("requires an explicit resume and excludes paused time", () => {
    const state = initialReplay(42, "still");
    const pause: TraceEvent = { kind: "pause", round: 0, tMs: 100, elapsedMs: 100, color: -1, xQ: 0, yQ: 0 };
    expect(replayEvents(state, [pause, {...pause, kind:"resume", tMs:1100}])).toBeNull();
    expect(replayEvents(state, [drop(state, 400)])).toBeNull(); expect(state.rules.score).toBe(1);
    const other = initialReplay(42, "still"); replayEvents(other, [pause]); expect(replayEvents(other, [drop(other)])).toContain("paused");
  });
  it("supports a pause during board transition, and abandoning from pause", () => {
    const state = initialReplay(42, "still"); replayEvents(state, [drop(state)]);
    const pause: TraceEvent = {kind:"pause",round:1,tMs:410,elapsedMs:0,color:-1,xQ:0,yQ:0};
    expect(replayEvents(state, [pause, {...pause,kind:"resume",tMs:1410}, drop({...state, roundPausedMs:1000}, 300)])).toBeNull();
    const paused = initialReplay(42,"still");
    expect(replayEvents(paused,[{...pause,round:0}, {...pause,round:0,kind:"abandon",tMs:500}])).toBeNull(); expect(paused.terminal).toBe(true);
  });
});

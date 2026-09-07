import { describe, expect, it } from "vitest";
import fixture from "../../Assets/Tests/EditMode/Core/ReviveFixtures.json";
import { initialReplay, replayEvents, ringCenter, type ReplayState, type TraceEvent, type Variant } from "../convex/rules";

function move(state: ReplayState, elapsedMs: number): TraceEvent {
  const [xQ, yQ] = ringCenter(state.rules, state.rules.active, elapsedMs);
  return {kind:"drop", round:state.rules.score, tMs:state.roundStartTMs + elapsedMs,
    elapsedMs, color:state.rules.active, xQ, yQ};
}
function sameBoard(state: ReplayState, uninterrupted: ReplayState) {
  // Includes RNG, active color, all layout/variation state, duration and perfect count.
  expect(state.rules).toEqual(uninterrupted.rules);
  expect(state.perfects).toBe(uninterrupted.perfects);
  for (let color = 0; color < 4; color++)
    expect(ringCenter(state.rules, color, fixture.elapsedMs)).toEqual(ringCenter(uninterrupted.rules, color, fixture.elapsedMs));
}

describe("Shared client/server revive action fixtures", () => {
  for (const variant of fixture.variants)
    for (const scenario of fixture.scenarios)
      it(`${variant} / ${scenario.name}`, () => {
        const state = initialReplay(fixture.seed, variant as Variant, fixture.rulesVersion);
        const uninterrupted = initialReplay(fixture.seed, variant as Variant, fixture.rulesVersion);
        for (const step of scenario.steps) {
          let event: TraceEvent | undefined;
          switch (step.action) {
            case "match":
              while (state.rules.score < step.score) {
                expect(replayEvents(state, [move(state, fixture.elapsedMs)])).toBeNull();
                expect(replayEvents(uninterrupted, [move(uninterrupted, fixture.elapsedMs)])).toBeNull();
                sameBoard(state, uninterrupted);
              }
              break;
            case "miss": event = {...move(state, fixture.elapsedMs), xQ:0, yQ:0}; break;
            case "inactive": event = {...move(state, fixture.elapsedMs), color:(state.rules.active + 1) % 4}; break;
            case "timeout": event = {...move(state, state.rules.durationMs), kind:"timeout"}; break;
            case "revive":
              event = {kind:"revive", round:state.rules.score, tMs:state.lastTMs + 30000, elapsedMs:0, color:-1, xQ:0, yQ:0};
              break;
            case "abandon":
              event = {kind:"abandon", round:state.rules.score, tMs:state.lastTMs + 1000, elapsedMs:state.lastElapsedMs, color:-1, xQ:0, yQ:0};
              break;
            default: throw new Error(`Unknown fixture action: ${step.action}`);
          }
          if (event) expect(replayEvents(state, [event])).toBeNull();
          if (event?.kind === "revive") {
            expect(state.roundStartTMs).toBe(event.tMs + fixture.reviveTransitionMs);
            expect(state.lastElapsedMs).toBe(0);
          }
          expect(state.rules.score).toBe(step.score);
          expect(state.revivesAvailable).toBe(step.bank);
          expect(state.terminal ? "ended" : state.awaitingRevive ? "failed" : "playing").toBe(step.state);
          sameBoard(state, uninterrupted);
        }
      });
});

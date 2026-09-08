/** Daily protocols v1/v2. Keep synchronized with Assets/Scripts/Core/DailyRules.cs. */
export type Variant = "lively" | "still";
export type FlowMode = "Steady" | "Floating" | "Drifting" | "Breather" | "Rotation";
export type TraceEvent = { kind: "drop" | "timeout" | "pause" | "resume" | "abandon" | "revive"; round: number; tMs: number; elapsedMs: number; color: number; xQ: number; yQ: number };
export type RulesState = {
  seed: number; rng: number; variant: Variant; rings: number[]; pucks: number[]; active: number; score: number;
  phase: "Calm" | "Challenge" | "Recovery"; mode: FlowMode; previousChallenge: FlowMode; remaining: number;
  rotationSteps: number; durationMs: number; transitionMs: number;
};
export type ReplayState = {
  // Optional for workflow checkpoints journaled before v2 deployment.
  rulesVersion?: number; revivesAvailable?: number; awaitingRevive?: boolean;
  rules: RulesState; lastTMs: number; roundStartTMs: number; lastElapsedMs: number;
  pausedAt: number; roundPausedMs: number; terminal: boolean; perfects: number; events: number;
};
const modes: FlowMode[] = ["Floating", "Drifting", "Rotation"];
const ringSlots = [[-103000, 152000], [103000, 152000], [-103000, -152000], [103000, -152000]];
export const RING_RADIUS = 63500;
export const PERFECT_RADIUS = 22225;
export const MAX_TRACE_TIME_MS = 25 * 60 * 60 * 1000;
export function nextUInt(s: { rng: number }) {
  let x = s.rng >>> 0;
  x ^= x << 13; x ^= x >>> 17; x ^= x << 5;
  return s.rng = x >>> 0;
}
function next(s: { rng: number }, n: number) { return nextUInt(s) % n; }
function permutation(s: { rng: number }) {
  const a = [0, 1, 2, 3];
  for (let i = 3; i > 0; i--) { const j = next(s, i + 1); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}
function timer(score: number) { return score >= 40 ? 1750 : score >= 20 ? 2000 : score >= 5 ? 2500 : 3000; }
export function initialRules(seed: number, variant: Variant): RulesState {
  const s: RulesState = { seed, rng: seed === 0 ? 0x6D2B79F5 : seed >>> 0, variant,
    rings: [], pucks: [], active: 0, score: 0, phase: "Calm", mode: "Steady", previousChallenge: "Steady", remaining: 0,
    rotationSteps: 0, durationMs: 3000, transitionMs: 0 };
  s.rings = permutation(s); s.pucks = permutation(s); s.active = next(s, 4); s.remaining = 3 + next(s, 3);
  return s;
}
export function advance(s: RulesState) {
  const previousMode = s.mode;
  s.score++; s.active = next(s, 4); s.rotationSteps = 0;
  if (--s.remaining <= 0) {
    if (s.phase === "Calm") {
      s.phase = "Challenge";
      const eligible = modes.slice(0, s.score >= 40 ? 3 : 2).filter(mode => mode !== s.previousChallenge);
      s.mode = s.previousChallenge = eligible[next(s, eligible.length)];
      s.remaining = 3 + next(s, 3);
      if (s.mode === "Rotation") s.rotationSteps = next(s, 2) === 0 ? -1 : 1;
    } else if (s.phase === "Challenge") {
      s.phase = "Recovery"; s.mode = "Breather"; s.remaining = 2;
    } else { s.phase = "Calm"; s.mode = "Steady"; s.remaining = 3 + next(s, 3); }
  }
  let ringsChanged = false, pucksChanged = false;
  if (s.rotationSteps !== 0) {
    const cycle = [0, 1, 3, 2], result = [0, 0, 0, 0];
    for (let i = 0; i < 4; i++) result[cycle[(i + s.rotationSteps + 4) % 4]] = s.pucks[cycle[i]];
    s.pucks = result; pucksChanged = true;
  } else {
    if (s.score > 80 || (s.score > 60 && s.score % 2 === 0) || (s.score > 30 && s.score % 5 === 0)) {
      s.rings = permutation(s); ringsChanged = true;
    }
    if (s.score > 40 && s.score % 5 === 0) {
      const result = permutation(s);
      if (result.every((color, i) => color === s.pucks[i])) [result[0], result[1]] = [result[1], result[0]];
      s.pucks = result; pucksChanged = true;
    }
  }
  s.transitionMs = s.rotationSteps !== 0 ? 750 : ringsChanged && pucksChanged ? 675 : ringsChanged || pucksChanged || s.mode !== previousMode ? 450 : 240;
  s.durationMs = timer(s.score) + (s.phase === "Recovery" ? 650 : 0);
}
function wave(phase: number) {
  const u = phase <= 4000 ? phase : 8000 - phase;
  return 2 * Math.trunc(u * u * (12000 - 2 * u) * 1000 / 64000000000) - 1000;
}
export function ringCenter(s: RulesState, color: number, elapsedMs: number): [number, number] {
  const slot = s.rings.indexOf(color);
  if (slot < 0) throw new Error("Unknown color");
  let [x, y] = ringSlots[slot];
  if (s.variant === "lively" && s.mode === "Drifting") {
    const time = Math.max(0, elapsedMs), blend = Math.min(time, 500);
    const phase = (s.seed % 8000 + s.score * 977 + color * 1999 + time) % 8000;
    x += Math.trunc(wave(phase) * 8000 * blend / 500000);
    y += Math.trunc(wave((phase + 2000) % 8000) * 5200 * blend / 500000);
  }
  return [x, y];
}
export function within(x: number, y: number, center: [number, number], radius: number) {
  const dx = x - center[0], dy = y - center[1];
  return dx * dx + dy * dy <= radius * radius;
}
export function initialReplay(seed: number, variant: Variant, rulesVersion = 1): ReplayState {
  return { rulesVersion, revivesAvailable: 0, awaitingRevive: false, rules: initialRules(seed, variant), lastTMs: 0, roundStartTMs: 0, lastElapsedMs: 0, pausedAt: -1, roundPausedMs: 0, terminal: false, perfects: 0, events: 0 };
}
function loseChance(state: ReplayState) {
  state.awaitingRevive = state.rulesVersion === 2 && (state.revivesAvailable ?? 0) > 0;
  state.terminal = !state.awaitingRevive;
}
function earnRevive(state: ReplayState) {
  const score = state.rules.score;
  if (state.rulesVersion === 2 && (score === 20 || score === 50 || (score >= 100 && score % 50 === 0)))
    state.revivesAvailable = Math.min(3, (state.revivesAvailable ?? 0) + 1);
}
function int(value: number, min: number, max: number) { return Number.isSafeInteger(value) && value >= min && value <= max; }
/** Returns a player-facing rejection reason, or null. Mutates only the supplied checkpoint. */
export function replayEvents(state: ReplayState, events: TraceEvent[]): string | null {
  for (const event of events) {
    const s = state.rules;
    if (state.terminal) return "Events were submitted after the run ended.";
    if (event.kind === "revive") {
      if (state.rulesVersion !== 2 || !state.awaitingRevive || (state.revivesAvailable ?? 0) < 1)
        return "A revive was not available for this failure.";
      if (event.round !== s.score || !int(event.tMs, 0, MAX_TRACE_TIME_MS) || event.tMs < state.lastTMs
        || event.elapsedMs !== 0 || event.color !== -1 || event.xQ !== 0 || event.yQ !== 0)
        return "The revive trace contains invalid timing or coordinates.";
      state.revivesAvailable = (state.revivesAvailable ?? 0) - 1;
      state.awaitingRevive = false;
      state.roundStartTMs = event.tMs + 3000;
      state.lastElapsedMs = state.roundPausedMs = 0;
      state.pausedAt = -1;
      state.lastTMs = event.tMs; state.events++;
      continue;
    }
    if (state.awaitingRevive && event.kind !== "abandon") return "Choose whether to revive before continuing the run.";
    if (!int(event.round, 0, 65536) || event.round !== s.score || !int(event.tMs, 0, MAX_TRACE_TIME_MS) || event.tMs < state.lastTMs
      || !int(event.elapsedMs, 0, s.durationMs) || event.elapsedMs < state.lastElapsedMs
      || !int(event.color, -1, 3) || !int(event.xQ, -1000000, 1000000) || !int(event.yQ, -1000000, 1000000))
      return "The run trace contains invalid timing or coordinates.";
    if (event.kind === "resume") {
      if (state.pausedAt < 0 || event.elapsedMs !== state.lastElapsedMs) return "The pause sequence is invalid.";
      state.roundPausedMs += event.tMs - state.pausedAt; state.pausedAt = -1;
    } else if (event.kind === "pause") {
      if (state.pausedAt >= 0) return "The run was paused twice without resuming.";
      if (event.elapsedMs > 0 && event.tMs + 2 < state.roundStartTMs + state.roundPausedMs + event.elapsedMs) return "The round ran faster than its trace permits.";
      state.pausedAt = event.tMs;
    } else {
      // Abandon is also allowed from the pause menu; no new score is awarded.
      if (state.pausedAt >= 0 && event.kind !== "abandon") return "A move was submitted while paused.";
      if (event.kind !== "abandon" && event.tMs + 2 < state.roundStartTMs + state.roundPausedMs + event.elapsedMs)
        return "The round ran faster than its trace permits.";
      if (event.kind === "drop") {
        if ((event.color !== s.active && state.rulesVersion !== 2) || event.color < 0 || event.elapsedMs >= s.durationMs) return "The move used an inactive puck or expired timer.";
        const center = ringCenter(s, s.active, event.elapsedMs);
        if (event.color !== s.active || !within(event.xQ, event.yQ, center, RING_RADIUS)) loseChance(state);
        else {
          if (within(event.xQ, event.yQ, center, PERFECT_RADIUS)) state.perfects++;
          advance(s); earnRevive(state); state.roundStartTMs = event.tMs + s.transitionMs;
          state.roundPausedMs = 0; state.lastElapsedMs = 0;
          state.lastTMs = event.tMs; state.events++; continue;
        }
      } else if (event.kind === "timeout") {
        if (event.elapsedMs !== s.durationMs) return "A timeout was recorded before the timer expired.";
        loseChance(state);
      } else if (event.kind === "abandon") { state.terminal = true; state.awaitingRevive = false; }
      else return "The run trace contains an unsupported event.";
    }
    state.lastTMs = event.tMs; state.lastElapsedMs = event.elapsedMs; state.events++;
  }
  return null;
}

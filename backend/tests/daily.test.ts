import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { convexTest } from "convex-test";
import aggregate from "@convex-dev/aggregate/test";
import workflow from "@convex-dev/workflow/test";
import rateLimiter from "@convex-dev/rate-limiter/test";
import schema from "../convex/schema";
import { api, internal } from "../convex/_generated/api";
import { workflow as validationWorkflow } from "../convex/validation";
import { DAY, standingNumbers } from "../convex/model";
import { initialReplay, replayEvents, ringCenter, type TraceEvent } from "../convex/rules";
const modules = import.meta.glob("../convex/**/*.ts");
function setup() {
  const t = convexTest(schema, modules);
  aggregate.register(t,"dailyScores"); workflow.register(t); rateLimiter.register(t);
  return t;
}
type Test = ReturnType<typeof setup>;
async function guest(t: Test) {
  const id = await t.run(ctx => ctx.db.insert("users", { isAnonymous: true, closedTestEpoch: "1" }));
  return { id, client: t.withIdentity({ subject: `${id}|session` }) };
}
async function ready(t: Test, rulesVersion = 1) {
  await t.mutation(internal.publication.ensureUpcoming, {});
  const challenge = (await t.query(api.daily.current, {}))!;
  // Existing suites exercise a challenge published before the v2 rollout.
  await t.run(ctx => ctx.db.patch(challenge.id, { rulesVersion }));
  return { ...challenge, rulesVersion };
}
async function record(t: Test, client: ReturnType<Test["withIdentity"]>, challenge: NonNullable<Awaited<ReturnType<typeof ready>>>, score: number, requestId: string) {
  const attempt = await client.mutation(api.daily.createAttempt, { challengeId: challenge.id, requestId, clientRulesRevision: 2 });
  const checkpoint = initialReplay(challenge.seed,challenge.variant), events: TraceEvent[] = [];
  for (let i=0;i<score;i++) {
    const [xQ,yQ]=ringCenter(checkpoint.rules,checkpoint.rules.active,100);
    const event:TraceEvent={kind:"drop",round:i,tMs:checkpoint.roundStartTMs+100,elapsedMs:100,color:checkpoint.rules.active,xQ,yQ};
    events.push(event); replayEvents(checkpoint,[event]);
  }
  const end:TraceEvent={kind:"abandon",round:score,tMs:checkpoint.roundStartTMs,elapsedMs:0,color:-1,xQ:0,yQ:0}; events.push(end); replayEvents(checkpoint,[end]);
  await client.mutation(api.daily.appendChunk,{attemptId:attempt.attemptId,index:0,events});
  // Isolate atomic result/ranking behavior from the component runner in these tests.
  await t.run(ctx=>ctx.db.patch(attempt.attemptId,{status:"validating",submittedAt:Date.now()+checkpoint.lastTMs}));
  await t.mutation(internal.validation.recordResult,{attemptId:attempt.attemptId,checkpoint,error:null});
  return attempt;
}
beforeEach(()=>{ vi.useFakeTimers(); vi.setSystemTime(new Date("2026-09-06T12:00:00Z")); vi.stubEnv("RING_RUSH_CLOSED_TEST_CODE","local-test-only"); vi.stubEnv("RING_RUSH_CLOSED_TEST_EPOCH","1"); });
afterEach(()=>{ vi.useRealTimers(); vi.unstubAllEnvs(); });
describe("Publication and access",()=>{
  it("reports scheduled health failures but accepts an intentional ranked pause",async()=>{
    const t=setup();
    const log=vi.spyOn(console,"error").mockImplementation(()=>{});
    try {
      await expect(t.action(internal.operations.checkHealth,{})).rejects.toThrow("Daily health check failed");
      expect(log).toHaveBeenCalledWith("Daily health check failed: challenge availability or validation requires attention.");
      await ready(t);
      await t.mutation(internal.operations.setRankedEnabled,{enabled:false});
      await expect(t.action(internal.operations.checkHealth,{})).resolves.toBeNull();
      expect(log).toHaveBeenCalledTimes(1);
    } finally { log.mockRestore(); }
  });
  it("keeps concurrent publication idempotent and health notices stalled validation",async()=>{
    const t=setup();
    const created=await Promise.all([t.mutation(internal.publication.ensureUpcoming,{}),t.mutation(internal.publication.ensureUpcoming,{})]);
    expect(created.reduce((a,b)=>a+b,0)).toBe(8);
    const challenge=(await t.query(api.daily.current,{}))!,{client}=await guest(t);
    const attempt=await client.mutation(api.daily.createAttempt,{clientRulesRevision:3,challengeId:challenge.id,requestId:"health-0001"});
    expect((await t.query(internal.operations.health,{})).ready).toBe(true);
    await t.run(ctx=>ctx.db.patch(attempt.attemptId,{status:"validating",submittedAt:Date.now()-300001}));
    const health=await t.query(internal.operations.health,{});expect(health.validationStalled).toBe(true);expect(health.ready).toBe(false);
  });
  it("publishes today and seven upcoming days once without revealing future seeds",async()=>{
    const t=setup(); expect(await t.mutation(internal.publication.ensureUpcoming,{})).toBe(8);
    const first=await t.query(api.daily.current,{}); expect(first?.date).toBe("2026-09-06"); expect(first?.rulesVersion).toBe(2);
    expect(await t.mutation(internal.publication.ensureUpcoming,{})).toBe(0); expect(await t.query(api.daily.current,{})).toEqual(first);
    const rows=await t.run(ctx=>ctx.db.query("challenges").take(9)); expect(rows).toHaveLength(8);
    expect(rows.map(x=>x.seed)).toHaveLength(8);
    vi.setSystemTime(new Date("2026-09-07T00:00:00Z")); const tomorrow=await t.query(api.daily.current,{});
    expect(tomorrow?.date).toBe("2026-09-07"); expect(tomorrow?.variant).not.toBe(first?.variant);
  });
  it("rejects unauthenticated and revoked guest mutations",async()=>{
    const t=setup(), challenge=await ready(t);
    await expect(t.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"})).rejects.toThrow("Sign in");
    const {client}=await guest(t); vi.stubEnv("RING_RUSH_CLOSED_TEST_EPOCH","2");
    await expect(client.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"})).rejects.toThrow("no longer active");
  });
  it("starts idempotently, forbids future starts, and observes the kill switch",async()=>{
    const t=setup(), challenge=await ready(t), {client}=await guest(t);
    const args={clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"};
    expect(await client.mutation(api.daily.createAttempt,args)).toEqual(await client.mutation(api.daily.createAttempt,args));
    const future=await t.run(ctx=>ctx.db.query("challenges").withIndex("by_date",q=>q.eq("date","2026-09-07")).unique());
    await expect(client.mutation(api.daily.createAttempt,{...args,clientRulesRevision:3,challengeId:future!._id,requestId:"request-0002"})).rejects.toThrow("not open");
    await t.mutation(internal.operations.setRankedEnabled,{enabled:false});
    await expect(client.mutation(api.daily.createAttempt,{...args,requestId:"request-0003"})).rejects.toThrow("temporarily paused");
    expect((await t.query(api.daily.current,{}))?.publicCompetitionEnabled).toBe(false);
  });
});
describe("Client rules revision gate",()=>{
  it("accepts revisions 2 and 3 for v1 while rejecting obsolete and unknown clients",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const args={challengeId:challenge.id,requestId:"revision-0001"};
    for(const clientRulesRevision of [undefined,1,4,2.5])
      await expect(client.mutation(api.daily.createAttempt,{...args,clientRulesRevision})).rejects.toThrow("UPDATE_REQUIRED");
    expect(await t.run(ctx=>ctx.db.query("attempts").take(1))).toHaveLength(0);
    const started=await client.mutation(api.daily.createAttempt,{...args,clientRulesRevision:2});
    expect((await t.run(ctx=>ctx.db.get(started.attemptId)))?.clientRulesRevision).toBe(2);
    expect(await client.mutation(api.daily.createAttempt,{...args,clientRulesRevision:2})).toEqual(started);
    expect(await client.mutation(api.daily.createAttempt,{...args,clientRulesRevision:3})).toEqual(started);
    await expect(client.mutation(api.daily.createAttempt,args)).rejects.toThrow("UPDATE_REQUIRED");
    // Existing rows stay schema-valid, but updating the app cannot relabel a legacy attempt.
    await t.run(ctx=>ctx.db.patch(started.attemptId,{clientRulesRevision:undefined}));
    await expect(client.mutation(api.daily.createAttempt,{...args,clientRulesRevision:2})).rejects.toThrow("UPDATE_REQUIRED");
  });
  it("rejects legacy new chunks, duplicate chunks, and finalization before retries can bypass the gate",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const started=await client.mutation(api.daily.createAttempt,{challengeId:challenge.id,requestId:"revision-0002",clientRulesRevision:2});
    const args={attemptId:started.attemptId,index:0,events:[{kind:"abandon" as const,round:0,tMs:0,elapsedMs:0,color:-1,xQ:0,yQ:0}]};
    await client.mutation(api.daily.appendChunk,args);
    await t.run(ctx=>ctx.db.patch(started.attemptId,{clientRulesRevision:undefined}));
    await expect(client.mutation(api.daily.appendChunk,args)).rejects.toThrow("UPDATE_REQUIRED");
    await expect(client.mutation(api.daily.appendChunk,{...args,index:1})).rejects.toThrow("UPDATE_REQUIRED");
    await expect(client.mutation(api.daily.finalize,{attemptId:started.attemptId,chunkCount:1})).rejects.toThrow("UPDATE_REQUIRED");
    const obsolete=await client.query(api.daily.attemptStatus,{attemptId:started.attemptId});
    expect(obsolete.status).toBe("rejected");expect(obsolete.reason).toContain("Progress from this run stays on your device");
    expect((await t.run(ctx=>ctx.db.get(started.attemptId)))?.status).toBe("open");
    expect(await t.run(ctx=>ctx.db.query("traceChunks").take(2))).toHaveLength(1);
  });
  it("allows revision 2 finalization and identical retries",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const started=await client.mutation(api.daily.createAttempt,{challengeId:challenge.id,requestId:"revision-0003",clientRulesRevision:2});
    await client.mutation(api.daily.appendChunk,{attemptId:started.attemptId,index:0,events:[{kind:"abandon",round:0,tMs:0,elapsedMs:0,color:-1,xQ:0,yQ:0}]});
    const args={attemptId:started.attemptId,chunkCount:1};
    // The component runner is exercised by the live smoke script; isolate our mutation's dispatch/retry behavior.
    const start=vi.spyOn(validationWorkflow,"start").mockResolvedValue("revision-test-workflow" as never);
    try {
      const finalized=await client.mutation(api.daily.finalize,args);
      expect(finalized.status).toBe("validating");
      expect(await client.mutation(api.daily.finalize,args)).toEqual(finalized);
      expect(start).toHaveBeenCalledTimes(1);
    } finally { start.mockRestore(); }
  });
  it("rejects old in-flight workflow results without changing historical accepted standings",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const historical=await record(t,client,challenge,1,"legacy-best-0001");
    await t.run(ctx=>ctx.db.patch(historical.attemptId,{clientRulesRevision:undefined}));
    const started=await client.mutation(api.daily.createAttempt,{challengeId:challenge.id,requestId:"revision-0004",clientRulesRevision:2});
    const checkpoint=initialReplay(challenge.seed,challenge.variant);
    const terminal:TraceEvent={kind:"abandon",round:0,tMs:0,elapsedMs:0,color:-1,xQ:0,yQ:0};
    replayEvents(checkpoint,[terminal]);
    await t.run(ctx=>ctx.db.patch(started.attemptId,{clientRulesRevision:undefined,status:"validating",submittedAt:Date.now(),eventCount:1}));
    expect((await client.query(api.daily.attemptStatus,{attemptId:started.attemptId})).status).toBe("rejected");
    expect((await t.run(ctx=>ctx.db.get(started.attemptId)))?.status).toBe("validating");
    await t.mutation(internal.validation.recordResult,{attemptId:started.attemptId,checkpoint,error:null});
    const rejected=await client.query(api.daily.attemptStatus,{attemptId:started.attemptId});
    expect(rejected.status).toBe("rejected");expect(rejected.reason).toContain("Update Ring Rush");
    const standing=await client.query(api.daily.myStanding,{challengeId:challenge.id});
    expect(standing.bestScore).toBe(1);expect(standing.participants).toBe(1);
    expect((await client.query(api.daily.attemptStatus,{attemptId:historical.attemptId})).status).toBe("accepted");
  });
});
describe("Bounded upload",()=>{
  it("enforces ownership, chunk order and identical retry payloads",async()=>{
    const t=setup(), challenge=await ready(t), a=await guest(t), b=await guest(t);
    const attempt=await a.client.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"});
    const event:TraceEvent={kind:"abandon",round:0,tMs:0,elapsedMs:0,color:-1,xQ:0,yQ:0};
    const args={attemptId:attempt.attemptId,index:0,events:[event]};
    await expect(b.client.mutation(api.daily.appendChunk,args)).rejects.toThrow("not found");
    await expect(b.client.query(api.daily.attemptStatus,{attemptId:attempt.attemptId})).rejects.toThrow("not found");
    await expect(a.client.mutation(api.daily.appendChunk,{...args,index:1})).rejects.toThrow("Upload chunk 0");
    const uploaded=await a.client.mutation(api.daily.appendChunk,args); expect(uploaded.nextChunkIndex).toBe(1);
    expect(await a.client.mutation(api.daily.appendChunk,args)).toEqual(uploaded);
    await expect(a.client.mutation(api.daily.appendChunk,{...args,events:[{...event,tMs:1}]})).rejects.toThrow("different events");
    await expect(a.client.mutation(api.daily.appendChunk,{...args,index:1,events:Array(129).fill(event)})).rejects.toThrow("128 events");
  });
  it("rejects late uploads and new starts after midnight",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const attempt=await client.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"});
    vi.setSystemTime(challenge.closesAt);
    await expect(client.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0002"})).rejects.toThrow("not open");
    vi.setSystemTime(challenge.uploadDeadline);
    const result=await client.mutation(api.daily.appendChunk,{attemptId:attempt.attemptId,index:0,events:[{kind:"abandon",round:0,tMs:0,elapsedMs:0,color:-1,xQ:0,yQ:0}]});
    expect(result.status).toBe("expired");
  });
});
describe("Personal best and tie-aware standings",()=>{
  it("counts each guest once, replaces improvement, handles ties and exclusions",async()=>{
    const t=setup(),challenge=await ready(t),a=await guest(t),b=await guest(t),c=await guest(t);
    await record(t,a.client,challenge,1,"a-run-0001");
    let s=await a.client.query(api.daily.myStanding,{challengeId:challenge.id}); expect(s.participants).toBe(1);expect(s.waiting).toBe(true);expect(s.percentile).toBeNull();
    await record(t,b.client,challenge,2,"b-run-0001");await record(t,c.client,challenge,2,"c-run-0001");
    s=await b.client.query(api.daily.myStanding,{challengeId:challenge.id});expect(s.participants).toBe(3);expect(s.percentile).toBeCloseTo(100*2/3);expect(s.topPercent).toBe(34);
    await record(t,a.client,challenge,3,"a-run-0002");await record(t,a.client,challenge,0,"a-run-0003");
    s=await a.client.query(api.daily.myStanding,{challengeId:challenge.id});expect(s.bestScore).toBe(3);expect(s.participants).toBe(3);
    await t.mutation(internal.operations.excludeStanding,{challengeId:challenge.id,userId:c.id,reason:"Synthetic test submission"});
    expect((await a.client.query(api.daily.myStanding,{challengeId:challenge.id})).participants).toBe(2);
    expect((await c.client.query(api.daily.myStanding,{challengeId:challenge.id})).excluded).toBe(true);
  });
  it("does not accept forged trace duration or an unended run",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    const attempt=await client.mutation(api.daily.createAttempt,{clientRulesRevision:2,challengeId:challenge.id,requestId:"request-0001"});
    await t.run(ctx=>ctx.db.patch(attempt.attemptId,{status:"validating",submittedAt:Date.now()}));
    const checkpoint=initialReplay(challenge.seed,challenge.variant);checkpoint.terminal=true;checkpoint.lastTMs=10_000;
    await t.mutation(internal.validation.recordResult,{attemptId:attempt.attemptId,checkpoint,error:null});
    expect((await client.query(api.daily.attemptStatus,{attemptId:attempt.attemptId})).status).toBe("rejected");
  });
  it("freezes after pending validation completes and preserves final standings",async()=>{
    const t=setup(),challenge=await ready(t),{client,id}=await guest(t);
    await record(t,client,challenge,1,"request-0001");
    vi.setSystemTime(challenge.uploadDeadline); await t.mutation(internal.operations.sealDue,{});
    expect((await client.query(api.daily.myStanding,{challengeId:challenge.id})).provisional).toBe(false);
    await expect(t.mutation(internal.operations.excludeStanding,{challengeId:challenge.id,userId:id,reason:"Late exclusion"})).rejects.toThrow("immutable");
  });
  it("cleans raw traces after seven days and standings after a year",async()=>{
    const t=setup(),challenge=await ready(t),{client}=await guest(t);
    await record(t,client,challenge,1,"request-0001");
    vi.setSystemTime(Date.now()+7*DAY+1); await t.mutation(internal.operations.purge,{});
    expect(await t.run(ctx=>ctx.db.query("traceChunks").take(1))).toHaveLength(0);
    expect((await client.query(api.daily.myStanding,{challengeId:challenge.id})).bestScore).toBe(1);
    vi.setSystemTime(challenge.uploadDeadline+365*DAY); await t.mutation(internal.operations.purge,{});
    expect(await t.run(ctx=>ctx.db.query("dailyBests").take(1))).toHaveLength(0);
  });
  it("uses the midpoint of ties and flags early cohorts",()=>{
    expect(standingNumbers(10,4,20)).toEqual({percentile:60,topPercent:40,early:false,waiting:false});
  });
});

describe("Daily v2 rollout", () => {
  it("leaves a published v1 challenge immutable while publishing only new dates as v2", async () => {
    const t = setup(), old = await ready(t);
    vi.setSystemTime(new Date("2026-09-07T12:00:00Z"));
    expect(await t.mutation(internal.publication.ensureUpcoming, {})).toBe(1);
    expect((await t.run(ctx => ctx.db.get(old.id)))?.rulesVersion).toBe(1);
    const newest = await t.run(ctx => ctx.db.query("challenges").withIndex("by_date", q => q.eq("date", "2026-09-14")).unique());
    expect(newest?.rulesVersion).toBe(2);
  });
  it("requires revision 3 for v2 across start, retries, upload, finalize, status and validation", async () => {
    const t = setup(), challenge = await ready(t, 2), { client } = await guest(t);
    const args = { challengeId: challenge.id, requestId: "revives-v2-0001", clientRulesRevision: 3 };
    await expect(client.mutation(api.daily.createAttempt, {...args, clientRulesRevision:2})).rejects.toThrow("UPDATE_REQUIRED");
    const attempt = await client.mutation(api.daily.createAttempt, args);
    expect(await client.mutation(api.daily.createAttempt, args)).toEqual(attempt);
    const terminal: TraceEvent = {kind:"abandon", round:0, tMs:0, elapsedMs:0, color:-1, xQ:0, yQ:0};
    const chunk = {attemptId:attempt.attemptId, index:0, events:[terminal]};
    await client.mutation(api.daily.appendChunk, chunk);
    await t.run(ctx => ctx.db.patch(attempt.attemptId, {clientRulesRevision:2}));
    await expect(client.mutation(api.daily.createAttempt, args)).rejects.toThrow("UPDATE_REQUIRED");
    await expect(client.mutation(api.daily.appendChunk, chunk)).rejects.toThrow("UPDATE_REQUIRED");
    await expect(client.mutation(api.daily.finalize, {attemptId:attempt.attemptId, chunkCount:1})).rejects.toThrow("UPDATE_REQUIRED");
    expect((await client.query(api.daily.attemptStatus, {attemptId:attempt.attemptId})).status).toBe("rejected");
    const checkpoint = initialReplay(challenge.seed, challenge.variant, 2); replayEvents(checkpoint, [terminal]);
    await t.run(ctx => ctx.db.patch(attempt.attemptId, {status:"validating", submittedAt:Date.now()}));
    await t.mutation(internal.validation.recordResult, {attemptId:attempt.attemptId, checkpoint, error:null});
    expect((await client.query(api.daily.attemptStatus, {attemptId:attempt.attemptId})).status).toBe("rejected");
    expect((await client.query(api.daily.myStanding, {challengeId:challenge.id})).participants).toBe(0);
  });
  it("validates revive trace chunks across checkpoints and publishes the continued score", async () => {
    const t = setup(), challenge = await ready(t, 2), { client } = await guest(t);
    const attempt = await client.mutation(api.daily.createAttempt, {challengeId:challenge.id,requestId:"revives-v2-0002",clientRulesRevision:3});
    const local = initialReplay(challenge.seed, challenge.variant, 2), first: TraceEvent[] = [];
    for (let i = 0; i < 20; i++) {
      const [xQ,yQ] = ringCenter(local.rules, local.rules.active, 100);
      const event: TraceEvent = {kind:"drop",round:i,tMs:local.roundStartTMs+100,elapsedMs:100,color:local.rules.active,xQ,yQ};
      first.push(event); expect(replayEvents(local,[event])).toBeNull();
    }
    const failure: TraceEvent = {kind:"drop",round:20,tMs:local.roundStartTMs+100,elapsedMs:100,color:local.rules.active,xQ:0,yQ:0};
    first.push(failure); replayEvents(local,[failure]);
    const revive: TraceEvent = {kind:"revive",round:20,tMs:local.lastTMs+30000,elapsedMs:0,color:-1,xQ:0,yQ:0};
    replayEvents(local,[revive]);
    const end: TraceEvent = {kind:"abandon",round:20,tMs:local.roundStartTMs,elapsedMs:0,color:-1,xQ:0,yQ:0};
    await client.mutation(api.daily.appendChunk,{attemptId:attempt.attemptId,index:0,events:first});
    await client.mutation(api.daily.appendChunk,{attemptId:attempt.attemptId,index:1,events:[revive,end]});
    await t.run(ctx=>ctx.db.patch(attempt.attemptId,{status:"validating",submittedAt:Date.now()+end.tMs}));
    const start = await t.query(internal.validation.beginReplay,{attemptId:attempt.attemptId});
    const failed = await t.query(internal.validation.validateChunk,{attemptId:attempt.attemptId,index:0,checkpoint:start});
    expect(failed.error).toBeNull(); expect(failed.checkpoint.awaitingRevive).toBe(true); expect(failed.checkpoint.terminal).toBe(false);
    const result = await t.query(internal.validation.validateChunk,{attemptId:attempt.attemptId,index:1,checkpoint:failed.checkpoint});
    expect(result.error).toBeNull(); expect(result.checkpoint.revivesAvailable).toBe(0);
    await t.mutation(internal.validation.recordResult,{attemptId:attempt.attemptId,...result});
    expect((await client.query(api.daily.myStanding,{challengeId:challenge.id})).bestScore).toBe(20);
  });
});

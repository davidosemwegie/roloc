/** Development-only real HTTP/auth/workflow smoke. Never prints credentials. */
import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { initialReplay, replayEvents, ringCenter } from "../convex/rules.ts";
const local = Object.fromEntries(readFileSync(new URL("../.env.local",import.meta.url),"utf8").split("\n").filter(line=>/^[A-Z_]+=/.test(line)).map(line=>{
  const at=line.indexOf("="); return [line.slice(0,at),line.slice(at+1).trim().replace(/^['"]|['"]$/g,"")];
}));
assert.match(local.CONVEX_DEPLOYMENT??"",/^dev:/,"Smoke is restricted to a development deployment.");
const base=local.CONVEX_URL;
assert.match(base??"",/^https:\/\/[^/]+\.convex\.cloud$/);
const code=execFileSync("npx",["convex","env","get","RING_RUSH_CLOSED_TEST_CODE"],{cwd:new URL("..",import.meta.url),encoding:"utf8",stdio:["ignore","pipe","pipe"]}).trim();
assert.ok(code && code !== "undefined","Closed-test environment is not configured.");
async function call(kind,path,args,token) {
  const response=await fetch(`${base}/api/${kind}`,{method:"POST",headers:{"content-type":"application/json",...(token?{Authorization:`Bearer ${token}`}:{})},body:JSON.stringify({path,args,format:"json"})});
  const result=await response.json();
  if(result.status!=="success") throw new Error(`${path}: ${result.errorMessage??"Request failed"}`);
  return result.value;
}
const auth=await call("action","auth:signIn",{provider:"anonymous",params:{closedTestCode:code}});
assert.ok(auth.tokens?.token && auth.tokens?.refreshToken,"Anonymous auth did not issue credentials.");
const refreshed=await call("action","auth:signIn",{refreshToken:auth.tokens.refreshToken});
assert.ok(refreshed.tokens?.token,"Refresh did not issue a credential.");
const token=refreshed.tokens.token;
try {
  const challenge=await call("query","daily:current",{});
  assert.ok(challenge?.id,"Publish current challenge before smoke.");
  const attempt=await call("mutation","daily:createAttempt",{challengeId:challenge.id,requestId:randomUUID(),clientRulesRevision:3},token);
  const checkpoint=initialReplay(challenge.seed,challenge.variant,challenge.rulesVersion),events=[];
  for(let i=0;i<3;i++) {
    const elapsedMs=100,[xQ,yQ]=ringCenter(checkpoint.rules,checkpoint.rules.active,elapsedMs);
    const event={kind:"drop",round:i,tMs:checkpoint.roundStartTMs+elapsedMs,elapsedMs,color:checkpoint.rules.active,xQ,yQ};
    events.push(event); assert.equal(replayEvents(checkpoint,[event]),null);
  }
  const end={kind:"abandon",round:3,tMs:checkpoint.roundStartTMs,elapsedMs:0,color:-1,xQ:0,yQ:0};events.push(end);
  // Real elapsed time must support the submitted trace.
  await new Promise(resolve=>setTimeout(resolve,end.tMs+50));
  const first={attemptId:attempt.attemptId,index:0,events:events.slice(0,2)};
  await call("mutation","daily:appendChunk",first,token);await call("mutation","daily:appendChunk",first,token);
  await call("mutation","daily:appendChunk",{attemptId:attempt.attemptId,index:1,events:events.slice(2)},token);
  await call("mutation","daily:finalize",{attemptId:attempt.attemptId,chunkCount:2},token);
  await call("mutation","daily:finalize",{attemptId:attempt.attemptId,chunkCount:2},token);
  let status;
  for(let i=0;i<60;i++) {
    status=await call("query","daily:attemptStatus",{attemptId:attempt.attemptId},token);
    if(status.status!=="validating")break;
    await new Promise(resolve=>setTimeout(resolve,1000));
  }
  assert.equal(status.status,"accepted",status.reason??"Validation did not complete");assert.equal(status.score,3);
  const standing=await call("query","daily:myStanding",{challengeId:challenge.id},token);assert.equal(standing.bestScore,3);
  console.log(JSON.stringify({auth:"passed",refresh:"passed",chunkRetry:"passed",durableValidation:status.status,derivedScore:status.score,personalBest:standing.bestScore,participants:standing.participants},null,2));
} finally { await call("action","auth:signOut",{},token); }

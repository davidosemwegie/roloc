import { v } from "convex/values";
import { WorkflowManager, vWorkflowId, vResultValidator } from "@convex-dev/workflow";
import { internalQuery, internalMutation } from "./_generated/server";
import { components, internal } from "./_generated/api";
import { initialReplay, replayEvents, type ReplayState } from "./rules";
import { boardVariant } from "./validators";
import { supportsClientRulesRevision, CLIENT_UPDATE_MESSAGE, DAY, scores } from "./model";

const mode = v.union(v.literal("Steady"), v.literal("Floating"), v.literal("Drifting"), v.literal("Breather"), v.literal("Rotation"));
export const replayCheckpoint = v.object({
  rulesVersion: v.optional(v.number()), revivesAvailable: v.optional(v.number()), awaitingRevive: v.optional(v.boolean()),
  rules: v.object({ seed: v.number(), rng: v.number(), variant: boardVariant, rings: v.array(v.number()), pucks: v.array(v.number()), active: v.number(), score: v.number(),
    phase: v.union(v.literal("Calm"), v.literal("Challenge"), v.literal("Recovery")), mode, previousChallenge: mode, remaining: v.number(),
    rotationSteps: v.number(), durationMs: v.number(), transitionMs: v.number() }),
  lastTMs: v.number(), roundStartTMs: v.number(), lastElapsedMs: v.number(), pausedAt: v.number(), roundPausedMs: v.number(), terminal: v.boolean(), perfects: v.number(), events: v.number(),
});
const chunkResult = v.object({ checkpoint: replayCheckpoint, error: v.union(v.string(), v.null()) });
export const workflow = new WorkflowManager(components.workflow);
export const validateAttempt = workflow.define({
  args: { attemptId: v.id("attempts"), chunkCount: v.number() }, returns: v.null(),
  handler: async (step, args): Promise<null> => {
    let checkpoint: ReplayState = await step.runQuery(internal.validation.beginReplay, { attemptId: args.attemptId });
    let error: string | null = null;
    for (let index = 0; index < args.chunkCount; index++) {
      const result: { checkpoint: ReplayState; error: string | null } = await step.runQuery(internal.validation.validateChunk, { attemptId: args.attemptId, index, checkpoint });
      checkpoint = result.checkpoint; error = result.error;
      if (error) break;
    }
    if (!error && !checkpoint.terminal) error = "The run trace does not contain an ending.";
    await step.runMutation(internal.validation.recordResult, { attemptId: args.attemptId, checkpoint, error });
    return null;
  },
});
export const beginReplay = internalQuery({
  args: { attemptId: v.id("attempts") }, returns: replayCheckpoint,
  handler: async (ctx, args) => {
    const attempt = await ctx.db.get(args.attemptId);
    if (!attempt || attempt.status !== "validating") throw new Error("Attempt unavailable for validation");
    const challenge = await ctx.db.get(attempt.challengeId);
    if (!challenge || !supportsClientRulesRevision(attempt.clientRulesRevision, challenge.rulesVersion)) throw new Error("Unsupported Daily rules");
    return initialReplay(challenge.seed, challenge.variant, challenge.rulesVersion);
  },
});
export const validateChunk = internalQuery({
  args: { attemptId: v.id("attempts"), index: v.number(), checkpoint: replayCheckpoint }, returns: chunkResult,
  handler: async (ctx, args) => {
    const chunk = await ctx.db.query("traceChunks").withIndex("by_attemptId_and_index", q => q.eq("attemptId", args.attemptId).eq("index", args.index)).unique();
    if (!chunk) return { checkpoint: args.checkpoint, error: "A trace chunk is missing." };
    return { checkpoint: args.checkpoint, error: replayEvents(args.checkpoint, chunk.events) };
  },
});
export const recordResult = internalMutation({
  args: { attemptId: v.id("attempts"), checkpoint: replayCheckpoint, error: v.union(v.string(), v.null()) }, returns: v.null(),
  handler: async (ctx, args) => {
    const attempt = await ctx.db.get(args.attemptId);
    if (!attempt || attempt.status !== "validating") return null;
    const challenge = await ctx.db.get(attempt.challengeId);
    let error = args.error;
    if (!challenge || challenge.sealedAt !== undefined) error = "The challenge standings are finalized.";
    if (attempt.submittedAt === undefined || attempt.submittedAt >= attempt.uploadDeadline) error = "The upload deadline passed.";
    if (attempt.submittedAt !== undefined && args.checkpoint.lastTMs > attempt.submittedAt - attempt.startedAt + 2000) error = "The submitted run takes longer than the attempt existed.";
    if (!args.checkpoint.terminal && !error) error = "The run trace does not contain an ending.";
    if (args.checkpoint.events !== attempt.eventCount && !error) error = "The event count does not match the uploaded trace.";
    // A workflow started before the client-rule cutover must not add a new standing afterward.
    if (!supportsClientRulesRevision(attempt.clientRulesRevision, challenge?.rulesVersion ?? 0)) error = CLIENT_UPDATE_MESSAGE;
    const finalizedAt = Date.now();
    if (error) {
      await ctx.db.patch(attempt._id, { status: "rejected", reason: error, finalizedAt, purgeAt: finalizedAt + 7 * DAY });
      return null;
    }
    const score = args.checkpoint.rules.score;
    const best = await ctx.db.query("dailyBests").withIndex("by_challengeId_and_userId", q => q.eq("challengeId", attempt.challengeId).eq("userId", attempt.userId)).unique();
    if (!best) {
      const id = await ctx.db.insert("dailyBests", { challengeId: attempt.challengeId, userId: attempt.userId, score, attemptId: attempt._id,
        updatedAt: finalizedAt, expiresAt: attempt.uploadDeadline + 365 * DAY, excluded: false });
      await scores.insert(ctx, (await ctx.db.get(id))!);
    } else if (!best.excluded && score > best.score) {
      const updated = { ...best, score, attemptId: attempt._id, updatedAt: finalizedAt };
      await ctx.db.patch(best._id, { score, attemptId: attempt._id, updatedAt: finalizedAt });
      await scores.replace(ctx, best, updated);
    }
    await ctx.db.patch(attempt._id, { status: "accepted", score, finalizedAt, purgeAt: finalizedAt + 7 * DAY });
    return null;
  },
});
export const onComplete = internalMutation({
  args: { workflowId: vWorkflowId, result: vResultValidator, context: v.object({ attemptId: v.id("attempts") }) }, returns: v.null(),
  handler: async (ctx, args) => {
    const attempt = await ctx.db.get(args.context.attemptId);
    if (args.result.kind !== "success" && attempt?.status === "validating") {
      // Expose no internal error details or user traces. Convex retains the workflow error for operators.
      await ctx.db.patch(attempt._id, { status: "rejected", reason: "Daily validation could not finish. Please try another run.", finalizedAt: Date.now(), purgeAt: Date.now() + 7 * DAY });
    }
    await ctx.scheduler.runAfter(7 * DAY, internal.operations.cleanupWorkflow, { workflowId: args.workflowId });
    return null;
  },
});

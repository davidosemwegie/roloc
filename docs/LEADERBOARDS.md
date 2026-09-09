# Public casual leaderboard

The current client ranks regular Lively Flow games independently from the invitation-only Daily challenge. Rush is no longer selectable; existing Rush records, queued submissions and backend mode contracts remain readable for older clients. Existing saved Rush selections switch to Flow without importing Rush scores into its board. Players join by choosing a nickname; later online runs obtain a server-issued ticket automatically. Offline starts, tutorial runs, and historical local records do not qualify. The score is the number of matches, including runs with rewarded revives, not progress points or Perfect bonuses.

## Player identity and connectivity

Use the existing Convex deployment and guest credentials. Public builds need `RING_RUSH_CONVEX_URL`; omit `RING_RUSH_CLOSED_TEST_CODE` unless that build also needs invited Daily access. `ProjectBuilder.ConfigureDaily` already creates the ignored connection asset from these variables. Never include deployment keys in a build.

Nicknames contain 3–16 ASCII letters, digits, or underscores and are reserved case-insensitively in a transaction. Settings supports editing the nickname and disabling future participation. Existing scores remain visible when participation is disabled; already issued tickets may finish. A nickname is tied to anonymous credentials, not a recoverable account. Reinstalling or losing credentials may lose access to it.

The client persists a request ID before requesting a run ticket and falls back to unranked local play if online start cannot finish promptly. Completed summaries queue durably and retry on return or when opening the board. An unfinished run cannot resume after restarting the app. Local progress does not depend on successful upload.

## Unity HTTP contract

Use the existing Convex JSON query/mutation envelope and bearer-token transport. Mode is `flow` or `rush`. Timestamps are UTC epoch milliseconds. All endpoints below require a signed-in guest; public guests do not need a Daily invitation.

| Function | Kind | Arguments | Result |
| --- | --- | --- | --- |
| `leaderboard:profile` | query | `{}` | Profile or null |
| `leaderboard:setProfile` | mutation | `{nickname,participating}` | Profile |
| `leaderboard:start` | mutation | `{mode,requestId,clientRulesRevision:1}` | Run ticket |
| `leaderboard:submit` | mutation | `{runId,score,revives,elapsedMs}` | Run ticket |
| `leaderboard:status` | query | `{runId}` | Run ticket |
| `leaderboard:board` | query | `{mode,day:"today"\|"yesterday"}` | Board |

Profile: `{nickname,participating}`. Ticket: `{runId,mode,date,status,startedAt,uploadDeadline,score,reason}`. Status is `open`, `accepted`, `rejected`, or `expired`; score and reason are nullable. Board: `{date,mode,participants,provisional,enabled,entries,personal}`. Each entry is `{nickname,score,rank,isMe}`; personal is null when the caller has no eligible best. No guest authentication IDs are exposed in entries.

Only the best accepted score per guest/mode/day contributes to ranking. Equal scores share competition rank (1, 1, 3); ties display by earliest achievement, then stable record identity. The first 100 entries are shown even if a tie crosses the boundary. Personal rank is calculated across the entire board.

A ticket belongs to its server start date. New-day starts begin at midnight UTC; the previous day's uploads close at 01:00 UTC. Yesterday remains provisional until then. Accepted scores remain queryable after the deadline so a client can resolve a lost submission response before discarding its queue. Moderation can change boards after their submission window ends.

## Integrity and operations

This is a casual leaderboard. Ownership checks, idempotency, supported-client checks, numeric bounds, elapsed-time plausibility, and rate limits reduce accidental or basic abusive submissions. They do not verify actual gameplay, a unique human identity, or ad completion. Full replay and device attestation remain outside this feature; the stricter Daily public-competition gate is unchanged.

Public leaderboard submissions default to disabled. Deploy backend support first, verify CI and Unity integration, then enable on the intended deployment. Keep development, public beta, and default production data separate; ordinary `convex deploy` targets default production, while the beta deployment uses the separately provisioned deployment environment file described in DAILY_OPERATIONS.md. Do not enable Daily as part of this rollout.

Use internal leaderboard operations (from `backend/`, select the intended deployment explicitly):

| Internal function | Arguments | Effect |
| --- | --- | --- |
| `leaderboard:setEnabled` | `{enabled:false}` | Pause new starts/submissions; `true` enables them |
| `leaderboard:exclude` | `{date,mode,userId,reason}` | Remove that guest's contribution to one board |
| `leaderboard:resetNickname` | `{userId}` | Reset an inappropriate profile name |
| `leaderboard:purge` | `{}` | Run a bounded retention pass |

User IDs for moderation come from the authenticated administrative database view, never from public board entries. Registration uses a global token bucket (1,000/hour, burst 100), because the auth callback exposes no trusted client IP. Monitor saturation: an attacker can consume that shared budget and temporarily prevent other new guests from registering. Per-user starts allow 30/minute, burst 10; submissions allow 60/minute, burst 20; nickname changes allow 5/hour, burst 3. Pausing preserves reads and local gameplay. Review Convex function failures and rejection patterns, and verify board counts/ranks after moderation. Avoid logging credentials or submission bodies.

Run tickets and summaries expire seven days after their upload deadline. Bests expire one year after that deadline. Scheduled bounded purges remove bests from the aggregate before deleting records. Profile/authentication deletion is separate; operator deletion must cover both leaderboard and Daily data.

## Release checks

- Backend CI: naming conflicts, authorization, request retries, improved scores, ties, ranks below the top 100, exclusion, day rollover, upload deadlines, retention, and Daily access isolation.
- Unity: parsing, durable queues, opt-in/offline behavior, terminal-only submission, revive continuation, and safe-area layout.
- Device: production-shaped connectivity, Keychain persistence, native nickname keyboard, scroll/navigation, resume/retry, and rewarded-ad continuation.
- Publish updated privacy disclosures for public nicknames and regular-run summaries before public enablement. Device and public-policy checks are release tasks; do not infer they passed from backend CI.

# Ring Rush Daily backend

The isolated Convex project is **ring-rush**. Unity is the client; this package stores canonical player identities and run summaries, and delivers optional usage analytics to PostHog. Firebase and Mixpanel are not used. See [analytics contracts and operations](../docs/ANALYTICS.md).

## Development

Use Node 22.18 or later. From this directory:

```sh
npm ci
npx convex dev
npm run check
```

Link an existing `ring-rush` project when prompted. Keep `.env.local` and deployment keys out of Git. Generated bindings in `convex/_generated` are committed. The backend's fixture test also reads `../Assets/Tests/EditMode/Core/DailyFixtures.json`, generated from the actual C# rules.

Configure these **deployment** environment variables independently for development and production:

- `JWT_PRIVATE_KEY`, `JWKS`, `SITE_URL`: Convex Auth configuration. `auth.config.ts` uses Convex's `CONVEX_SITE_URL` issuer.
- `RING_RUSH_CLOSED_TEST_CODE`: code distributed only with the closed-test client. A matching code grants closed-test Daily access. Public Flow/Rush guests register without a code and do not receive Daily access.
- `RING_RUSH_CLOSED_TEST_EPOCH`: optional, defaults to `1`. Increment alongside a code rotation to revoke existing guest access. Every private Daily API checks the user's stored epoch.

The test code is an invitation gate, not a deployment/admin key or a device-integrity guarantee. The schema fixes `publicCompetitionEnabled` to `false`. Opening public competition requires implementation and review of App Attest and abuse controls; there is deliberately no public-enable toggle.

After the first development deploy:

```sh
npx convex run publication:ensureUpcoming '{}'
npm run smoke
```

`smoke` is restricted to the configured **development** deployment. It creates a guest and a three-point test standing, confirms anonymous auth/refresh, chunk retries, the actual durable validation workflow and personal standing, then signs out. It never prints tokens or the test code. Development scores are never imported to production.

## Unity HTTP contract

Call `POST <CONVEX_URL>/api/query`, `/api/mutation`, or `/api/action` with JSON `{ "path": "daily:current", "args": {}, "format": "json" }`. Private calls use `Authorization: Bearer <token>`. Responses use Convex's `{status:"success",value:...}` or `{status:"error",errorMessage,errorData}` envelope.

Anonymous sign-in is action `auth:signIn` with `{provider:"anonymous",params:{closedTestCode}}`; refresh is the same action with `{refreshToken}`. Both return `{tokens:{token,refreshToken}}`. Store refresh credentials in native Keychain and never store deployment admin keys in Unity. Sign-out is action `auth:signOut` with `{}` and the current access token.

| Function | Kind | Arguments | Result |
|---|---|---|---|
| `daily:current` | query | `{}` | Current challenge or null; never future seeds |
| `daily:createAttempt` | mutation | `{challengeId,requestId,clientRulesRevision:3}` | Attempt |
| `daily:appendChunk` | mutation | `{attemptId,index,events}` | Attempt |
| `daily:finalize` | mutation | `{attemptId,chunkCount}` | Attempt |
| `daily:attemptStatus` | query | `{attemptId}` | Attempt |
| `daily:myStanding` | query | `{challengeId}` | Personal standing |

`requestId` is an installation-generated UUID, persisted before starting. A retry returns the existing attempt. A request ID cannot be reused for another challenge or for a legacy attempt created without a supported client rules revision.

Challenge: `{id,date,seed,rulesVersion,variant,opensAt,closesAt,uploadDeadline,serverNow,rankedEnabled,publicCompetitionEnabled}`. Variant is lowercase `lively` or `still`; all absolute times are UTC epoch milliseconds. V1 requires client revision 2 or 3; v2 requires revision 3. Unsupported revisions return `UPDATE_REQUIRED` throughout the attempt lifecycle. New clients support both published challenge versions. V1 retains its original inactive-puck `abandon` rule; v2 records an inactive-color `drop` as a losing move.

Attempt: `{attemptId,challengeId,status,nextChunkIndex,score,reason,uploadDeadline}`. Status is `open`, `validating`, `accepted`, `rejected`, or `expired`; score and reason are nullable.

Standing: `{challengeId,date,variant,bestScore,participants,percentile,topPercent,provisional,early,waiting,excluded}`. Score and percentile values may be null. One best per guest contributes to Aggregate. Percentile is `100 * (lower + 0.5 * tied) / participants`; top percent is its rounded-up complement with a minimum display of 1%. Fewer than 20 participants is early; one participant waits for more. Excluded entries do not contribute.

Trace event: `{kind,round,tMs,elapsedMs,color,xQ,yQ}`. All fields are required. Kind is `drop`, `timeout`, `pause`, `resume`, `abandon`, or (v2 only) `revive`; unused color is -1 and unused coordinates are zero. `round` is the pre-event score. `tMs` is monotonic run wall time (ceil); `elapsedMs` is active round time (floor), excluding pauses and board transitions. Positions use 1,000 units per Unity board coordinate. The fixed outer radius is 63,500 and Perfect radius is 22,225. A two-millisecond wall-time tolerance covers integer rounding; it never expands the timer or hit geometry.

Upload sequential chunks of 1–128 events, indices starting at zero. Identical retries are accepted; a reused index with changed contents is rejected. Up to 512 chunks and 25 hours are supported per attempt to bound resource use. Finalize only after uploading a terminal drop, timeout, or voluntary abandon. In v2 a loss with banked revives is pending, not terminal: append `revive` or `abandon` before finalization. A revive has `elapsedMs:0`, `color:-1`, and zero coordinates, resets the same target timer, and starts a fixed 3000ms countdown. The bank earns at scores 20, 50, 100, then every 50; it holds up to three and discards overflow. Server replay derives the bank independently. SDK reward callbacks authorize client revives; replay does not independently verify ad completion. The server derives the score and runs bounded replay steps through Workflow; the client must show pending while status is `validating`.

New ranked attempts close at midnight UTC. Upload and finalize must reach the server before 01:00 UTC. Already accepted/validating attempts remain queryable afterward, so resolve uncertain responses before pruning an upload queue. Timely submitted workflows may finish just after the deadline; standings remain provisional until they settle and the minute-level sealing task freezes them. Offline and late runs retain local progress but cannot be newly ranked.

## Daily v2 rollout

Deploy v1/v2 backend support before distributing the revision-3 client. New publication uses v2 only for previously unpublished dates; never rewrite published challenge rows or accepted standings. With seven days already published, revive-enabled Daily may start up to eight days later. Existing revision-2 v1 attempts, upload retries, and journaled validation workflows remain supported. Older missing/revision-1 attempts remain ineligible; preserve their local earnings and readable historical accepted results rather than backfilling their revision.

V2 permits a banked revive after a loss, without advancing randomness, geometry sequence, or score. While a decision is pending, only revive or abandon is valid. The active timer excludes advertising, countdown, and pause time. Both runtimes consume the shared `ReviveFixtures.json` action scenarios, alongside the unchanged v1 geometry fixtures. Public competition remains disabled: ad server callbacks and client integrity verification are separate future work.

## Operations and retention

- Cron at **23:50 UTC** ensures today plus seven days ahead. Existing published definitions are immutable. Daily board style alternates by UTC day index.
- `operations:setRankedEnabled {"enabled":false}` is an internal kill switch for new starts/uploads/finalize; regular local gameplay and reading existing results remain available.
- `operations:excludeStanding {challengeId,userId,reason}` is internal and removes a guest's Aggregate contribution. It refuses changes after final sealing, preserving frozen standings.
- `operations:sealDue {}` waits for pending validations before sealing. `operations:expireOpen {}` marks expired unfinished uploads.
- `GET <CONVEX_SITE_URL>/health/daily` is non-sensitive and returns HTTP 503 when today/tomorrow is missing or validation has been pending for over five minutes. `rankedEnabled:false` is reported independently, allowing intentional maintenance. No seed, identity, score, or secret is returned.
- Raw trace chunks and attempt metadata expire seven days after validation completes (unfinished attempts: seven days after the upload deadline). Completed workflow journals are also removed after seven days. Standing records expire one year after the upload deadline; challenge definitions follow two days later. Bounded purge batches schedule continuation until cleared.

Review Convex failed-function/workflow logs and the health-check alert before reopening ranked submissions. Operational logs do not contain raw traces, credentials, or analytics events. App Store privacy disclosures should cover anonymous guest identifiers and submitted gameplay/standing data. Cross-device identity and progression are not provided in this release.

## Public casual Flow/Rush leaderboards

These use independent profiles, run tickets, best scores, and ranking controls. They do not change Daily's replay validation or invitation checks. See [leaderboard operations and contract](../docs/LEADERBOARDS.md) for endpoints, release configuration, ranking rules, moderation, and retention.

Anonymous sign-in without a code creates a public guest. A valid invitation additionally grants Daily access; an invalid nonempty invitation is rejected. Existing guest sessions remain valid for the new leaderboard. Nicknames do not add recovery or cross-device accounts.

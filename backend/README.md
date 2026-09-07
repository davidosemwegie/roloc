# Ring Rush Daily backend

The isolated Convex project is **ring-rush**. Unity is the client; this package contains no analytics, Firebase, or Mixpanel integration.

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
- `RING_RUSH_CLOSED_TEST_CODE`: code distributed only with the closed-test client. No new anonymous registration works without a matching code.
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
| `daily:createAttempt` | mutation | `{challengeId,requestId,clientRulesRevision:2}` | Attempt |
| `daily:appendChunk` | mutation | `{attemptId,index,events}` | Attempt |
| `daily:finalize` | mutation | `{attemptId,chunkCount}` | Attempt |
| `daily:attemptStatus` | query | `{attemptId}` | Attempt |
| `daily:myStanding` | query | `{challengeId}` | Personal standing |

`requestId` is an installation-generated UUID, persisted before starting. A retry returns the existing attempt. A request ID cannot be reused for another challenge or for a legacy attempt created without client rules revision 2.

Challenge: `{id,date,seed,rulesVersion,variant,opensAt,closesAt,uploadDeadline,serverNow,rankedEnabled,publicCompetitionEnabled}`. Variant is lowercase `lively` or `still`; all absolute times are UTC epoch milliseconds. Challenge rules version is currently 1. The separate `clientRulesRevision` gate is exactly 2: it requires inactive-puck releases to end the attempt, represented by the existing `abandon` terminal event. Missing, older, or newer revisions return `UPDATE_REQUIRED` before returning any idempotent start.

Attempt: `{attemptId,challengeId,status,nextChunkIndex,score,reason,uploadDeadline}`. Status is `open`, `validating`, `accepted`, `rejected`, or `expired`; score and reason are nullable.

Standing: `{challengeId,date,variant,bestScore,participants,percentile,topPercent,provisional,early,waiting,excluded}`. Score and percentile values may be null. One best per guest contributes to Aggregate. Percentile is `100 * (lower + 0.5 * tied) / participants`; top percent is its rounded-up complement with a minimum display of 1%. Fewer than 20 participants is early; one participant waits for more. Excluded entries do not contribute.

Trace event: `{kind,round,tMs,elapsedMs,color,xQ,yQ}`. All fields are required. Kind is `drop`, `timeout`, `pause`, `resume`, or `abandon`; unused color is -1 and unused coordinates are zero. `round` is the pre-event score. `tMs` is monotonic run wall time (ceil); `elapsedMs` is active round time (floor), excluding pauses and board transitions. Positions use 1,000 units per Unity board coordinate. The fixed outer radius is 63,500 and Perfect radius is 22,225. A two-millisecond wall-time tolerance covers integer rounding; it never expands the timer or hit geometry.

Upload sequential chunks of 1–128 events, indices starting at zero. Identical retries are accepted; a reused index with changed contents is rejected. Up to 512 chunks and 25 hours are supported per attempt to bound resource use. Finalize only after uploading a terminal drop, timeout, or voluntary abandon. The server derives the score and runs bounded replay steps through Workflow; the client must show pending while status is `validating`.

New ranked attempts close at midnight UTC. Upload and finalize must reach the server before 01:00 UTC. Already accepted/validating attempts remain queryable afterward, so resolve uncertain responses before pruning an upload queue. Timely submitted workflows may finish just after the deadline; standings remain provisional until they settle and the minute-level sealing task freezes them. Offline and late runs retain local progress but cannot be newly ranked.

## Client revision 2 rollout

Coordinate this backend release with the Unity client that sends `clientRulesRevision:2`. Deploying the backend first deliberately prevents older clients from starting ranked runs; regular modes and local earnings remain available. Existing challenge definitions, deterministic geometry, trace protocol, and already accepted standings remain unchanged. This is a closed-test compatibility gate, not proof of client integrity or a replacement for App Attest.

Attempts created before this gate cannot be resumed via a reused request ID, appended to, or finalized, including identical upload retries. They return `UPDATE_REQUIRED`; the client should keep local earnings and start a fresh attempt after updating. Any old workflow still validating at deployment is rejected before adding a standing. The status query reports legacy open or validating attempts as rejected without mutating them, allowing upgraded clients to drain queued uploads. Accepted historical results remain readable. The schema field is optional to accommodate existing rows, and must not be backfilled: an absent revision identifies an attempt whose input rules were not verified at entry.

## Operations and retention

- Cron at **23:50 UTC** ensures today plus seven days ahead. Existing published definitions are immutable. Daily board style alternates by UTC day index.
- `operations:setRankedEnabled {"enabled":false}` is an internal kill switch for new starts/uploads/finalize; regular local gameplay and reading existing results remain available.
- `operations:excludeStanding {challengeId,userId,reason}` is internal and removes a guest's Aggregate contribution. It refuses changes after final sealing, preserving frozen standings.
- `operations:sealDue {}` waits for pending validations before sealing. `operations:expireOpen {}` marks expired unfinished uploads.
- `GET <CONVEX_SITE_URL>/health/daily` is non-sensitive and returns HTTP 503 when today/tomorrow is missing or validation has been pending for over five minutes. `rankedEnabled:false` is reported independently, allowing intentional maintenance. No seed, identity, score, or secret is returned.
- Raw trace chunks and attempt metadata expire seven days after validation completes (unfinished attempts: seven days after the upload deadline). Completed workflow journals are also removed after seven days. Standing records expire one year after the upload deadline; challenge definitions follow two days later. Bounded purge batches schedule continuation until cleared.

Review Convex failed-function/workflow logs and the health-check alert before reopening ranked submissions. Operational logs do not contain raw traces, credentials, or analytics events. App Store privacy disclosures should cover anonymous guest identifiers and submitted gameplay/standing data. Cross-device identity and progression are not provided in this release.

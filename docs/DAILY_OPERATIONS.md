# Ring Rush Daily operations

Unity is the repository root; the isolated Convex package is `backend/`. The Convex project is **ring-rush**. Development, automated tests, and closed production testing use separate data. Firebase, Mixpanel, and analytics are not part of this runtime.

## Deployment and configuration

Use Node 24 and the committed lockfile. Run these commands from `backend/`:

```sh
npm ci
npx convex dev --configure existing
npm run check
npx convex dev --once
npx convex run publication:ensureUpcoming '{}'
npx convex run operations:health '{}'
```

Select `ring-rush` during configuration; create that project only if it is absent from the account. `.env.local` is private. The [Convex CLI](https://docs.convex.dev/cli/overview) targets development for `dev` and `run`; `deploy` targets the associated production deployment, and `run --prod` explicitly selects production.

Configure these values separately on each deployment, using the dashboard or interactive `npx convex env set NAME` (`--prod` for production). Do not put values into source, terminal transcripts, screenshots, or CI logs:

| Variable | Purpose |
| --- | --- |
| `JWT_PRIVATE_KEY` | Deployment-specific authentication signing key |
| `JWKS` | Corresponding public verification keys |
| `SITE_URL` | Site origin required by the authentication setup |
| `RING_RUSH_CLOSED_TEST_CODE` | Invitation code shared with the closed-test build |
| `RING_RUSH_CLOSED_TEST_EPOCH` | Guest access generation; defaults to `1` |

Follow [Convex Auth setup](https://labs.convex.dev/auth/setup) for the key pair. `CONVEX_SITE_URL` is supplied by Convex and is the issuer used by `auth.config.ts`. Administrative deploy keys never belong in Unity.

The private Unity asset `Assets/Resources/DailyConnection.asset` contains the public `.convex.cloud` deployment URL and the invitation code. ProjectBuilder can populate it from `RING_RUSH_CONVEX_URL` and `RING_RUSH_CLOSED_TEST_CODE`; the asset and its metadata are ignored. This code is extractable from the distributed app and is a closed-test admission check, not public anti-cheat. Tokens and refresh credentials are stored in iOS Keychain, scoped to the deployment; the Editor retains credentials in memory. Local Daily caches are also scoped to the deployment.

After development checks pass, deploy production deliberately:

```sh
npx convex deploy
npx convex run --prod publication:ensureUpcoming '{}'
npx convex run --prod operations:health '{}'
```

Production testing must use a build configured for the production URL. Do not replay automated test traces there. Rotating the invitation code prevents new sign-ins with an old code; also increment `RING_RUSH_CLOSED_TEST_EPOCH` to revoke existing guest access. Coordinate that change with a replacement closed-test build.

## Publication, deadlines, and health

The 23:50 UTC cron fills today and seven upcoming UTC dates idempotently. Existing definitions never change. A challenge opens at 00:00 UTC, stops admitting attempts the next midnight, and accepts completed uploads until 01:00 UTC. Timely submissions may finish validation after that deadline. The minute-based sealing job waits for pending validation before making standings final.

A daily best contributes once per guest; improving it replaces the previous score. Ties use midrank percentiles. Guest identity is not proof of a unique person.

`GET https://<deployment>.convex.site/health/daily` is public and returns only availability flags. HTTP 503 means today or tomorrow is missing. It does not reveal unpublished seeds, player IDs, scores, traces, or credentials.

Set the GitHub repository variable `RING_RUSH_DAILY_HEALTH_URL` to that endpoint (a same-named secret is also accepted). `Daily availability` checks it twice an hour and supports manual dispatch. The workflow intentionally fails when configuration is missing. Scheduled Actions run from the default branch and can be delayed; Convex's own cron remains the publisher. Repository maintainers must enable Actions failure notifications and verify a deliberate failing check reaches them before relying on it as an alert.

For a missing challenge, inspect the Convex cron/function logs, run `publication:ensureUpcoming`, and recheck health. Monitor failed Workflow validations and long-running `validating` attempts; availability alone does not establish validation health. Never log authentication tokens or full request traces.

## Pause ranking and investigate

From `backend/`, pause production ranking with:

```sh
npx convex run --prod operations:setRankedEnabled '{"enabled":false}'
```

This prevents starts, uploads, and finalization requests while ordinary play and cached practice remain available. It does not cancel work already accepted for validation. Health reports ranking as intentionally paused while still checking publication. Re-enable with `{"enabled":true}` after the incident is resolved.

Before a challenge seals, `operations:excludeStanding` accepts `challengeId`, `userId`, and a brief `reason`; it removes an excluded best from the aggregate. Resolve the IDs in the selected deployment dashboard and record a specific reason. Finalized standings are immutable. No public client endpoint may invoke administrative functions.

A bad published rules version requires a new version for future dates and a compatible app build. Do not edit a live challenge or reuse its version for different behavior. Pause rankings if the current version cannot be validated fairly.

## Retention and release gate

The cleanup cron runs at 02:15 UTC. Accepted/rejected attempt records and raw trace chunks expire seven days after validation finalization; unsubmitted attempts use their configured expiry. Daily bests expire one year after the upload deadline. Challenge metadata is removed afterward. Workflow cleanup removes completed internal validation state. Verify these jobs in development with expired records before treating retention as operationally proven.

Guest authentication/session records have their own lifecycle and are not covered by the trace/standing purge. Document the operator deletion process and verify provider log/backup retention before public launch; see `PRIVACY.md`.

`publicCompetitionEnabled` is hard-coded false in the backend contract. Before removing that gate: integrate and verify App Attest, test suspicious-submission exclusion, exercise rate limits and token revocation, and verify publication/validation alerting. Legal replay validation cannot distinguish genuine touches from a script that produces legal events.

## Verification ownership

`Backend checks` runs `npm ci`, TypeScript checks, and Vitest on pushes and pull requests. It reads the shared canonical fixture at `Assets/Tests/EditMode/Core/DailyFixtures.json` from the full checkout; no deployment keys are needed. GitHub actions are pinned to reviewed release commits.

Unity license provisioning and native signing are not configured in hosted CI. Unity EditMode/PlayMode and physical iPhone checks remain an integration gate for gameplay, rendering, saves, audio, Keychain, and sharing. See `TESTFLIGHT.md` for the native release process. Passing backend CI alone does not verify the app or imply a TestFlight upload.

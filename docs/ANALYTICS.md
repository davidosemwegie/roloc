# Player history and usage analytics

[Ring Rush PostHog project](https://us.posthog.com/project/600627/) is in **CJVS**, with UTC dates. [Game health & leaderboards dashboard](https://us.posthog.com/project/600627/dashboard/2078262) contains ten saved insights: daily/weekly active players, completed runs by mode, median/p90 score, average elapsed time, completed/abandoned runs, tutorial completion, leaderboard activation, rewarded revive conversion, ranked submission outcomes, and 14-day retention (including D1/D7). Development events are excluded by default. Funnel conversion is per player; it is not an exact per-run ad delivery audit.

## Identity and storage

Convex `users` is canonical. Public anonymous registration reuses the installation's existing Convex Auth credentials in Keychain and does not grant closed-test Daily access. PostHog `distinct_id` is `<CONVEX_SITE_URL>:<users._id>`; changing the public nickname leaves it unchanged. Credentials lost or a different deployment/device imply a new identity; no recovery, aliasing, or cross-device merging is promised. The client never sends a claimed user ID for authorization.

`runHistory` keeps each run under `(userId, clientRunId)` with mode, timestamps, match score, revives, elapsed milliseconds, and started/completed/abandoned status. Final results are immutable and retries are idempotent. Owned leaderboard/Daily links retain separate `rankingStatus` and `rankedScore` fields; client history does not change a board score. Profiles and histories persist until explicit deletion. Tickets and daily bests keep their independent retention policies.

History includes unranked play and survives disabling optional analytics. A run finishes only after the revive decision; navigation out records abandonment. Backgrounding snapshots the active run, and a restarted process recovers an unfinished run as abandoned using its last saved counters. History is best effort: the atomic deployment-specific local queue holds 256 items for seven days and prioritizes run summaries over optional events when full. Long offline use, storage/credential loss, or rejected payloads can leave missing history. Active run snapshots are not replay logs.

## Validated API

All endpoints require the existing shared authenticated transport and explicit argument/return validators.

| Endpoint | Arguments | Response |
| --- | --- | --- |
| `telemetry:identity` query | `{}` | `{playerId, analyticsEnabled}` |
| `telemetry:setAnalyticsEnabled` mutation | `{enabled: boolean}` | `{playerId, analyticsEnabled}` |
| `telemetry:recordRuns` mutation | `{runs: RunSummary[]}` (1–20) | `{recorded, duplicates}` |
| `telemetry:capture` mutation | `{eventId, name, occurredAt, sessionId, mode, clientRunId}` | `null` |

`RunSummary` has `clientRunId`, `mode` (`flow/rush/daily`), `startedAt`, `endedAt`, `score`, `revives`, `elapsedMs`, `status` (`started/completed/abandoned`), `leaderboardRunId`, and `dailyAttemptId`, plus optional `analyticsEnabled` captured with the run. A false value keeps history without sending usage analytics even after re-enabling. Use empty strings for absent links. Started rows have zero end/result counters. Timestamps are integer Unix milliseconds, at most seven days old or five minutes ahead; finite bounded integer counters and linked ownership/mode are checked. IDs are 8–128 ASCII letters/digits/underscore/hyphen; client run IDs also allow the colon used by the game's installation-and-counter format. Run writes and client events have independent per-user token buckets; duplicate retries do not consume another token.

## Events

| Event | Source / purpose |
| --- | --- |
| `game_opened` | Launch and foreground after 30 minutes away; activity |
| `tutorial_started`, `tutorial_completed` | Tutorial funnel |
| `run_started`, `run_completed`, `run_abandoned` | Idempotent history transitions; volume, balance, retention |
| `leaderboard_viewed` | Public leaderboard entry |
| `leaderboard_joined`, `leaderboard_profile_updated` | Server profile transitions; first opt-in only emits joined |
| `revive_offered`, `revive_requested`, `revive_completed` | Client reward flow; completion requires SDK reward callback |
| `leaderboard_score_accepted`, `leaderboard_score_rejected`, `leaderboard_score_expired` | Server ranking transitions, including Daily |

Properties include `mode`, `score`, `revives`, `elapsed_ms`, `client_run_id`, `session_id`, `run_id`, `status`, `participating`, `environment`, and `source` as applicable. Sources distinguish `client`, `client_summary`, and `server_validation`. Duration includes pauses/ads. No credentials, nicknames, raw traces, advertising IDs, or arbitrary client properties are captured. Casual accepted scores still use plausibility checks; accepted does not mean independently verified gameplay. Expired counts include unfinished tickets and missed deadlines.

## Deployment and operations

Set these Convex environment variables on each intended deployment, keeping the capture token out of Unity assets and Git:

- `POSTHOG_PROJECT_TOKEN`: Ring Rush project capture token.
- `POSTHOG_HOST`: `https://us.i.posthog.com` (only US/EU PostHog ingestion hosts accepted).
- `RING_RUSH_ENVIRONMENT`: `development` for dev and `beta` for the isolated beta; use `production` only for a later production deployment.

Without valid PostHog configuration, history is still saved and optional events are not later backfilled. Delivery runs every minute through a bounded transactional outbox, leased batches, retries, stable UUID/timestamp/identity, and consent revision checks. Delivered payloads are deleted; pending payloads expire at seven days and deduplication receipts at eight days. `telemetryDelivery:deliver` can be invoked internally for a smoke test. Failures log no payloads or secrets. Inspect oldest due outbox rows and failure logs if charts stop advancing; dashboard emptiness alone is not proof of no activity.

Keep public leaderboard submissions disabled until integrated device validation. Existing Daily controls remain independent. Follow [DAILY_OPERATIONS.md](DAILY_OPERATIONS.md) for the isolated beta deployment rather than deploying to the project's unrelated default production.

`telemetryDelivery:deletePlayerHistory {userId}` is an internal, bounded deletion operation that disables analytics and removes run history, pending payloads, and receipts. It does not delete auth sessions, public/Daily profiles or scores, aggregates, or events already delivered to PostHog. Complete requests need those separate actions. Confirm PostHog/provider retention and publish updated privacy disclosures before distribution.

Backend CI covers ownership, retry transitions, identity, consent, outbox leases, retention, and ranking linkage. Unity tests cover offline retry/persistence, opt-out, run lifecycle, revive/tutorial events and navigation. On an iOS device verify Keychain identity after restart, keyboard nickname entry, offline recovery, analytics toggle persistence, background/killed-run handling, safe areas and rewarded completion. CI cannot establish these native behaviors.

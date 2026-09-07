# Ring Rush TestFlight delivery

These are release instructions, acceptance gates, and dated delivery records. Archive, upload, processing, and invitation outcomes are recorded separately below.

## Prepare the exact version

1. Record the integrated Git commit and successful backend CI run. Use Unity **6000.6.0f1** with iOS Build Support and the project at the repository root.
2. Configure the intended `ring-rush` deployment and its closed-test invitation code using `DAILY_OPERATIONS.md`. Confirm health, upload retry, ranking, and the public-competition gate there. Keep development data separate from production.
3. Supply the public deployment URL through `RING_RUSH_CONVEX_URL`, the invitation code through `RING_RUSH_CLOSED_TEST_CODE`, and a unique increasing integer through `RING_RUSH_BUILD_NUMBER`. Use secure environment injection; never commit the private generated `DailyConnection` asset or print the code.
4. Complete Unity EditMode/PlayMode checks, then a physical-device playthrough. Exercise three Flow chances and recovery, Rush sudden death, repeated colors, canonical moving targets, touch cancellation, hidden-board pause, background/resume, offline ordinary play, save migration, symbols, reduced effects, audio toggles, haptics, ranking retry, and canceled sharing. Check sustained 60 FPS and safe-area readability on the intended iPhone sizes.

## Export and archive

Always export from the current Unity source before building Xcode. Reusing an older export can reinstall an older theme and gameplay code.

From the repository root, with the private environment already configured:

```sh
/Users/david/.unity/bin/unity build . --target iOS --execute-method Roloc.Editor.ProjectBuilder.BuildTestFlight --output-path Builds/iOS --allow-dirty-build --timeout 600 --no-tail --format json
```

`BuildTestFlight` uses the device SDK and `BuildOptions.None`, selects `com.clearjar.ringrush` on team `BK7TPQ53FF`, and requires `RING_RUSH_BUILD_NUMBER`. `BuildIOS` uses `com.clearjar.ringrush.dev`; store export restores this development identity afterward. Ensure the output shown by Unity is the directory opened in Xcode.

Archive the fresh store project with identifier `com.clearjar.ringrush` and Apple team `BK7TPQ53FF`:

```sh
xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination 'generic/platform=iOS' -archivePath Builds/Archives/RingRush.xcarchive -allowProvisioningUpdates DEVELOPMENT_TEAM=BK7TPQ53FF archive
```

In Xcode Organizer, validate the archive and choose distribution to App Store Connect. Confirm the identifier, version, build number, icon, privacy manifest, and selected deployment before upload. Do not upload an app pointing to a developer's test data. Native archive success is separate from successful App Store Connect processing.

## Account steps and closed distribution

The release owner may need to sign in, satisfy two-factor authentication, accept Apple agreements, grant the required App Store Connect role, register the bundle identifier/app record, and enable distribution signing. Those account actions cannot be inferred from a successful local development install.

Provide beta description, what to test, a working feedback contact, and accurate export-compliance information. Create a private tester group and assign the processed build. Start with internal testers; external distribution may require Beta App Review. Share the invitation code/build only with that cohort. Apple documents the upload, group, review, and invitation flow in its [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview).

The owner authorized public TestFlight on September 7, 2026, then explicitly authorized App Store review after listing setup. Public gameplay testing is separate from public Daily competition: keep ranked Daily disabled on the beta deployment until its public-competition gates are complete. Invitations/messages require authorized recipients. See `PRIVACY.md` for the disclosure inventory and unresolved operational checks.

## Acceptance and recorded outcome

Have at least two guests play the same UTC Daily and verify identical initial boards/sequences, one best per guest, tied rankings, a lower score retaining the best, and a better score replacing it. Check provisional labels, an upload crossing midnight, 01:00 UTC expiry, final standings, and result-card labeling. Use the development deployment for synthetic boundary/abuse tests.

Record the archive path, Git commit, bundle version/build number, deployment environment, native validation result, App Store Connect processing state, review state, and whether any tester actually installed it. Report incomplete steps as pending. Backend checks currently run in hosted CI; Unity licensing, device input, Keychain, signing, and TestFlight account steps require separate verification.

A public release additionally requires verified App Attest integration, suspicious-submission review/exclusion, operational alerting, and a tested privacy/deletion process. Keep the closed-test gate until those controls are complete.

## Store registration and identity verification

- App Store Connect draft: [Ring Rush - Match the colors](https://appstoreconnect.apple.com/apps/6809459301/distribution), Apple ID `6809459301`, English (US), SKU `ring-rush-ios`.
- Store `com.clearjar.ringrush` and development `com.clearjar.ringrush.dev` are registered under ClearJar Financial Inc. (`BK7TPQ53FF`). Existing build-8 installation used the legacy identifier recorded below; no device reinstall or save migration accompanied this change.
- An isolated Unity 6000.6.0f1 store export using build number 9 passed. Xcode resolved `PRODUCT_BUNDLE_IDENTIFIER=com.clearjar.ringrush` and team `BK7TPQ53FF`; Unity settings reverted to the dev identity. A separate Configure invocation also passed. No archive, install or upload was made from this verification export; it excluded unfinished advertising changes.
- Six screenshots, listing copy, subtitle, Casual/Action game categories and review contact are saved. Manual release is selected. No review submission or publication is authorized; the owner explicitly requested keeping the draft.
- Public support/privacy URLs, copyright/content rights, final-build privacy and age disclosures, pricing/availability, and alignment of the App Store version with a release binary remain release work. The earlier preview video has not been uploaded. Build 9 has processed successfully for TestFlight.

## TestFlight delivery — build 9 (September 7, 2026)

### Subsequent App Store submission

- The owner authorized App Review and confirmed ownership or distribution permission for all artwork and original recordings. App Information records no third-party content and a 4+ age rating with regional equivalents; copyright is `2026 ClearJar Financial Inc.`.
- Public support form and read-only Google Docs privacy policy are saved in `marketing/app-store/listing.json`. App Privacy responses were published for support and guest-linked ranking data, used for functionality without tracking. No ads/analytics are included in this build; Daily ranked submissions remain disabled.
- Saved free prices across 175 countries/regions and availability on release. The owner's existing **automatic release after approval** selection remains enabled. Reviewer contact and instructions explain offline Flow/Rush, the tutorial, and practice-only Daily.
- On September 7, 2026 at 14:13 UTC, Apple confirmed **1 Item Submitted**: store version **1.0**, processed binary **0.1.0 (9)**. The [submission](https://appstoreconnect.apple.com/apps/6809459301/distribution/reviewsubmissions/details/d62fa954-e2c6-4c4d-8ece-1d694414d344) shows **Waiting for Review**, ID `d62fa954-e2c6-4c4d-8ece-1d694414d344`. This is App Store review, separate from the earlier TestFlight Beta App Review. Approval and public release have not been verified.

### Build and beta history

- Stable source `9f44cf2` freshly exported with Unity 6000.6.0f1 as **0.1.0 (9)** into `Builds/iOS-store9`. Includes the build-8 game, refreshed store assets, and separated store/development identities; unfinished advertising work is excluded.
- Xcode Release archive succeeded at `Builds/Archives/RingRush-store9.xcarchive`. The app resolves to `com.clearjar.ringrush`, team `BK7TPQ53FF`, iPhone only. Deep, strict signature verification passed. Unity settings returned to `com.clearjar.ringrush.dev`.
- `xcodebuild -exportArchive` successfully uploaded the package to App Store Connect at 13:24 UTC. Apple processing completed and internal testing is active. Upload produced a non-blocking missing-dSYM warning for `UnityRuntime.framework`; Unity runtime crash symbolication may be incomplete.
- Created **Ring Rush Private Beta** and assigned build 9. Automatic distribution is disabled so later uploads must be assigned deliberately. The internal group now contains the owner's two accounts; Apple displayed **Invited** for the Gmail account. No successful TestFlight installation has been verified.
- Following the owner's public-beta request, created **Ring Rush Public Beta** and submitted build 9 to **Beta App Review**. Apple reports **Waiting for Review**. Public link: https://testflight.apple.com/join/uBd2Ca49. Apple explicitly states testers cannot join until this group has an approved build. No App Store review or publication was submitted.
- Beta description, feedback/contact details, review instructions and build testing notes are saved. They accurately describe Daily as practice-only, with no ads or purchases.
- Independent source inspection found that `publicCompetitionEnabled=false` is descriptive, while the compiled invitation code admits guests to isolated beta rankings. The coordinator verified client and backend paths, then used the existing `operations:setRankedEnabled` mutation to pause rankings on **affable-lyrebird-62 only**. `daily:current` confirmed `rankedEnabled=false`; Flow, Rush, local progress and Daily practice remain available. Existing standings are preserved; already accepted validation can finish. Development and default production were not changed.
- Source/build-number CI passed. Archive/signature checks and the live challenge response provide validation beyond CI; physical-device acceptance is still pending. Public privacy URLs and operational privacy procedures remain unfinished release work.

## Current device delivery record — build 8

- Source `f9a12df` freshly exported from the full root project with Unity 6000.6.0f1 as **0.1.0, build 8**. This includes the badge collection and Perfect feedback omitted from build 7, together with persistent palettes and combined movement. Export: `Builds/iOS-build8`; Xcode Release archive: `Builds/Archives/RingRush-build8.xcarchive`.
- Adds 12 run-score and nine lifetime badges, historical backfill, persistent unlocks, a menu collection, and inline results badges. Perfect matches have a short double ripple and distinct native haptic. Board resets and palette changes clear lingering Perfect feedback.
- Parallel persistence review verified ordered atomic credit and duplicate suppression; the integrated presentation review checked effect cleanup and compact/tall badge captures. All 174 EditMode tests passed (one explicit network test skipped) and all 13 focused PlayMode tests passed, including the existing movement, Daily fixtures, input, and new badge/feedback coverage. Backend CI passed.
- The archive passed deep, strict signing verification for `com.osazi.roloc.unitydev`. Build 8 installed and launched wirelessly on the paired iPhone; CoreDevice confirmed its new process running.
- Direct device install only. Native haptic feel, extended physical play, and sustained FPS still require hands-on acceptance. No backend redeployment or TestFlight upload was performed.

## Previous delivery record — build 7

- Source `210cbe3` freshly exported with Unity 6000.6.0f1 as **0.1.0, build 7** from an isolated checkout that excluded concurrent unfinished badge work. The retained export is `Builds/iOS-build7`; the successful Xcode Release archive is `Builds/Archives/RingRush-build7.xcarchive`.
- Shifted palettes now persist through recovery, calm, and movement episodes until the next color shift or a new run. Floating pucks plus drifting rings are eligible from 20 matches; puck orbit plus ring drift and ring orbit plus floating pucks are eligible from 60. Each group has one compatible movement effect, and the existing recovery/calm rhythm remains.
- Verification: 170 EditMode tests passed (one explicit network test skipped), including unchanged Daily fixtures; all 11 focused PlayMode checks passed. Combined-motion checks cover both groups moving, full movement sweeps, compact/tall geometry with cosmetics, persistent palettes, pauses/retries, and actual frame-driven dragging. Rendered captures were inspected. Backend CI passed.
- Integration review verified the core scheduling contribution against the presentation consumers and confirmed that the isolated build matched all committed source changes. Extra clearance and bounded drift keep combined layouts separated.
- Deep, strict code-sign verification passed for `com.osazi.roloc.unitydev`. Build 7 installed and launched wirelessly on the paired iPhone; CoreDevice verified the process under the new installation path.
- This was a direct device install, not a TestFlight upload. Physical shade perception, extended touch play, and sustained on-device FPS remain hands-on acceptance items. Daily definitions and backend were unchanged.

## Previous delivery record — build 6

- Source `ca064b7` freshly exported with Unity 6000.6.0f1 as **0.1.0, build 6**. Export used an isolated checkout to exclude another task's unfinished badge edits. The retained export is `Builds/iOS-build6`; the successful Xcode Release archive is `Builds/Archives/RingRush-build6.xcarchive`.
- Deep, strict code-sign verification passed for `com.osazi.roloc.unitydev`. Build 6 installed and launched wirelessly; CoreDevice verified the running process under the new installation path.
- Includes curated Color Shift palettes from 20 matches, single-group orbits from 40, and dual orbits from 60. The Still selector is removed; regular runs use Lively while historical Still records remain saved. There are four puck/ring pairs throughout. Published Daily definitions and backend were unchanged.
- Verification: 155 EditMode tests passed (one explicit network test skipped), including unchanged Daily fixtures; 12 PlayMode checks passed, including actual frame-driven dragging, transition timing, orbit bounds, compact/tall captures, and removal of the Still selection without deleting its record. Backend CI passed. The integration review caught and corrected an extra palette transition delay, then verified the combined behavior.
- This was a direct device install, not a TestFlight upload. Shade perception, extended physical touch play, and sustained on-device FPS remain hands-on acceptance items.

## Previous delivery record — build 5

- Source `bcc7817` was freshly exported with Unity 6000.6.0f1 as version **0.1.0, build 5**. Xcode Release archive succeeded at `Builds/Archives/RingRush.xcarchive`; deep, strict code-sign verification passed for `com.osazi.roloc.unitydev`.
- Build 5 installed and launched wirelessly on the paired iPhone. CoreDevice confirmed the new installation's RingRush process running.
- Includes the simplified results screen with open colored record typography and cosmetic progress, circular puck touch boundaries, and client rules revision 2. Two focused Unity layout/capture tests and backend CI passed before export.
- Deployed the matching revision gate to Clearjar Studio's isolated closed-test deployment (`affable-lyrebird-62`). Its health endpoint reports ready, today's and tomorrow's challenges available, no stalled validation, and public competition disabled. Development and default production were not redeployed.
- This is a direct device install; no new TestFlight distribution attempt was made. The account and hands-on acceptance gates below remain pending.

## Previous delivery record — build 4

- Unity 6000.6.0f1 freshly exported version **0.1.0, build 4**, using `BuildTestFlight` and the isolated Clearjar Studio closed-test deployment (`affable-lyrebird-62`). Production standings remain separate and ranked production remains disabled.
- Xcode **Release archive succeeded** at `Builds/Archives/RingRush.xcarchive`. Code-sign verification passed for `com.osazi.roloc.unitydev`.
- Build 4 installed and launched successfully on the paired iPhone over the local network. CoreDevice confirmed the RingRush process remained running. The first launch command timed out; a subsequent launch succeeded.
- Unity: 134 ordinary EditMode tests passed (one explicit network test skipped in the unfiltered suite). PlayMode checks passed across focused runs, including a real 45-match rendered/input Daily run accepted by Convex with a transition pause/resume. Actual UI captures were inspected for menu, board, collection, pause, and results.
- Build 4 adds faint inactive pucks, inactive-puck release penalties, and a measured results stack that adapts to the safe area. The integrated change passed 73 focused Unity rule tests and six PlayMode touch/layout/capture tests, including compact and tall iPhone safe areas. Rendered captures were inspected.
- GitHub backend and Daily health workflows passed. Scheduled Convex checks are live every five minutes; GitHub cron scheduling begins when the workflow reaches the default branch.
- TestFlight distribution was attempted and stopped by Xcode with **“No Accounts with App Store Connect Access.”** No TestFlight upload or tester invitation was completed. An authorized App Store Connect account must be added in Xcode before retrying the existing archive export.
- Physical-device audio/haptic perception, accessibility preferences, extended offline play, repeated relaunches, native sharing, and sustained FPS still need a hands-on acceptance pass. Installation and Editor tests do not establish those results.

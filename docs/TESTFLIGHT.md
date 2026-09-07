# Ring Rush closed TestFlight delivery

These are release instructions and acceptance gates. This document does not assert that an archive was uploaded, Apple approved a build, or testers were invited.

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

`BuildTestFlight` uses the device SDK and `BuildOptions.None` and requires `RING_RUSH_BUILD_NUMBER`. `BuildIOS` remains the development export. Ensure the output shown by Unity is the directory opened in Xcode.

Archive the fresh project with the existing identifier `com.osazi.roloc.unitydev` and Apple team `TYU4JMX349`:

```sh
xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination 'generic/platform=iOS' -archivePath Builds/Archives/RingRush.xcarchive -allowProvisioningUpdates DEVELOPMENT_TEAM=TYU4JMX349 archive
```

In Xcode Organizer, validate the archive and choose distribution to App Store Connect. Confirm the identifier, version, build number, icon, privacy manifest, and selected deployment before upload. Do not upload an app pointing to a developer's test data. Native archive success is separate from successful App Store Connect processing.

## Account steps and closed distribution

The release owner may need to sign in, satisfy two-factor authentication, accept Apple agreements, grant the required App Store Connect role, register the bundle identifier/app record, and enable distribution signing. Those account actions cannot be inferred from a successful local development install.

Provide beta description, what to test, a working feedback contact, and accurate export-compliance information. Create a private tester group and assign the processed build. Start with internal testers; external distribution may require Beta App Review. Share the invitation code/build only with that cohort. Apple documents the upload, group, review, and invitation flow in its [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview).

Do not enable a public TestFlight invitation link or public Daily competition in this milestone. Invitations/messages require the user's authorized recipients. See `PRIVACY.md` for the disclosure inventory and unresolved release-owner checks.

## Acceptance and recorded outcome

Have at least two guests play the same UTC Daily and verify identical initial boards/sequences, one best per guest, tied rankings, a lower score retaining the best, and a better score replacing it. Check provisional labels, an upload crossing midnight, 01:00 UTC expiry, final standings, and result-card labeling. Use the development deployment for synthetic boundary/abuse tests.

Record the archive path, Git commit, bundle version/build number, deployment environment, native validation result, App Store Connect processing state, review state, and whether any tester actually installed it. Report incomplete steps as pending. Backend checks currently run in hosted CI; Unity licensing, device input, Keychain, signing, and TestFlight account steps require separate verification.

A public release additionally requires verified App Attest integration, suspicious-submission review/exclusion, operational alerting, and a tested privacy/deletion process. Keep the closed-test gate until those controls are complete.

## Current delivery record — 2026-09-07 UTC

- Unity 6000.6.0f1 freshly exported version **0.1.0, build 4**, using `BuildTestFlight` and the isolated Clearjar Studio closed-test deployment (`affable-lyrebird-62`). Production standings remain separate and ranked production remains disabled.
- Xcode **Release archive succeeded** at `Builds/Archives/RingRush.xcarchive`. Code-sign verification passed for `com.osazi.roloc.unitydev`.
- Build 4 installed and launched successfully on the paired iPhone over the local network. CoreDevice confirmed the RingRush process remained running. The first launch command timed out; a subsequent launch succeeded.
- Unity: 134 ordinary EditMode tests passed (one explicit network test skipped in the unfiltered suite). PlayMode checks passed across focused runs, including a real 45-match rendered/input Daily run accepted by Convex with a transition pause/resume. Actual UI captures were inspected for menu, board, collection, pause, and results.
- Build 4 adds faint inactive pucks, inactive-puck release penalties, and a measured results stack that adapts to the safe area. The integrated change passed 73 focused Unity rule tests and six PlayMode touch/layout/capture tests, including compact and tall iPhone safe areas. Rendered captures were inspected.
- GitHub backend and Daily health workflows passed. Scheduled Convex checks are live every five minutes; GitHub cron scheduling begins when the workflow reaches the default branch.
- TestFlight distribution was attempted and stopped by Xcode with **“No Accounts with App Store Connect Access.”** No TestFlight upload or tester invitation was completed. An authorized App Store Connect account must be added in Xcode before retrying the existing archive export.
- Physical-device audio/haptic perception, accessibility preferences, extended offline play, repeated relaunches, native sharing, and sustained FPS still need a hands-on acceptance pass. Installation and Editor tests do not establish those results.

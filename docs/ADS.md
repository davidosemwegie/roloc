# Advertising setup and validation

Ring Rush uses Unity LevelPlay 9.5.0 with Unity Ads demand. The package lock pins EDM4U 1.2.185; dependency XMLs pin native LevelPlay 9.5.0, the Unity adapter bundle 5.11.0.0, iOS adapter 5.9.0.0, and Unity Ads 4.19.0. Do not accept automatic network-manager upgrades without reviewing and validating the resulting XML/pods.

## Dashboard and configuration

The Unity Ads iOS app is registered as **Ring Rush**, general audience, in organization `2476041440047`, project `19a271b8-521b-4cba-bf26-786950060f49`. Its dashboard app ID is `c6c4fa2d-98b1-4192-a75d-aa7259e33884` and Unity Ads Game ID is `800368993`.

The [Unity Ads placements](https://cloud.unity.com/organizations/2476041440047/monetization-v2/placements) are `Banner_iOS`, `Rewarded_iOS`, and `Interstitial_iOS`. These are demand-network identifiers for the LevelPlay connection; they are **not** the LevelPlay app key or ad-unit IDs consumed by `AdsConfiguration`. LevelPlay registration/linking, test-device setup, and the published policy remain required before enabling ads in a build. The store ID is not yet set because the app is not publicly released.

1. Create a LevelPlay iOS application for the bundle identifier of the intended build. Create banner, rewarded, and interstitial ad units. Use Unity Ads as the demand network and link its game/placement configuration. Do not enable SDK automatic initialization.
2. Configure the rewarded unit to award one revive. Register device identifiers as test devices in the dashboard before requesting ads. Never click live ads while validating.
3. In Unity, choose **Ring Rush → Advertising configuration**. Fill the four iOS dashboard identifiers and a published HTTPS privacy-policy URL. This creates `Assets/Resources/AdsConfiguration.asset`; the identifiers are application configuration, not admin credentials. Do not put dashboard API secrets in the app.
4. The policy must cover both Unity advertising and the existing Daily service. Missing identifiers or policy disables advertising. The Editor uses no live ads; tests inject `IAdService` or `FakeAdService` explicitly. No production build fabricates rewards.
5. Verify the Unity Ads network in LevelPlay's integration testing tools and review the generated Podfile before archiving. CocoaPods and Xcode 26+ are required by this pinned SDK. Confirm the configured privacy choices propagate to the active demand network.

## Player behavior

Successful scores 20, 50, 100, 150, then every 50 earn one revive opportunity. The bank holds three, discards overflow, and resets each game. Normal Flow chances are exhausted first. A completed rewarded ad spends one, preserves score/combo, resets the Perfect streak, restores one chance and the current timer, and resumes the same target after a three-second countdown. Normal Flow recovery limits do not reset.

If no revive or rewarded inventory is available, the loss goes to results. Declining also finalizes once. Reward and close callbacks may arrive in either order: a close without a reward returns to the offer; retry/end remains available. Duplicate and stale callbacks cannot revive a new or finalized game. Starting a retry retires the preceding unresolved ad attempt.

A completed game with an empty bank qualifies for one 25% interstitial roll before the next actual game start, including starts through the menu. There is no cooldown. Unavailable inventory/display failure skips the ad; voluntary abandonment and tutorial completion do not qualify. Probability uses randomness independent of Daily and ordinary gameplay.

Banners reserve a 50-point native-height area above the iOS safe-area bottom, with additional separation from controls. They hide for tutorials, modals, menus, results, backgrounding and full-screen advertising; the gameplay rectangle remains stable while inventory loads.

## Consent and iOS

Before first advertising initialization, the player chooses personalized or limited advertising. Personalized advertising additionally requests ATT when undetermined and active, after the consent panel dismisses. Declining either choice preserves gameplay and rewarded eligibility. Limited advertising passes GDPR consent false and CCPA sale/sharing opt-out true; it does not claim affirmative consent. Advertising settings can be revisited, and changed ATT status is reapplied on returning from iOS Settings. Changing effective consent destroys old inventory before reloading.

The native bridge and build postprocessor add ATT and its purpose description. Confirm the archive's `NSUserTrackingUsageDescription`, SKAdNetwork identifiers and every embedded SDK privacy manifest. Update App Store privacy answers against the actual configured release, using `PRIVACY.md` as the implementation inventory. The general-audience setup is not a child-directed configuration.

## Daily rollout

Newly published challenges use rules v2; existing published rows remain v1. This may delay Daily revives by eight days. Client revision 3 supports both; existing revision-2 v1 attempts remain valid. Both replays derive bank eligibility independently and share action fixtures. Revive events retain the same seed sequence and exclude ad/countdown time from the active timer. The existing closed-test gate remains: server replay validates gameplay eligibility, not independent proof of watching an ad.

## Release checks

- Before selecting an ads-enabled build in App Store Connect, change its age-rating Advertising answer to Yes and assess the actual ad content. Build 9 has no ads, so its current Advertising answer remains No until replaced. Keep public-beta Daily practice-only; enabling ranked competition requires a separate review of the Contests answer.
- Run the backend CI and Unity EditMode/PlayMode suites, including `ReviveTests`, `ReviveFixtureTests`, and `AdFlowTests`.
- Verify compact/tall iPhone board and revive layouts, banner separation, and no banners over overlays.
- On a physical test device, exercise ATT allowed/denied/restricted, consent changes, rewarded completion/cancellation, close/reward callback order, backgrounding and interstitial close/failure.
- Inspect the generated iOS project and archive for linked ad frameworks, correct native dependency versions, SKAdNetwork entries and privacy manifests.
- Until dashboard identifiers, a public policy and test-device configuration exist, mock flow tests and a successful export do not establish live ad delivery.

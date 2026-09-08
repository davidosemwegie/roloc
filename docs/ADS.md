# Advertising setup and validation

Ring Rush uses Unity LevelPlay 9.5.0 with Unity Ads demand. The package lock pins EDM4U 1.2.185; dependency XMLs pin native LevelPlay 9.5.0, the Unity adapter bundle 5.11.0.0, iOS adapter 5.9.0.0, and Unity Ads 4.19.0. Do not accept automatic network-manager upgrades without reviewing and validating the resulting XML/pods.

## Dashboard and configuration

The Unity Ads iOS app is registered as **Ring Rush**, general audience, in organization `2476041440047`, project `19a271b8-521b-4cba-bf26-786950060f49`. Its dashboard app ID is `c6c4fa2d-98b1-4192-a75d-aa7259e33884` and Unity Ads Game ID is `800368993`.

The [Unity Ads placements](https://cloud.unity.com/organizations/2476041440047/monetization-v2/placements) are `Banner_iOS`, `Rewarded_iOS`, and `Interstitial_iOS`. These are demand-network identifiers for the LevelPlay connection; they are **not** the LevelPlay app key or ad-unit IDs consumed by `AdsConfiguration`. Published regional consent messages, test-device setup and advertising policy disclosures remain required before enabling ads in a build. The store ID is not yet set because the app is not publicly released.

LevelPlay organization onboarding is complete using the owner's supplied company profile, Umbrellamode Inc, Union City, US. Do not use the Unity Ads Game ID as a substitute LevelPlay app key.

The [LevelPlay iOS app](https://platform.ironsrc.com/partners/next/adUnits/27fdef9ad?visibility=show) is registered as Ring Rush (not live yet), app key `27fdef9ad`. Its saved ad units are:

| Ad unit | Format | ID |
| --- | --- | --- |
| Gameplay Banner | Banner, 25-second refresh | `sc23sll38nkxeete` |
| Continue Streak | Rewarded, one Revive | `k62ws729b3wloc8c` |
| Between Games | Interstitial | `7ehidbf3tz1elcyf` |

These public identifiers are saved in `Assets/Resources/AdsConfiguration.asset`. The policy field remains empty so ads stay disabled until advertising disclosures are published. The existing [Ring Rush privacy policy](https://docs.google.com/document/d/1mDMFBulWr3so1DPojF6IYVHLHNd9H61ACp3FKwtMak0/view) describes the ad-free build 9 and must be updated before using it for advertising consent. Do not overwrite that policy during its separate App Store submission.

Unity Ads is connected using an owner-authorized monetization reporting key and Organization Core ID `2476041440047`. The key remains in the dashboards, never the app or repository. All three active Unity Ads bidding instances map Game ID `800368993` to the matching placement above and target All Countries. Default ironSource demand instances were deactivated; only Unity Ads demand is active. Rewarded and interstitial ad-unit capping and pacing are disabled, as verified in their saved Advanced settings. LevelPlay still reports that the ironSource Ads account is pending approval; this notice does not establish whether Unity Ads test delivery will succeed.

Test-device registration requires the physical iPhone's advertising ID, not its Xcode device identifier. The connected-device inventory currently reports the owner's iPhone as unavailable, so registration and real callback verification remain pending. No placeholder device ID has been registered.

1. Verify the saved LevelPlay app and ad-unit identifiers above against the intended iOS build. Unity Ads is the configured demand network. Do not enable SDK automatic initialization.
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

Apple ATT is the tracking prompt. The app has no custom personalization dialog or general-purpose ad-disable option. Denied or restricted ATT keeps contextual ads and rewarded eligibility available when the consent provider permits ad requests.

Regional requirements use Google's standalone User Messaging Platform 3.1.0, without adding the Google Mobile Ads SDK or Google ad demand. Ring Rush's consent-only AdMob app ID is `ca-app-pub-6400654457067913~9383583266`. The provider updates its regional decision per launch and presents a form only when required. Settings exposes **Privacy choices** only when the provider requires that entry point. Do not publish Google's IDFA explainer; Ring Rush requests native ATT directly after any required regional form closes.

Publish an EEA/UK/Switzerland European-regulations message and the applicable US privacy-options message, including Unity Ads and ironSource in Additional Consent. Only mark `ConsentMessagesPublished` true after verifying the published dashboard configuration; UMP's `canRequestAds` can otherwise return true even when no messages exist. The flag currently remains false. No language/timezone-based region guessing or production debug geography is used.

The consent-only AdMob app is created. On September 7, 2026, the European-regulations message builder returned **Can't load page**, including after reloading and retrying. No regional messages were published. Complete message setup when the dashboard is available and the future advertising policy is ready.

UMP completion is not blanket vendor or tracking consent. Where GDPR applies, the native bridge additionally requires purpose-1 storage consent before initializing LevelPlay, following [Unity's initialization guidance](https://docs.unity.com/en-us/grow/levelplay/platform/legal-resources/ironsource-gdpr-compliance). Personalization refusal can still allow contextual ads; complete refusal of required storage consent can prevent this SDK from serving ads. This is a regulatory gate, not a free remove-ads product feature. [UMP setup](https://developers.google.com/admob/ios/privacy).

LevelPlay reads actual UMP/Additional Consent decisions. ATT only adds a Unity Ads non-behavioral restriction; it never grants GDPR consent or fabricates a US sale/sharing choice. Under GDPR, tracking eligibility also requires personalization purposes 3/4 and consent for Unity Ads (provider 3234) and ironSource (2878). US regions requiring privacy options currently stay contextual because GPP opt-out decoding is not implemented. Inventory and late reward attempts are invalidated after provider privacy changes. Verify changes propagate to already initialized adapters on device: the SDK documentation does not specify their timing. LevelPlay has no shutdown API, and prior collection is not deleted by changing privacy options.

The native bridge and build postprocessor add ATT and its purpose description. Confirm the archive's `NSUserTrackingUsageDescription`, SKAdNetwork identifiers and every embedded SDK privacy manifest. Update App Store privacy answers against the actual configured release, using `PRIVACY.md` as the implementation inventory. The general-audience setup is not a child-directed configuration.

## Daily rollout

Newly published challenges use rules v2; existing published rows remain v1. This may delay Daily revives by eight days. Client revision 3 supports both; existing revision-2 v1 attempts remain valid. Both replays derive bank eligibility independently and share action fixtures. Revive events retain the same seed sequence and exclude ad/countdown time from the active timer. The existing closed-test gate remains: server replay validates gameplay eligibility, not independent proof of watching an ad.

## Release checks

- Before selecting an ads-enabled build in App Store Connect, change its age-rating Advertising answer to Yes and assess the actual ad content. Build 9 has no ads, so its current Advertising answer remains No until replaced. Keep public-beta Daily practice-only; enabling ranked competition requires a separate review of the Contests answer.
- Run the backend CI and Unity EditMode/PlayMode suites, including `ReviveTests`, `ReviveFixtureTests`, and `AdFlowTests`.
- Verify compact/tall iPhone board and revive layouts, banner separation, and no banners over overlays.
- On a physical test device, exercise ATT allowed/denied/restricted, consent changes, rewarded completion/cancellation, close/reward callback order, backgrounding and interstitial close/failure.
- Inspect the generated iOS project and archive for linked ad frameworks, correct native dependency versions, SKAdNetwork entries and privacy manifests.
- Until the policy covers advertising, regional consent messages are published and test-device configuration exists, mock flow tests and a successful export do not establish live ad delivery.

# Ring Rush privacy disclosure draft

This is an implementation inventory for the release owner. It is not a submitted App Store disclosure or a published privacy policy. Review the final binary, enabled deployment, third-party infrastructure settings, and [Apple's privacy definitions](https://developer.apple.com/app-store/app-privacy-details/) before completing App Store Connect.

## Data used by this version

| Data | Location and purpose | Retention |
| --- | --- | --- |
| Audio/haptic/accessibility preferences, mode records, tutorial hints, cosmetic equipment, progress, goals | Local app save; ordinary gameplay and personalization | Until local data is removed; normal device backup behavior must be verified |
| Anonymous guest ID and authentication/session records | Convex Auth; identify the guest allowed to submit a Daily best | Authentication lifecycle; not automatically deleted by ranking retention |
| Guest access/refresh credentials | iOS Keychain; renew authenticated requests | Until revoked or removed; Keychain may survive an app reinstall |
| Daily attempt ID, challenge, timestamps, outcome, rejection reason | Convex; ticket ownership, validation, upload retries | Seven days after finalized validation; open attempts expire separately |
| Drop coordinates, active round elapsed milliseconds, pauses/resumes, timeouts, revive decisions, and abandon events | Local upload queue and Convex trace chunks; derive a legal Daily score | Queued locally until handled/expired; server chunks follow the attempt purge |
| Best Daily score and guest association | Convex; one contribution per guest, percentile calculation | One year after the challenge upload deadline |
| Shared result image/text | Generated locally and handed to the destination the player selects in the iOS share sheet | Destination controls its own copy; Ring Rush does not upload the shared card to Convex |
| Function failures and infrastructure request information | Convex/GitHub operational systems; reliability and abuse response | Check actual provider logs, backups, access, and retention before release |

Daily request data is associated with an anonymous account identifier even though the game does not request a name or email. “Anonymous guest” must not be described as “no data collected” or assumed to mean Apple's “not linked” category.

The application integrates Unity LevelPlay and Unity Ads for gameplay banners, rewarded revives, and between-game interstitials. The SDK initializes only after separate affirmative advertising device-data consent and valid placement/policy configuration. The player can decline and continue ordinary gameplay. Personalized advertising additionally requires a personalization choice and iOS tracking authorization. Limited ads use conservative personalization consent and sale/sharing opt-out flags. Cosmetic progress, ordinary Flow/Rush traces, and sound settings are not sent to a separate analytics service, but the advertising SDKs do process advertising analytics and diagnostics. Convex hosts the Daily game service. Apple TestFlight can collect its own beta feedback and diagnostics under Apple's terms.

Advertising privacy preferences are saved locally. ATT remains an operating-system permission: declining it does not remove gameplay or rewarded-ad eligibility when advertising device-data use is allowed. Device-data consent withdrawal stops app-controlled inventory and retries; it does not erase previously collected data or unload the native SDK. SDK inventory is replaced when effective personalization consent changes, including tracking changes made in iOS Settings.

The reviewed iOS export includes IronSourceAdQualitySDK 9.9.0 through LevelPlay 9.5.0. Its embedded privacy manifest declares device identifiers, performance data and other diagnostics for analytics and app functionality, marked not linked and not tracking. This is distinct from Unity Ads demand; disabling ironSource demand does not remove LevelPlay's diagnostic components.

Unity's [Unity Ads disclosure table](https://docs.unity.com/en-us/grow/ads/privacy/apple-privacy-survey) includes user/device identifiers, approximate location, purchase history, ad activity, other usage, performance and other device information. The [LevelPlay disclosure](https://docs.unity.com/en-us/grow/levelplay/platform/legal-resources/apple-privacy-questionnaire) is a separate inventory. Review both against the final binary and configuration; an empty collected-data list in a bundled manifest is not evidence that an SDK collects nothing. Do not infer that purchase-related SDK data is absent solely because Ring Rush has no purchases.

## Future advertising policy addition

This is proposed wording for the future ads release, not a change to the public Google Doc. Assign its actual build/version and effective date when releasing it, retain the ad-free build 9 section, and reconcile the final SDK data categories and support/deletion process before publishing.

**Advertising-enabled versions:** These versions use Unity LevelPlay to manage advertisements supplied by Unity Ads. Ads include gameplay banners, occasional full-screen ads between games, and optional rewarded ads that let eligible players continue a run. Build 9 remains ad-free and does not include these advertising SDKs.

Unity's advertising services process app and device identifiers, device and network information, approximate location, ad activity and performance information for ad delivery, measurement, fraud prevention and service improvement. With permission, advertising identifiers and activity may also be used or shared for personalized ads. Unity explains its processing, partners and privacy rights in its [Game Player and App User Privacy Policy](https://unity.com/legal/game-player-and-app-user-privacy-policy).

Before advertising starts, Ring Rush asks whether to allow advertising device-data use. You can decline and continue playing without ads, or allow advertising and choose personalized or limited ads. Limited ads still involve data processing. Personalized ads additionally require Apple's tracking permission on iOS. Declining tracking does not prevent ordinary gameplay or available rewarded ads when advertising data use is allowed.

You can revisit these choices under Ad privacy in Settings, including turning ads off, and change tracking permission in iOS Settings. Ring Rush saves these preferences on your device. Rewarded-ad completion is used to grant an eligible revive. LevelPlay includes ad-quality diagnostics. Turning ads off stops Ring Rush's ad requests but does not delete information already collected; Unity's policy explains retention and access/deletion options.

## App Store Connect review items

- Assess **Identifiers / User ID** and **Usage Data / Gameplay Content** for Daily guest records and run traces, used for app functionality. Determine whether other usage or diagnostics categories apply to actual operational collection.
- Treat data associated with a persistent guest identifier conservatively as linked until the release owner confirms Apple's definitions against the implemented lifecycle.
- Disclose enabled third-party advertising and any permitted tracking. Verify ATT purpose text, allowed/denied/restricted states, consent withdrawal, SKAdNetwork identifiers, and every embedded ad SDK privacy manifest. Use test devices to avoid live ad traffic during validation.
- Inspect the Unity-generated privacy manifest and every embedded framework. Required-reason API declarations must match the actual binary, including preferences, timestamps, storage, and native code usage where applicable.
- Publish a privacy policy describing Unity advertising and consent choices, Convex processing, retention, support contact, and how guests request deletion. There is no implemented in-app account-deletion flow in this milestone; define and test the operator process before public release.
- Verify deletion covers authentication tables/sessions, attempts, chunks, daily bests, aggregate entries, and applicable provider records. Respect the published final-ranking policy when designing that process.
- Verify device backup behavior, cached-queue expiry, auth revocation, and provider log/backup retention rather than promising immediate removal everywhere.

App Attest is a later public-competition gate. Update this inventory when attestation information is collected. Revisit the disclosure before changing ad networks or adding analytics, purchases, named accounts, or cloud progression.

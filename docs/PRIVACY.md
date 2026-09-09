# Ring Rush privacy disclosure inventory

This implementation inventory supports the release owner. The public policy and submitted disclosure are recorded below. Reassess the final binary, enabled deployment, third-party infrastructure settings, and [Apple's privacy definitions](https://developer.apple.com/app-store/app-privacy-details/) whenever collection changes.

## Published policy and build 9 disclosure — September 7, 2026

- [Public privacy policy](https://docs.google.com/document/d/1mDMFBulWr3so1DPojF6IYVHLHNd9H61ACp3FKwtMak0/view): Google Doc in the owner's ChatGPT folder. Drive metadata confirms `anyone / reader`, with search discovery disabled. Visitors can view, not edit.
- [Support form](https://docs.google.com/forms/d/e/1FAIpQLSdZvo9IH7wCY63cpYVSbsQ_I2aA6aBaN9RZIhyPInVIswdT4g/viewform): one required message, optional reply email and device/app version. Anyone with the link can respond without signing in; automatic email collection, one-response restriction, and public response summaries are off. Contact: `osahon@clearjar.co`.
- App Privacy responses published in App Store Connect: **Email Address**, **Customer Support**, **Gameplay Content**, **User ID**, and **Product Interaction**. All are used for **App Functionality**, linked to the user, and not used for tracking. Product Interaction covers the ranked drop/timing/pause trace, not an analytics SDK.
- Build 9 contains no advertising or analytics SDK. Rankings remain paused on the isolated beta deployment. Ordinary modes and Daily practice remain local; existing ranked guests may still authenticate for saved standings or queued uploads. Do not label the app “Data Not Collected.”
- Source review covered `DailyClient`, the Daily presentation flow, native Keychain/sharing code, validation retention, and scheduled purge. The coordinator checked the material paths and verified the published form and Drive permissions. Provider logs/backups and a complete operator deletion process remain operational follow-up; the policy does not promise immediate or automated deletion.
- Update this policy, the App Privacy responses, and age-rating Advertising answer before shipping the separate advertising integration. The build-9 policy must not be reused unchanged for an ads-enabled binary.

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

The application integrates Unity LevelPlay and Unity Ads for gameplay banners, rewarded revives, and between-game interstitials. Apple ATT controls tracking. Declining ATT does not disable ads or rewarded eligibility; Unity Ads is restricted to contextual delivery. There is no general ad-disable setting or custom personalization popup.

Google User Messaging Platform 3.1.0 supplies regional consent forms and stores provider-managed choices. It presents additional UI only when required and exposes regional privacy options in Settings when necessary. The SDK is consent-only; Google Mobile Ads and Google ad demand are not enabled. Before LevelPlay initialization, the provider must permit ad requests and, where GDPR applies, required device-storage consent must be present. A complete refusal can prevent SDK initialization; an allowed contextual-ad decision does not require tracking permission. The app never treats ATT authorization as GDPR or US state consent.

The compiled UMP 3.1.0 resource-bundle privacy manifest declares coarse location, performance data and product interaction for app functionality, marked not linked and not tracking. It declares UserDefaults access with reason CA92.1. Include this component when reviewing the future release's App Store privacy answers.

Cosmetic progress, ordinary Flow/Rush traces and sound settings are not sent to a separate analytics service, but advertising SDKs process advertising analytics and diagnostics. Convex hosts the Daily service. TestFlight can collect beta feedback and diagnostics under Apple's terms. Changing provider privacy options replaces app-controlled inventory and invalidates late rewards; it does not unload the native SDK or erase previously collected information. Verify actual updated consent propagation in the final device build.

The reviewed iOS export includes IronSourceAdQualitySDK 9.9.0 through LevelPlay 9.5.0. Its embedded privacy manifest declares device identifiers, performance data and other diagnostics for analytics and app functionality, marked not linked and not tracking. This is distinct from Unity Ads demand; disabling ironSource demand does not remove LevelPlay's diagnostic components.

Unity's [Unity Ads disclosure table](https://docs.unity.com/en-us/grow/ads/privacy/apple-privacy-survey) includes user/device identifiers, approximate location, purchase history, ad activity, other usage, performance and other device information. The [LevelPlay disclosure](https://docs.unity.com/en-us/grow/levelplay/platform/legal-resources/apple-privacy-questionnaire) is a separate inventory. Review both against the final binary and configuration; an empty collected-data list in a bundled manifest is not evidence that an SDK collects nothing. Do not infer that purchase-related SDK data is absent solely because Ring Rush has no purchases.

## Published advertising policy addition — September 8, 2026

The public Google Doc now includes a version-specific section for advertising-enabled build 10 and later, with an updated September 8, 2026 date. Connector readback and an unauthenticated export verified the addition, vendor links, and preserved build-9 disclosure. The following summarizes the published addition; App Store privacy and age-rating answers still need review before submitting the new binary.

**Advertising-enabled versions:** These versions use Unity LevelPlay to manage advertisements supplied by Unity Ads. Ads include gameplay banners, occasional full-screen ads between games, and optional rewarded ads that let eligible players continue a run. Build 9 remains ad-free and does not include these advertising SDKs.

Unity's advertising services process app and device identifiers, device and network information, approximate location, ad activity and performance information for ad delivery, measurement, fraud prevention and service improvement. With permission, advertising identifiers and activity may also be used or shared for personalized ads. Unity explains its processing, partners and privacy rights in its [Game Player and App User Privacy Policy](https://unity.com/legal/game-player-and-app-user-privacy-policy).

Apple's tracking prompt controls whether this version can track activity across other companies' apps and websites. Declining tracking still allows contextual advertisements and available rewarded revives. Ring Rush does not provide a general setting to remove ads.

Where regional rules require it, Google's User Messaging Platform presents privacy choices before advertising starts. Available ad delivery depends on those choices and inventory. You can revisit regional choices through Privacy choices in Settings when that option is required, and change Apple tracking permission in iOS Settings. The provider stores its consent choices on your device. Google's [privacy policy](https://policies.google.com/privacy) describes its handling of information.

Rewarded-ad completion grants an eligible revive. LevelPlay includes ad-quality diagnostics. Changing privacy preferences does not delete information already collected; Unity's policy explains its retention and access/deletion options.

## App Store Connect review items

- Assess **Identifiers / User ID**, **User Content / Gameplay Content**, and **Usage Data / Product Interaction** for Daily guest records and run traces, used for app functionality. Determine whether other usage or diagnostics categories apply to actual operational collection.
- Treat data associated with a persistent guest identifier conservatively as linked until the release owner confirms Apple's definitions against the implemented lifecycle.
- Disclose enabled third-party advertising and any permitted tracking. Verify ATT purpose text, allowed/denied/restricted states, consent withdrawal, SKAdNetwork identifiers, and every embedded ad SDK privacy manifest. Use test devices to avoid live ad traffic during validation.
- Inspect the Unity-generated privacy manifest and every embedded framework. Required-reason API declarations must match the actual binary, including preferences, timestamps, storage, and native code usage where applicable.
- Publish a privacy policy describing Unity advertising and consent choices, Convex processing, retention, support contact, and how guests request deletion. There is no implemented in-app account-deletion flow in this milestone; define and test the operator process before public release.
- Verify deletion covers authentication tables/sessions, attempts, chunks, daily bests, aggregate entries, and applicable provider records. Respect the published final-ranking policy when designing that process.
- Verify device backup behavior, cached-queue expiry, auth revocation, and provider log/backup retention rather than promising immediate removal everywhere.

App Attest is a later public-competition gate. Update this inventory when attestation information is collected. Revisit the disclosure before changing ad networks or adding analytics, purchases, named accounts, or cloud progression.

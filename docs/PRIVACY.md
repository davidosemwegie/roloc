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
| Drop coordinates, active round elapsed milliseconds, pauses/resumes, timeouts, and abandon events | Local upload queue and Convex trace chunks; derive a legal Daily score | Queued locally until handled/expired; server chunks follow the attempt purge |
| Best Daily score and guest association | Convex; one contribution per guest, percentile calculation | One year after the challenge upload deadline |
| Shared result image/text | Generated locally and handed to the destination the player selects in the iOS share sheet | Destination controls its own copy; Ring Rush does not upload the shared card to Convex |
| Function failures and infrastructure request information | Convex/GitHub operational systems; reliability and abuse response | Check actual provider logs, backups, access, and retention before release |

Daily request data is associated with an anonymous account identifier even though the game does not request a name or email. “Anonymous guest” must not be described as “no data collected” or assumed to mean Apple's “not linked” category.

The application has no advertising or analytics SDK and does not send cosmetic progress, ordinary Flow/Rush traces, or sound settings to an analytics service. Convex hosts the Daily game service. Apple TestFlight can collect its own beta feedback and diagnostics under Apple's terms; those capabilities are separate from an in-game analytics integration.

## App Store Connect review items

- Assess **Identifiers / User ID**, **User Content / Gameplay Content**, and **Usage Data / Product Interaction** for Daily guest records and run traces, used for app functionality. Determine whether other usage or diagnostics categories apply to actual operational collection.
- Treat data associated with a persistent guest identifier conservatively as linked until the release owner confirms Apple's definitions against the implemented lifecycle.
- Confirm there is no cross-company tracking or advertising use in the submitted build or provider configuration; do not request App Tracking Transparency for a use the app does not perform.
- Inspect the Unity-generated privacy manifest and every embedded framework. Required-reason API declarations must match the actual binary, including preferences, timestamps, storage, and native code usage where applicable.
- Publish a privacy policy describing Convex processing, retention, support contact, and how guests request deletion. There is no implemented in-app account-deletion flow in this milestone; define and test the operator process before public release.
- Verify deletion covers authentication tables/sessions, attempts, chunks, daily bests, aggregate entries, and applicable provider records. Respect the published final-ranking policy when designing that process.
- Verify device backup behavior, cached-queue expiry, auth revocation, and provider log/backup retention rather than promising immediate removal everywhere.

App Attest is a later public-competition gate. Update this inventory when attestation information is collected. Revisit the disclosure before adding analytics, ads, purchases, named accounts, or cloud progression.

# Ring Rush — App Store launch assets

English (US), refreshed September 7, 2026 for **ClearJar Financial Inc.**

**Status: App Store Connect draft saved.** [Ring Rush - Match the colors](https://appstoreconnect.apple.com/apps/6809459301/distribution) is registered under ClearJar Financial Inc. (`BK7TPQ53FF`) with store bundle ID `com.clearjar.ringrush`, English (US), and SKU `ring-rush-ios`. Development uses the separately registered `com.clearjar.ringrush.dev`. Six screenshots are uploaded in 01–06 order and verified after reload; name, subtitle, promotional text, description, keywords, Casual/Action game categories and review contact are saved. Manual release is selected. Nothing has been submitted for App Store review or published. TestFlight setup and a private invitation are authorized separately and in progress.

The listing copy has been updated for build 8, including movement combinations, color shifts and badges, and no longer offers Still boards. The confirmed support email is `osahon@clearjar.co`; the review contact, including the supplied phone, is saved privately in Apple. Public support/privacy URLs and copyright ownership remain unconfirmed.

## Review and deliverables

- `contact-sheet.png`: six screenshots in upload order, left to right, top to bottom.
- `screenshots/01-match.png` through `06-your-pace.png`: opaque 1320 × 2868 PNG masters.
- `video/ring-rush-preview.mp4`: final 15-second, 886 × 1920 portrait preview.
- `video/poster-frame.png`: recommended poster at 1.0 seconds; select this timestamp in App Store Connect.
- `app-icon-1024.png`: opaque export of the existing icon, not a new design. The actual store icon is supplied by the uploaded app build.
- `listing.md` / `listing.json`: copy for manual entry or later automation.
- `submission-checklist.md`: fields and account actions that remain pending.
- `validation.json`: machine-checked dimensions, encoding, lengths and media properties.

## Editable sources

`source/compose.cjs` contains screenshot text, positioning and colors. `source/compositions/*.svg` are self-contained compositions with embedded real gameplay images and outlined text. Adjust text in the JavaScript to keep it easily editable. Run from the repository root:

```sh
npm ci --prefix marketing/app-store/source
node marketing/app-store/source/compose.cjs
```

The editable preview is available at [HyperFrames Studio](http://localhost:3047/#project/preview) while the local preview server is running.

`video/preview/index.html` is the editable HyperFrames composition. It includes a full-screen real gameplay video, thirteen original match-effect clips at recorded event times, the original music bed and a closing caption. Dependencies and CLI versions are pinned. Rendering instructions are in `source/render-preview.sh`.

`captures/*.png` were refreshed from build 8 at 1320 × 2868 on September 7. `captures/cosmetics.png` replaces the retired Still-board inset. `captures/frames/` contains the real 886 × 1920 video frames; `timeline.csv` preserves their original unscaled timestamps. `source/prepare-preview.py` conforms those timestamps to 30 FPS by selecting the nearest real frame, without speeding up gameplay or synthesizing intermediate frames. The compact conformed source video is also retained in `video/preview/assets/gameplay.mp4`. The PNG frame sequence is preserved locally but excluded from Git by this folder's ignore file.

## Capture integrity

The screenshot-only capture harness (`source/StoreCaptureTests.cs`) passed in an isolated copy of source `ea9f01e718e5b31cc9b46f95f2323512f0983737`, build 8, with Unity 6000.6.0f1. All six upload screenshots were recomposed from those current renders. The motion image shows a blue palette with puck orbit and ring drift at score 90. The original video and its timeline/audio provenance remain from September 6; they were not regenerated or verified as an exact build-8 preview. It invokes the real touch/drag handlers and rules, and earns the displayed scores and cosmetics through matches. It uses a fresh temporary save, not the player's save. Tutorial hints are marked seen for marketing capture. Off-camera screenshot preparation skips board-transition waits. The retained September 6 video uses normal game timing and transitions. The camera and safe area are configured for the requested portrait capture dimensions. These are Unity-rendered gameplay captures, not recordings from a physical iPhone.

The preview starts from an earned score of 29. A seeded normal run changes from Steady to Drifting at 5.036 seconds, then takes a Breather at 8.619 and returns to Steady at 10.920. There are successful matches throughout. The last two seconds add “Find your flow.” in the lower margin. This is a loop-like preview with a closing beat, not a claim of pixel-perfect seamless looping. Original audio files are copied unchanged; volume fades make the music boundary clean. The final mix measures −22.1 dB mean and −1.0 dB peak, with the last 20 ms below −37 dB peak.

The sculpted backdrop is generated artwork used only outside the game screens. No gameplay elements, rewards, scores or UI were generated by the image model. See `source/provenance.json` for source hashes and the backdrop prompt.

## Apple references

Specifications checked September 6, 2026:

- [Screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/): the selected portrait screenshot dimensions.
- [App preview specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/app-preview-specifications/): accepted portrait resolution, duration, H.264 and AAC delivery settings.
- [App Store search](https://developer.apple.com/app-store/search/): accurate naming, subtitle, keyword and visual positioning guidance.

App Store Connect accepted all six screenshots and their order was verified after reload. The retained September 6 preview video has not been uploaded; its processing remains unverified. The marketing captures reflect the project snapshot used for this session; check them against the final release build if gameplay or UI changes before launch.

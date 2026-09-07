# Ring Rush

The Unity 6.6 game lives at the repository root. Open this folder with **Unity 6000.6.0f1**, load `Assets/Scenes/Roloc.unity`, and press Play. Unity UI and the Input System support touch and Editor mouse input.

Flow gives three chances, a gentler opening, and limited recovery. Rush keeps the strict one-chance rules. Regular runs use the Lively board; the board selector has been removed. Historical Still records remain saved. Every successful match earns local progress; centered Perfects earn an extra point. The collection, daily goals, audio choices, haptics, matching symbols, and reduced effects are saved locally. The original save filename and bundle ID `com.osazi.roloc.unitydev` are unchanged.

The bright puck is the correct choice. All pucks can be dragged; releasing a dim puck costs a chance in Flow and ends Rush or Daily, even inside its own ring. Canceled touches do not count as mistakes. Results adapt to the safe area with separate rows for the score, records, progress, and replay actions.

Run-score and lifetime milestones unlock 21 collectible badges. Open Badges from the menu to browse both tracks; newly earned badges appear on results. Existing records backfill earned badges once. Perfect matches add a brief double ripple and distinct iPhone haptic, respecting reduced effects and haptic preferences.

Regular challenge episodes can change colors from 20 matches, orbit either pucks or rings from 40, and orbit both groups from 60. Movement episodes last 3–5 matches, followed by recovery and calm. Changed colors stay for the rest of the run until another color shift. Floating pucks can combine with drifting rings from 20 matches; puck orbits with ring drift and ring orbits with floating pucks join the rotation from 60. Each group has at most one movement effect, and all combinations can use the current palette. Color shifts use six curated palettes with distinct shades, including four pinks or blues. Orbit directions are independently clockwise or counterclockwise; grabbing a puck keeps it under the finger. Timing and input pause during board transitions. Thresholds and speeds are editable in `Assets/Settings/Difficulty.asset`; palettes are in `Assets/Resources/VariationPalettes.asset`. Every board still has four pucks and four matching rings.

Daily uses the isolated [Convex backend](backend/README.md). Invited ranked attempts require an online start; gameplay then runs locally and finished traces queue for upload. Cached challenges are available as practice. Development and production must use separate deployments and invitation codes. Public competition stays disabled until App Attest and suspicious-submission review are ready.

## Develop

```sh
npm --prefix backend ci
npm --prefix backend run check
# Connect/configure only the development backend:
cd backend
npx convex dev
```

Unity package versions are pinned in `Packages/manifest.json` and `Packages/packages-lock.json`. The experimental Pipeline package supports Editor interaction; the installed Unity CLI also runs batch commands:

```sh
unity test . --mode EditMode --output TestResults/editmode.xml --timeout 300
unity test . --mode PlayMode --output TestResults/playmode.xml --timeout 300
unity build . --target iOS --execute-method Roloc.Editor.ProjectBuilder.BuildIOS --output-path Builds/iOS --allow-dirty-build --timeout 600
```

Always export freshly from Unity before an Xcode build. `ProjectBuilder.Configure` uses the existing Apple team `TYU4JMX349`. `BuildTestFlight` exports a release build and requires a unique `RING_RUSH_BUILD_NUMBER`. See [closed-test delivery](docs/TESTFLIGHT.md).

To configure Daily in a local build, set `RING_RUSH_CONVEX_URL` and `RING_RUSH_CLOSED_TEST_CODE` in the build process environment. The builder creates the ignored `Assets/Resources/DailyConnection.asset`. It contains a tester invitation code, never a Convex administrative key. Guest tokens use iOS Keychain; Editor tokens remain in memory. Offline Flow and Rush work without this asset.

## Reference and operations

- [Original recordings](src/assets) remain unchanged at their original paths and are copied unchanged into `Assets/Audio/`.
- [Self-contained legacy rule reference](Reference/legacy-geometry.ts) preserves geometry and strict timing without the retired app dependencies. Earlier runtime code remains in Git history.
- [Daily operations](docs/DAILY_OPERATIONS.md): publication, health checks, kill switch, retention, and release gates.
- [Privacy disclosure draft](docs/PRIVACY.md): data used for anonymous rankings.

The React Native runtime, Firebase, and Mixpanel integrations have been removed. No historical users or backend data are imported. Unity Ads monetization uses LevelPlay for banners, milestone-based rewarded revives, and optional interstitials. See `docs/ADS.md` for configuration and device validation. Separate analytics, purchases, named accounts, and cross-device progression remain deferred.

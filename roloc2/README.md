# ROLOC 2

An offline, portrait color-matching game for iPhone, rebuilt in Unity. Drag the highlighted puck into its matching ring before the timer expires. Matches increase the score and pace; later scores shuffle the rings.

After 40 points, the middle pucks also shuffle every five matches (45, 50, 55, and onward). Each shuffle changes their arrangement. Dragging and the countdown pause during the movement; pausing the game freezes it until you resume. Adjust the threshold and interval in `Assets/Settings/Difficulty.asset`.

## Random flow

After the opening matches, successful drops can introduce one randomly selected variation: floating inactive pucks, slow drifting rings, extra-time breather stretches, or a coordinated clockwise/counterclockwise puck rotation. Floating and breathers become eligible from score 5, drift from 20, and rotation from 40. These are eligibility thresholds, not fixed event schedules.

Floating, drift, and breathers last 3–5 matches; rotations happen once between rounds. Random calm gaps separate variations, the same variation cannot repeat immediately, and breathers have a cooldown. Rotation replaces any simultaneous ring/puck scramble. When rings and middle pucks both shuffle, the rings move first and the pucks follow; play resumes after both settle. Motion is bounded, the active puck stays steady until grabbed, pauses freeze all movement, and the timer waits for each transition. Breathers add 0.65 seconds to the normal timer without reducing time elsewhere.

The difficulty asset exposes **Random Flow**, eligibility thresholds, episode lengths, cooldown, movement amplitudes, and transition durations. Disable **Random Flow Enabled** to retain the standard timed/shuffling rules.

## Requirements

- Unity **6000.6.0f1**, Apple Silicon Editor, with iOS Build Support.
- Unity CLI (tested with `1.0.0-beta.6`) and an activated Unity license.
- Xcode for iPhone/simulator builds; signing and a trusted device for physical installation.

## Open and play

From this directory, run:

```sh
~/.unity/bin/unity open .
```

Open `Assets/Scenes/Roloc.unity` and press Play. If the scene has not been generated yet, choose **ROLOC → Set up game**, or run:

```sh
~/.unity/bin/unity run . -- -executeMethod Roloc.Editor.ProjectBuilder.Setup
```

The setup command generates the scene and reusable puck/ring prefabs, imports the original sound recordings, and configures portrait iPhone builds. Existing prefabs and difficulty settings are preserved when setup is rerun. The generated main scene is rebuilt.

Touch and mouse are supported. The first play opens an untimed tutorial. Sound settings independently control music, match effects, and game-over effects. Scores, aggregate statistics and preferences are stored in `roloc2-progress.json` in Unity's application persistent-data directory; the old game is not accessed.

## Development

- `Assets/Scripts/Core`: timed game state and editable `DifficultySettings`.
- `Assets/Scripts/Presentation`: generated Unity UI, vector shapes, dragging, animation and lifecycle.
- `Assets/Scripts/Services`: local saves and three reusable audio sources.
- `Assets/Editor/ProjectBuilder.cs`: repeatable setup and build entry points.
- `Assets/Tests`: isolated rule, persistence and integrated PlayMode tests.

The Pipeline package is pinned in `Packages/manifest.json`. When the Editor is open, use `unity command --project-path .` to discover live commands. Pipeline is a development tool; no runtime automation component is added to the game.

## Tests

Close the Editor before launching batch-mode tests against this same project.

```sh
~/.unity/bin/unity test . --mode EditMode --output TestResults/editmode.xml --timeout 300
~/.unity/bin/unity test . --mode PlayMode --output TestResults/playmode.xml --timeout 300
```

For physical validation: finish the tutorial, match several pucks, miss a ring, restart, pause during a drag, background and resume, toggle each sound, and relaunch to confirm the save. Verify repeated identical colors reset the visible countdown. Test in airplane mode. Inspect high-score ring shuffling through the rule tests and sustained play.

## iPhone build

```sh
~/.unity/bin/unity build . --target iOS --execute-method Roloc.Editor.ProjectBuilder.BuildIOS --output-path Builds/iOS --allow-dirty-build
open Builds/iOS/Unity-iPhone.xcodeproj
```

In Xcode, open **Signing & Capabilities**, enable **Automatically manage signing**, and select your development team. Choose your connected, unlocked iPhone as the run destination, then Run. Enable **Settings → Privacy & Security → Developer Mode** on the phone and complete its restart/confirmation prompts before installation.

If Xcode reports “Operation not permitted” while copying headers, check its access to the project folder under macOS **System Settings → Privacy & Security → Files & Folders**. After granting access, quit and reopen Xcode before retrying.

The development bundle ID is `com.osazi.roloc.unitydev`. Generated builds, test results and Unity caches are ignored by Git.

For the Apple Silicon iPhone simulator:

```sh
~/.unity/bin/unity build . --target iOS --execute-method Roloc.Editor.ProjectBuilder.BuildSimulator --output-path Builds/iOSSimulator --allow-dirty-build
```

## Audio provenance

`Assets/Audio/playing.wav`, `match.wav`, and `game-over.wav` are byte-for-byte copies of the original game's `src/assets` recordings. The music loops during play; pause preserves its playback position. Successful matches and game over each trigger their original effect once.

## Next milestone

Unity Ads through LevelPlay is deferred until the playable iPhone milestone is validated. It will start with test ads and one optional rewarded continue per run. This build contains no advertising, analytics, accounts, Firebase, or network requirement.

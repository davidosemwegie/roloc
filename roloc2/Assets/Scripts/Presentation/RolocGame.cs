using System;
using Roloc.Core;
using Roloc.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    /// <summary>Presentation and application lifecycle; all timed rules live in GameSession.</summary>
    public sealed class RolocGame : MonoBehaviour
    {
        public DifficultySettings difficulty;
        public AudioClip backgroundMusic, matchSound, gameOverSound;
        public Font typeface;
        public GameObject puckPrefab, ringPrefab;
        [NonSerialized] public string SaveDirectoryOverride;
        [NonSerialized] public int? RandomSeedOverride;
        public GameSession Session { get; private set; }
        public SaveService Saves { get; private set; }

        public static readonly Color[] Palette = {
            new Color32(245, 112, 99, 255), new Color32(80, 157, 235, 255),
            new Color32(76, 190, 150, 255), new Color32(166, 127, 226, 255)
        };
        static readonly Color Ink = new Color32(42, 54, 69, 255);
        static readonly Color Muted = new Color32(130, 138, 147, 255);
        static readonly Color Paper = new Color32(246, 245, 241, 255);
        static readonly Vector2[] RingSlots = { new Vector2(-103, 152), new Vector2(103, 152),
            new Vector2(-103, -152), new Vector2(103, -152) };
        static readonly Vector2[] PuckSlots = { new Vector2(-47, 53), new Vector2(47, 53),
            new Vector2(-47, -53), new Vector2(47, -53) };

        RectTransform safe, menu, game, board, results, overlay;
        readonly RectTransform[] rings = new RectTransform[4];
        readonly PuckView[] pucks = new PuckView[4];
        readonly SoftShape[] ringArt = new SoftShape[4];
        readonly SoftShape[] guide = new SoftShape[9];
        readonly Vector2[] ringFrom = new Vector2[4], ringTo = new Vector2[4];
        readonly Vector2[] puckFrom = new Vector2[4], puckBeforeHomes = new Vector2[4];
        Text scoreText, bestText, instruction, tempoText, resultScore, resultTitle, resultBest, menuBest, menuGames, menuAverage;
        SoftShape timerFill, ripple;
        RectTransform timerRect;
        GameAudio audioPlayer;
        float transitionLeft, rippleTime;
        float transitionDuration, motionTime, motionBlend;
        int transitionRotation;
        bool transitionBothBoards;
        int rippleColor;
        Vector2 ripplePosition;
        bool gameRecorded;
        bool settingsFromPause;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            if (!typeface) typeface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!difficulty) difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            Saves = new SaveService(SaveDirectoryOverride);
            var random = RandomSeedOverride.HasValue ? new System.Random(RandomSeedOverride.Value) : new System.Random();
            var flow = difficulty.RandomFlowEnabled ? new FlowDirector(random, difficulty.Flow) : null;
            Session = new GameSession(random, difficulty.GetSeconds, difficulty.IsShuffleScore, difficulty.IsPuckShuffleScore, flow);
            audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.Initialize(backgroundMusic, matchSound, gameOverSound, Saves.Data);
            BuildUI();
            ShowMenu();
        }

        void BuildUI()
        {
            var canvasObject = new GameObject("ROLOC Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(400, 860);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var canvasRect = (RectTransform)canvasObject.transform;
            var background = Box(canvasRect, "Paper background", Paper, Vector2.zero, Vector2.zero);
            Stretch(background.rectTransform);
            background.shaded = background.shadow = false;
            safe = Container(canvasRect, "Safe area"); Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            menu = Container(safe, "Menu"); Stretch(menu);
            game = Container(safe, "Game"); Stretch(game);
            results = Container(safe, "Results"); Stretch(results);
            // Modal backdrops extend behind the notch and home indicator.
            overlay = Container(canvasRect, "Overlay"); Stretch(overlay);
            BuildMenu(); BuildBoard(); BuildResults();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var events = new GameObject("Event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        void BuildMenu()
        {
            Label(menu, "A LITTLE GAME OF FOCUS", 10, Muted, new Vector2(0, -45), new Vector2(300, 20), new Vector2(.5f, 1));
            var title = Label(menu, "roloc", 78, Ink, new Vector2(0, -121), new Vector2(310, 105), new Vector2(.5f, 1));
            title.fontStyle = FontStyle.Bold;
            Label(menu, "Find your color. Find your flow.", 15, Muted, new Vector2(0, -188), new Vector2(350, 30), new Vector2(.5f, 1));

            var sculpture = Container(menu, "Color sculpture");
            Place(sculpture, new Vector2(.5f, .51f), Vector2.zero, new Vector2(280, 230));
            int[] sculptureColors = { 1, 0, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 63;
                float y = (i < 2 ? 1 : -1) * 56;
                var shape = Circle(sculpture, "Sculpture ring " + i, Palette[sculptureColors[i]], new Vector2(x, y), 124, true);
                shape.rectTransform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? -8 : 8);
                if (i == 1 || i == 2) Circle(sculpture, "Sculpture puck " + i, Palette[sculptureColors[i]], new Vector2(x, y + 5), 47, false);
            }

            var stats = Box(menu, "Stats card", Color.white, new Vector2(0, 184), new Vector2(336, 79));
            Place(stats.rectTransform, new Vector2(.5f, 0), new Vector2(0, 184), new Vector2(336, 79));
            menuBest = Stat(stats.transform, "BEST", -110);
            menuGames = Stat(stats.transform, "PLAYED", 0);
            menuAverage = Stat(stats.transform, "AVERAGE", 110);
            Button(menu, "Let's play", new Vector2(0, 100), new Vector2(336, 60), new Vector2(.5f, 0), Ink, Color.white, BeginRun);
            Button(menu, "How to play", new Vector2(-90, 38), new Vector2(155, 40), new Vector2(.5f, 0), Color.clear, Muted, BeginTutorial);
            Button(menu, "Sound settings", new Vector2(90, 38), new Vector2(155, 40), new Vector2(.5f, 0), Color.clear, Muted, () => ShowSettings(false));
        }

        Text Stat(Transform parent, string title, float x)
        {
            Label(parent, title, 9, Muted, new Vector2(x, -19), new Vector2(100, 18));
            var value = Label(parent, "0", 23, Ink, new Vector2(x, 7), new Vector2(100, 32));
            value.fontStyle = FontStyle.Bold;
            return value;
        }

        void BuildBoard()
        {
            Label(game, "ROLOC", 13, Ink, new Vector2(49, -37), new Vector2(85, 25), new Vector2(0, 1));
            bestText = Label(game, "BEST 0", 11, Muted, new Vector2(0, -37), new Vector2(180, 25), new Vector2(.5f, 1));
            Button(game, "Ⅱ", new Vector2(-40, -37), new Vector2(46, 44), new Vector2(1, 1), Color.white, Ink, PauseRun);
            scoreText = Label(game, "0", 66, Ink, new Vector2(0, -107), new Vector2(220, 84), new Vector2(.5f, 1));
            scoreText.fontStyle = FontStyle.Bold;
            tempoText = Label(game, "TAKE A BREATH. FIND YOUR COLOR.", 9, Muted, new Vector2(0, -158), new Vector2(350, 22), new Vector2(.5f, 1));
            var timerBack = Box(game, "Timer track", new Color32(224, 227, 226, 255), new Vector2(0, -186), new Vector2(240, 6));
            Place(timerBack.rectTransform, new Vector2(.5f, 1), new Vector2(0, -186), new Vector2(240, 6));
            timerBack.shadow = false; timerBack.cornerRadius = 3;
            timerFill = Box(timerBack.transform, "Time remaining", Palette[0], Vector2.zero, new Vector2(240, 6));
            timerFill.shadow = false; timerFill.cornerRadius = 3;
            timerRect = timerFill.rectTransform; timerRect.pivot = new Vector2(0, .5f);
            timerRect.anchorMin = timerRect.anchorMax = new Vector2(0, .5f);
            timerRect.anchoredPosition = Vector2.zero;

            board = Container(game, "Board");
            Place(board, new Vector2(.5f, .43f), Vector2.zero, new Vector2(350, 440));
            for (int c = 0; c < 4; c++)
            {
                var ring = ringPrefab ? Instantiate(ringPrefab, board).GetComponent<SoftShape>() : Circle(board, "Ring " + c, Palette[c], RingSlots[c], 137, true);
                ring.name = "Ring " + c; ring.color = Palette[c]; ring.raycastTarget = false;
                rings[c] = ring.rectTransform; ringArt[c] = ring;
                Place(rings[c], new Vector2(.5f, .5f), RingSlots[c], new Vector2(137, 137));
            }
            for (int i = 0; i < guide.Length; i++)
            {
                guide[i] = Circle(board, "Tutorial path " + i, Color.white, Vector2.zero, 5, false);
                guide[i].shadow = guide[i].shaded = false;
            }
            ripple = Circle(board, "Match ripple", Palette[0], Vector2.zero, 137, true);
            ripple.shadow = ripple.shaded = false; ripple.thickness = .035f; ripple.gameObject.SetActive(false);
            for (int c = 0; c < 4; c++)
            {
                PuckView puck;
                if (puckPrefab) puck = Instantiate(puckPrefab, board).GetComponent<PuckView>();
                else puck = Circle(board, "Puck " + c, Palette[c], PuckSlots[c], 79, false).gameObject.AddComponent<PuckView>();
                int color = c;
                puck.name = "Puck " + c; puck.Configure(c, Palette[c]);
                puck.GetComponent<SoftShape>().raycastTarget = true;
                Place(puck.Rect, new Vector2(.5f, .5f), PuckSlots[c], new Vector2(79, 79));
                puck.Home = PuckSlots[c];
                puck.CanDrag = () => (Session.State == RoundState.Playing || Session.State == RoundState.Tutorial) && Session.ActiveColor == color;
                puck.Released = ReleasePuck;
                pucks[c] = puck;
            }
            instruction = Label(game, "Drag the bright puck to its ring", 15, Muted, new Vector2(0, 58), new Vector2(365, 50), new Vector2(.5f, 0));
            Label(game, "MATCH THE COLOR · KEEP THE FLOW", 9, new Color32(165, 170, 176, 255), new Vector2(0, 25), new Vector2(360, 20), new Vector2(.5f, 0));
        }

        void BuildResults()
        {
            Label(results, "A MOMENT TO RESET", 10, Muted, new Vector2(0, -60), new Vector2(320, 24), new Vector2(.5f, 1));
            resultTitle = Label(results, "Nice flow.", 39, Ink, new Vector2(0, -131), new Vector2(365, 63), new Vector2(.5f, 1));
            resultTitle.fontStyle = FontStyle.Bold;
            var medal = Circle(results, "Result ring", Palette[1], new Vector2(0, 28), 226, true);
            Place(medal.rectTransform, new Vector2(.5f, .53f), new Vector2(0, 28), new Vector2(226, 226));
            resultScore = Label(medal.transform, "0", 69, Ink, new Vector2(0, 9), new Vector2(170, 90));
            resultScore.fontStyle = FontStyle.Bold;
            Label(medal.transform, "MATCHES", 10, Muted, new Vector2(0, -43), new Vector2(130, 22));
            resultBest = Label(results, "Your best: 0", 16, Muted, new Vector2(0, -114), new Vector2(320, 36));
            Button(results, "One more round", new Vector2(0, 133), new Vector2(336, 60), new Vector2(.5f, 0), Ink, Color.white, BeginRun);
            Button(results, "Back to menu", new Vector2(0, 70), new Vector2(230, 45), new Vector2(.5f, 0), Color.clear, Muted, ShowMenu);
        }

        public void BeginRun()
        {
            if (!Saves.Data.TutorialCompleted) { BeginTutorial(); return; }
            Session.StartGame();
            gameRecorded = false;
            SetScreen(game);
            ResetBoard();
            audioPlayer.StartMusic();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void BeginTutorial()
        {
            Session.StartTutorial();
            SetScreen(game); ResetBoard();
            audioPlayer.StartMusic();
            instruction.text = "Drag the bright puck into its matching ring.\nNo timer. Take your time.";
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void ShowMenu()
        {
            CancelAllTouches(); Session.ReturnToMenu();
            audioPlayer.StopMusic();
            SetScreen(menu);
            menuBest.text = Saves.Data.HighScore.ToString();
            menuGames.text = Saves.Data.GamesPlayed.ToString();
            menuAverage.text = Saves.Data.GamesPlayed == 0 ? "—" :
                (Saves.Data.TotalScore / (double)Saves.Data.GamesPlayed).ToString("0.0");
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        void SetScreen(RectTransform active)
        {
            menu.gameObject.SetActive(active == menu); game.gameObject.SetActive(active == game);
            results.gameObject.SetActive(active == results); overlay.gameObject.SetActive(false);
        }

        void ResetBoard()
        {
            CancelAllTouches();
            transitionLeft = 0; rippleTime = 0; ripple.gameObject.SetActive(false);
            motionTime = motionBlend = 0;
            transitionRotation = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                rings[Session.RingOrder[slot]].anchoredPosition = RingSlots[slot];
                var puck = pucks[Session.PuckOrder[slot]];
                puck.Home = PuckSlots[slot]; puck.MotionPaused = false;
                puck.BoardTransitioning = false; puck.IdleOffset = Vector2.zero; puck.SnapHome();
            }
            instruction.text = "Drag the bright puck to its ring";
            bestText.text = "BEST " + Saves.Data.HighScore;
            RefreshBoard(0);
        }

        void ReleasePuck(PuckView puck)
        {
            int c = puck.ColorIndex;
            float outerRadius = (rings[c].rect.width * .5f - 5) * rings[c].localScale.x;
            bool inside = Vector2.Distance(puck.Rect.anchoredPosition, rings[c].anchoredPosition) <= outerRadius;
            FlowMode previousMode = Session.FlowMode;
            var previousRings = Session.RingOrder;
            var previousPucks = Session.PuckOrder;
            MatchResult result = Session.Drop(c, inside);
            if (result == MatchResult.Matched)
            {
                audioPlayer.PlayMatch();
                ripplePosition = rings[c].anchoredPosition; rippleColor = c; rippleTime = .4f;
                bool changed = previousMode != Session.FlowMode || previousRings != Session.RingOrder || previousPucks != Session.PuckOrder;
                BeginBoardTransition(changed, previousMode != Session.FlowMode,
                    previousRings != Session.RingOrder && previousPucks != Session.PuckOrder);
            }
            else if (result == MatchResult.Failed) FinishRun();
            else if (result == MatchResult.TutorialCompleted)
            {
                audioPlayer.PlayMatch(); audioPlayer.StopMusic();
                Saves.CompleteTutorial();
                ShowTutorialComplete();
            }
            else if (Session.State == RoundState.Tutorial)
                instruction.text = "Aim for the ring with the same color.\nYou've got this.";
        }

        void ShowTutorialComplete()
        {
            CancelAllTouches();
            var panel = NewOverlay("You've got it.", "Match before the bar runs out.\nThe pace picks up as you go.", 330);
            Button(panel, "Find my flow", new Vector2(0, -75), new Vector2(270, 55), new Vector2(.5f, .5f), Ink, Color.white, BeginRun);
        }

        void FinishRun()
        {
            if (gameRecorded) return;
            gameRecorded = true;
            bool record = Session.Score > Saves.Data.HighScore;
            Saves.RecordGame(Session.Score);
            audioPlayer.StopMusic(); audioPlayer.PlayGameOver();
            CancelAllTouches(); SetScreen(results);
            resultScore.text = Session.Score.ToString();
            resultTitle.text = record ? "A new personal best." : Session.Score > 0 ? "Nice flow." : "Find your rhythm.";
            resultTitle.fontSize = record ? 30 : 39;
            resultBest.text = record ? "A little better, one color at a time." : "Your best: " + Saves.Data.HighScore;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        public void PauseRun()
        {
            if (Session.State != RoundState.Playing && Session.State != RoundState.Tutorial && Session.State != RoundState.Transition) return;
            Session.Pause(); CancelAllTouches(); audioPlayer.PauseMusic();
            foreach (var puck in pucks) puck.MotionPaused = true;
            ShowPausePanel();
        }

        void ShowPausePanel()
        {
            var panel = NewOverlay("Take a breath.", "Your round is right here.", 390);
            Button(panel, "Keep going", new Vector2(0, -15), new Vector2(270, 55), new Vector2(.5f, .5f), Ink, Color.white, ResumeRun);
            Button(panel, "Sound settings", new Vector2(0, -80), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Muted, () => ShowSettings(true));
            Button(panel, "End round", new Vector2(0, -133), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Muted, ShowMenu);
        }

        public void ResumeRun()
        {
            if (Session.State != RoundState.Paused) return;
            overlay.gameObject.SetActive(false); Session.Resume(); audioPlayer.ResumeMusic();
            foreach (var puck in pucks) puck.MotionPaused = false;
        }

        void ShowSettings(bool fromPause)
        {
            settingsFromPause = fromPause;
            var panel = NewOverlay("Make it yours.", "The original ROLOC sounds.", 440);
            SoundToggle(panel, "Background music", 30, () => Saves.Data.MusicEnabled,
                () => Saves.Data.MusicEnabled = !Saves.Data.MusicEnabled);
            SoundToggle(panel, "Match sound", -30, () => Saves.Data.MatchEnabled,
                () => Saves.Data.MatchEnabled = !Saves.Data.MatchEnabled);
            SoundToggle(panel, "Game-over sound", -90, () => Saves.Data.GameOverEnabled,
                () => Saves.Data.GameOverEnabled = !Saves.Data.GameOverEnabled);
            Button(panel, "Done", new Vector2(0, -164), new Vector2(270, 49), new Vector2(.5f, .5f), Ink, Color.white,
                () => { if (settingsFromPause) ShowPausePanel(); else overlay.gameObject.SetActive(false); });
        }

        void SoundToggle(RectTransform panel, string label, float y, Func<bool> read, Action toggle)
        {
            var text = Label(panel, label, 15, Ink, new Vector2(-42, y), new Vector2(190, 40));
            text.alignment = TextAnchor.MiddleLeft;
            Button button = null;
            button = Button(panel, read() ? "ON" : "OFF", new Vector2(111, y), new Vector2(58, 36), new Vector2(.5f, .5f),
                read() ? Palette[2] : new Color32(223, 226, 226, 255), read() ? Color.white : Muted, () =>
                {
                    toggle(); Saves.Save(); audioPlayer.ApplyPreferences(Saves.Data);
                    button.GetComponent<SoftShape>().color = read() ? Palette[2] : new Color32(223, 226, 226, 255);
                    var caption = button.GetComponentInChildren<Text>(); caption.text = read() ? "ON" : "OFF";
                    caption.color = read() ? Color.white : Muted;
                });
            button.GetComponentInChildren<Text>().fontSize = 11;
        }

        RectTransform NewOverlay(string title, string subtitle, float height)
        {
            for (int i = overlay.childCount - 1; i >= 0; i--) Destroy(overlay.GetChild(i).gameObject);
            overlay.gameObject.SetActive(true); overlay.SetAsLastSibling();
            var scrim = Box(overlay, "Scrim", new Color(.16f, .21f, .27f, .2f), Vector2.zero, Vector2.zero);
            Stretch(scrim.rectTransform); scrim.raycastTarget = true; scrim.shadow = false; scrim.cornerRadius = 0;
            var content = Container(overlay, "Safe area"); Stretch(content);
            content.anchorMin = safe.anchorMin; content.anchorMax = safe.anchorMax;
            content.gameObject.AddComponent<SafeArea>();
            var panel = Box(content, "Panel", Paper, Vector2.zero, new Vector2(338, height));
            panel.raycastTarget = true; panel.cornerRadius = 28;
            var t = Label(panel.transform, title, 28, Ink, new Vector2(0, height / 2 - 56), new Vector2(306, 55));
            t.fontStyle = FontStyle.Bold;
            Label(panel.transform, subtitle, 14, Muted, new Vector2(0, height / 2 - 110), new Vector2(300, 50));
            return panel.rectTransform;
        }

        void Update()
        {
            if (Session == null) return;
            if (Session.Tick(Time.unscaledDeltaTime)) FinishRun();
            if (Session.State == RoundState.Transition)
            {
                transitionLeft -= Time.unscaledDeltaTime;
                AnimateBoard(1 - Mathf.Clamp01(transitionLeft / Mathf.Max(.001f, transitionDuration)));
                if (transitionLeft <= 0) CompleteBoardTransition();
            }
            if (game.gameObject.activeSelf) RefreshBoard(Time.unscaledDeltaTime);
        }

        void BeginBoardTransition(bool changed, bool modeChanged, bool bothBoards)
        {
            if (modeChanged) motionBlend = 0;
            transitionRotation = Session.RotationSteps;
            transitionBothBoards = bothBoards && transitionRotation == 0;
            transitionDuration = Mathf.Max(0, difficulty.TransitionSeconds);
            if (changed) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.FlowTransitionSeconds, 0, 1));
            if (transitionBothBoards) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.FlowTransitionSeconds * 1.5f, 0, 1.5f));
            if (transitionRotation != 0) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.RotationSeconds, 0, 1.5f));
            for (int c = 0; c < 4; c++)
            {
                ringFrom[c] = rings[c].anchoredPosition;
                puckFrom[c] = pucks[c].Rect.anchoredPosition;
                puckBeforeHomes[c] = pucks[c].Home;
                pucks[c].BoardTransitioning = true;
                pucks[c].SetHighlighted(c == Session.ActiveColor);
                pucks[c].IdleOffset = FloatOffset(c);
            }
            for (int slot = 0; slot < 4; slot++)
            {
                int c = Session.RingOrder[slot];
                ringTo[c] = RingSlots[slot] + DriftOffset(c);
                pucks[Session.PuckOrder[slot]].Home = PuckSlots[slot];
            }
            transitionLeft = transitionDuration;
            if (transitionDuration <= 0) CompleteBoardTransition();
        }

        void AnimateBoard(float progress)
        {
            float ease = Mathf.SmoothStep(0, 1, progress);
            for (int c = 0; c < 4; c++)
            {
                float ringEase = transitionBothBoards ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress * 2)) : ease;
                rings[c].anchoredPosition = Vector2.LerpUnclamped(ringFrom[c], ringTo[c], ringEase);
                if (transitionBothBoards)
                {
                    pucks[c].Rect.anchoredPosition = progress < .5f
                        ? Vector2.LerpUnclamped(puckFrom[c], puckBeforeHomes[c], ringEase)
                        : Vector2.LerpUnclamped(puckBeforeHomes[c], pucks[c].Home + pucks[c].IdleOffset,
                            Mathf.SmoothStep(0, 1, (progress - .5f) * 2));
                }
                else if (transitionRotation == 0)
                    pucks[c].Rect.anchoredPosition = Vector2.LerpUnclamped(puckFrom[c], pucks[c].Home + pucks[c].IdleOffset, ease);
                else if (progress < .3f)
                    pucks[c].Rect.anchoredPosition = Vector2.LerpUnclamped(puckFrom[c], puckBeforeHomes[c], Mathf.SmoothStep(0, 1, progress / .3f));
                else
                {
                    float angle = -transitionRotation * Mathf.PI * .5f * Mathf.SmoothStep(0, 1, (progress - .3f) / .7f);
                    Vector2 start = new Vector2(puckBeforeHomes[c].x / 47, puckBeforeHomes[c].y / 53);
                    pucks[c].Rect.anchoredPosition = new Vector2(
                        (start.x * Mathf.Cos(angle) - start.y * Mathf.Sin(angle)) * 47,
                        (start.x * Mathf.Sin(angle) + start.y * Mathf.Cos(angle)) * 53);
                }
            }
        }

        void CompleteBoardTransition()
        {
            AnimateBoard(1);
            foreach (var puck in pucks) { puck.BoardTransitioning = false; puck.SnapHome(); }
            Session.CompleteTransition();
        }

        Vector2 FloatOffset(int color)
        {
            if (Session.FlowMode != FlowMode.Floating || color == Session.ActiveColor || Session.WasTutorial) return Vector2.zero;
            float phase = motionTime * 1.7f + color * 1.8f;
            return new Vector2(Mathf.Sin(phase * .7f) * .45f, Mathf.Sin(phase))
                * Mathf.Clamp(difficulty.PuckFloatAmplitude, 0, 5) * motionBlend;
        }

        Vector2 DriftOffset(int color)
        {
            if (Session.FlowMode != FlowMode.Drifting || Session.WasTutorial) return Vector2.zero;
            float phase = motionTime * .8f + color * 1.6f;
            return new Vector2(Mathf.Sin(phase), Mathf.Cos(phase) * .65f)
                * Mathf.Clamp(difficulty.RingDriftRadius, 0, 10) * motionBlend;
        }

        void RefreshBoard(float dt)
        {
            if (Session.State == RoundState.Menu || Session.State == RoundState.GameOver) return;
            float available = safe.rect.height;
            float boardScale = Mathf.Min(1, Mathf.Min((safe.rect.width - 16) / 350, (available - 275) / 440));
            board.localScale = Vector3.one * Mathf.Max(.4f, boardScale);
            bool tutorial = Session.State == RoundState.Tutorial || Session.State == RoundState.Paused && Session.WasTutorial;
            if (Session.State == RoundState.Playing)
            {
                motionTime += dt;
                motionBlend = Mathf.MoveTowards(motionBlend, 1, dt * 2);
            }
            scoreText.text = tutorial ? "Ready?" : Session.Score.ToString(); scoreText.fontSize = tutorial ? 47 : 66;
            tempoText.text = tutorial ? "ONE MATCH. THAT'S ALL IT TAKES." : Session.Score < 5 ? "TAKE A BREATH. FIND YOUR COLOR." :
                Session.Score < 20 ? "YOU'RE FINDING YOUR FLOW." : Session.Score < 40 ? "A LITTLE FASTER NOW." : "STAY IN THE FLOW.";
            if (!tutorial && Session.FlowMode != FlowMode.Steady)
                tempoText.text = Session.FlowMode == FlowMode.Floating ? "LET IT FLOAT." :
                    Session.FlowMode == FlowMode.Drifting ? "FOLLOW THE DRIFT." :
                    Session.FlowMode == FlowMode.Breather ? "TAKE A BREATH." : "A FRESH PERSPECTIVE.";
            timerFill.gameObject.SetActive(!tutorial);
            timerRect.sizeDelta = new Vector2(240 * Mathf.Clamp01(Session.RemainingSeconds / Mathf.Max(.001f, Session.DurationSeconds)), 6);
            timerFill.color = Palette[Session.ActiveColor];
            for (int slot = 0; slot < 4; slot++)
            {
                int c = Session.RingOrder[slot];
                if (Session.State == RoundState.Playing || Session.State == RoundState.Tutorial)
                {
                    rings[c].anchoredPosition = RingSlots[slot] + DriftOffset(c);
                    pucks[c].IdleOffset = FloatOffset(c);
                }
                float bounce = rippleTime > 0 && c == rippleColor ? Mathf.Sin((.4f - rippleTime) / .4f * Mathf.PI) * .1f : 0;
                if (Session.State != RoundState.Paused) rings[c].localScale = Vector3.one * (1 + bounce);
                pucks[c].SetHighlighted(c == Session.ActiveColor);
            }
            if (rippleTime > 0 && Session.State != RoundState.Paused)
            {
                rippleTime = Mathf.Max(0, rippleTime - dt);
                ripple.gameObject.SetActive(rippleTime > 0);
                ripple.rectTransform.anchoredPosition = ripplePosition;
                ripple.rectTransform.localScale = Vector3.one * (1 + (.4f - rippleTime) * 1.5f);
                var tint = Palette[rippleColor]; tint.a = rippleTime / .4f * .65f; ripple.color = tint;
            }
            for (int i = 0; i < guide.Length; i++)
            {
                guide[i].gameObject.SetActive(tutorial && Session.State != RoundState.Paused);
                if (!tutorial) continue;
                int c = Session.ActiveColor;
                float f = (i + 1) / (float)(guide.Length + 1);
                guide[i].rectTransform.anchoredPosition = Vector2.Lerp(pucks[c].Home, rings[c].anchoredPosition, f);
                var tint = Palette[c]; tint.a = .25f + .3f * (Mathf.Sin(Time.unscaledTime * 4 - f * 6) * .5f + .5f);
                guide[i].color = tint;
            }
        }

        void CancelAllTouches() { foreach (var puck in pucks) if (puck) puck.CancelDrag(); }
        void OnApplicationPause(bool paused) { if (paused && Session != null) { PauseRun(); Saves.Save(); } }
        void OnApplicationFocus(bool focused) { if (!focused && Session != null) PauseRun(); }
        void OnDestroy() { Saves?.Save(); }

        static RectTransform Container(Transform parent, string name)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); return r;
        }
        static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        { r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; }

        SoftShape Box(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            var rect = Container(parent, name); Place(rect, new Vector2(.5f, .5f), position, size);
            var shape = rect.gameObject.AddComponent<SoftShape>(); shape.kind = SoftShape.Shape.Panel;
            shape.color = color; shape.raycastTarget = false; return shape;
        }
        SoftShape Circle(Transform parent, string name, Color color, Vector2 position, float size, bool ring)
        {
            var rect = Container(parent, name); Place(rect, new Vector2(.5f, .5f), position, Vector2.one * size);
            var shape = rect.gameObject.AddComponent<SoftShape>(); shape.kind = ring ? SoftShape.Shape.Ring : SoftShape.Shape.Disc;
            shape.color = color; shape.raycastTarget = false; return shape;
        }
        Text Label(Transform parent, string value, int size, Color color, Vector2 position, Vector2 dimensions, Vector2? anchor = null)
        {
            var rect = Container(parent, value); Place(rect, anchor ?? new Vector2(.5f, .5f), position, dimensions);
            var label = rect.gameObject.AddComponent<Text>(); label.font = typeface; label.fontSize = size;
            label.color = color; label.text = value; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow; return label;
        }
        Button Button(Transform parent, string text, Vector2 position, Vector2 size, Vector2 anchor, Color fill, Color ink, Action action)
        {
            var shape = Box(parent, text + " button", fill, position, size);
            Place(shape.rectTransform, anchor, position, size);
            shape.raycastTarget = true; shape.cornerRadius = size.y * .5f; shape.shadow = fill.a > 0;
            var button = shape.gameObject.AddComponent<Button>(); button.targetGraphic = shape;
            var colors = button.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(.85f, .87f, .9f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            Label(shape.transform, text, 16, ink, Vector2.zero, size - new Vector2(14, 0));
            return button;
        }
    }
}

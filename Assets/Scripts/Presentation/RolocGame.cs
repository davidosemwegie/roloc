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
    public sealed partial class RolocGame : MonoBehaviour
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
            new Color32(255, 120, 31, 255), new Color32(49, 93, 255, 255),
            new Color32(197, 237, 50, 255), new Color32(255, 62, 135, 255)
        };
        static readonly Color Ink = new Color32(32, 35, 68, 255);
        static readonly Color Muted = new Color32(101, 117, 146, 255);
        static readonly Color Paper = new Color32(240, 246, 252, 255);
        static readonly Vector2[] RingSlots = { new Vector2(-103, 152), new Vector2(103, 152),
            new Vector2(-103, -152), new Vector2(103, -152) };
        static readonly Vector2[] PuckSlots = { new Vector2(-47, 53), new Vector2(47, 53),
            new Vector2(-47, -53), new Vector2(47, -53) };

        RectTransform safe, menu, game, board, results, overlay, sculpture;
        Sprite wordmark;
        Font iconFont;
        readonly RectTransform[] rings = new RectTransform[4];
        readonly PuckView[] pucks = new PuckView[4];
        readonly SoftShape[] ringArt = new SoftShape[4];
        readonly SoftShape[] guide = new SoftShape[9];
        readonly Vector2[] ringFrom = new Vector2[4], ringTo = new Vector2[4];
        readonly Vector2[] puckFrom = new Vector2[4], puckBeforeHomes = new Vector2[4];
        Text scoreText, bestText, instruction, tempoText, resultScore, resultTitle, resultBest, menuBest, timeText;
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
            typeface = Resources.Load<Font>("Brand/Rounded-Bold") ?? typeface;
            if (!typeface) typeface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wordmark = Resources.Load<Sprite>("Brand/RingRushLogo");
            iconFont = Resources.Load<Font>("Brand/MaterialIconsRound-Regular");
            gameObject.name = "Ring Rush";
            if (!difficulty) difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            Saves = new SaveService(SaveDirectoryOverride);
            CreateRegularSession();
            InitializeExperience();
            audioPlayer = gameObject.AddComponent<GameAudio>();
            audioPlayer.Initialize(backgroundMusic, matchSound, gameOverSound, Saves.Data);
            BuildUI();
            InitializeAdvertising();
            ShowMenu();
        }

        void BuildUI()
        {
            var canvasObject = new GameObject("Ring Rush Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(400, 860);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var canvasRect = (RectTransform)canvasObject.transform;
            backgroundArt = Box(canvasRect, "Paper background", Paper, Vector2.zero, Vector2.zero);
            Stretch(backgroundArt.rectTransform);
            backgroundArt.shaded = backgroundArt.shadow = false;
            safe = Container(canvasRect, "Safe area"); Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            menu = Container(safe, "Menu"); Stretch(menu);
            game = Container(safe, "Game"); Stretch(game);
            results = Container(safe, "Results"); Stretch(results);
            // Modal backdrops extend behind the notch and home indicator.
            overlay = Container(canvasRect, "Overlay"); Stretch(overlay);
            BuildMenu(); BuildBoard(); BuildResults(); BuildExperienceUI();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var events = new GameObject("Event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        void BuildMenu()
        {
            var best = Button(menu, "", new Vector2(40, -30), new Vector2(62, 60), new Vector2(0, 1),
                Color.clear, Ink, ShowStats);
            Label(best.transform, "BEST", 12, Ink, new Vector2(0, 15), new Vector2(62, 20));
            menuBest = Label(best.transform, "0", 25, Ink, new Vector2(0, -9), new Vector2(62, 35));
            IconButton(menu, "Sound settings", "\uE050", new Vector2(-36, -28), new Vector2(1, 1), () => ShowSettings(false));
            Logo(menu, new Vector2(0, -120), new Vector2(230, 153), new Vector2(.5f, 1));
            Label(menu, "Find your flow. Make every match count.", 14, Ink,
                new Vector2(0, -218), new Vector2(320, 49), new Vector2(.5f, 1));

            sculpture = Container(menu, "Color sculpture");
            Place(sculpture, new Vector2(.5f, .5f), new Vector2(0, 42), new Vector2(350, 350));
            int[] colors = { 1, 0, 2, 3 };
            Vector2[] ringPositions = { new Vector2(-88, 116), new Vector2(102, 28),
                new Vector2(-52, -30), new Vector2(108, -122) };
            Vector2[] puckPositions = { new Vector2(0, 68), new Vector2(36, -42),
                new Vector2(-141, 8), new Vector2(-15, -117) };
            for (int i = 0; i < colors.Length; i++)
            {
                Circle(sculpture, "Sculpture ring " + i, Palette[colors[i]], ringPositions[i], 112, true);
                Circle(sculpture, "Sculpture puck " + i, Palette[colors[i]], puckPositions[i], 54, false);
            }
            for (int i = 0; i < 2; i++)
            {
                var dash = Box(sculpture, "Motion line", Palette[1], new Vector2(-26 - i * 6, 92 + i * 5), new Vector2(2, 13));
                dash.shadow = dash.shaded = false; dash.cornerRadius = 1;
                dash.transform.localRotation = Quaternion.Euler(0, 0, 40);
            }
            PrimaryButton(menu, "PLAY", new Vector2(0, 176), new Vector2(286, 60), new Vector2(.5f, 0), BeginRun);
            Button(menu, "How to play", new Vector2(82, 57), new Vector2(145, 44), new Vector2(.5f, 0), Color.clear, Ink, BeginTutorial);
            ColorMarks(menu);
        }

        void ShowStats()
        {
            var data = Saves.Data;
            string average = data.GamesPlayed == 0 ? "—" : (data.TotalScore / (double)data.GamesPlayed).ToString("0.0");
            var panel = NewOverlay("Your best: " + data.HighScore, "Every match counts.", 370);
            Label(panel, "Rounds played     " + data.GamesPlayed + "\nAverage score     " + average,
                17, Ink, Vector2.zero, new Vector2(285, 85));
            Button(panel, "Done", new Vector2(0, -112), new Vector2(270, 50), new Vector2(.5f, .5f),
                Palette[0], Color.white, () => overlay.gameObject.SetActive(false));
        }

        void BuildBoard()
        {
            Logo(game, new Vector2(53, -28), new Vector2(86, 57), new Vector2(0, 1));
            bestText = Label(game, "BEST 0", 13, Ink, new Vector2(0, -139), new Vector2(180, 25), new Vector2(.5f, 1));
            IconButton(game, "Pause", "\uE034", new Vector2(-36, -28), new Vector2(1, 1), PauseRun);
            scoreText = Label(game, "0", 74, Ink, new Vector2(0, -89), new Vector2(220, 95), new Vector2(.5f, 1));
            scoreText.fontStyle = FontStyle.Bold;
            tempoText = Label(game, "", 10, Muted, new Vector2(0, -211), new Vector2(350, 22), new Vector2(.5f, 1));
            var timerBack = Box(game, "Timer track", new Color32(220, 228, 239, 255), new Vector2(0, -186), new Vector2(316, 8));
            Place(timerBack.rectTransform, new Vector2(.5f, 1), new Vector2(0, -186), new Vector2(316, 8));
            timerBack.shadow = false; timerBack.cornerRadius = 4;
            timerFill = Box(timerBack.transform, "Time remaining", Palette[0], Vector2.zero, new Vector2(316, 8));
            timerFill.shadow = false; timerFill.cornerRadius = 4;
            timerRect = timerFill.rectTransform; timerRect.pivot = new Vector2(0, .5f);
            timerRect.anchorMin = timerRect.anchorMax = new Vector2(0, .5f);
            timerRect.anchoredPosition = Vector2.zero;
            timeText = Label(game, "3.0s", 13, Ink, new Vector2(140, -206), new Vector2(70, 24), new Vector2(.5f, 1));

            board = Container(game, "Board");
            Place(board, new Vector2(.5f, .43f), Vector2.zero, new Vector2(350, 440));
            InitializeVariationPresentation();
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
                puck.name = "Puck " + c; puck.Configure(c, Palette[c]);
                puck.GetComponent<SoftShape>().raycastTarget = true;
                Place(puck.Rect, new Vector2(.5f, .5f), PuckSlots[c], new Vector2(79, 79));
                puck.Home = PuckSlots[c];
                puck.CanDrag = () => Session.State == RoundState.Playing || Session.State == RoundState.Tutorial;
                puck.Released = ReleasePuck;
                pucks[c] = puck;
            }
            instruction = Label(game, "Drag the bright puck to its ring", 14, Ink, new Vector2(0, 68), new Vector2(365, 50), new Vector2(.5f, 0));
            Label(game, "Match the color. Beat the clock.", 12, Muted, new Vector2(0, 40), new Vector2(370, 25), new Vector2(.5f, 0));
            ColorMarks(game);
        }

        public void BeginRun()
        {
            RequestGameStart(BeginRunNow);
        }

        void BeginRunNow()
        {
            if (ReplayDailyIfNeeded()) return;
            if (!Saves.Data.TutorialCompleted) { BeginTutorial(); return; }
            CreateRegularSession();
            Session.StartGame();
            StartLocalCredit();
            gameRecorded = false;
            SetScreen(game);
            ResetBoard();
            audioPlayer.StartMusic();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void BeginTutorial()
        {
            dailyRun = false;
            CreateRegularSession();
            Session.StartTutorial();
            SetScreen(game); ResetBoard();
            audioPlayer.StartMusic();
            instruction.text = "Drag the bright puck into its matching ring.\nNo timer. Take your time.";
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        public void ShowMenu()
        {
            if (fullScreenAdShowing || startingAfterAd) return;
            AbandonDailyIfNeeded();
            InvalidateRevive();
            CancelAllTouches(); Session.ReturnToMenu();
            ResetVariations();
            dailyRun = false;
            audioPlayer.StopMusic();
            SetScreen(menu);
            RefreshMenuExperience();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        void SetScreen(RectTransform active)
        {
            menu.gameObject.SetActive(active == menu); game.gameObject.SetActive(active == game);
            results.gameObject.SetActive(active == results); overlay.gameObject.SetActive(false);
            board.gameObject.SetActive(true);
            RefreshAdVisibility();
        }

        void ResetBoard()
        {
            InvalidateRevive();
            ReserveBannerSpace();
            CancelAllTouches();
            transitionLeft = 0; rippleTime = 0; ripple.gameObject.SetActive(false);
            motionTime = motionBlend = 0;
            transitionRotation = 0;
            ResetVariations();
            for (int slot = 0; slot < 4; slot++)
            {
                rings[Session.RingOrder[slot]].anchoredPosition = RingSlots[slot];
                var puck = pucks[Session.PuckOrder[slot]];
                puck.Home = PuckSlots[slot]; puck.MotionPaused = false;
                puck.BoardTransitioning = false; puck.IdleOffset = Vector2.zero; puck.SnapHome();
            }
            instruction.text = "Drag the bright puck to its ring";
            bestText.text = "BEST " + CurrentRecord().HighScore;
            ApplyAppearance();
            RefreshBoard(0);
        }

        void ReleasePuck(PuckView puck)
        {
            int c = puck.ColorIndex;
            const float outerRadius = 63.5f;
            float distance = Vector2.Distance(puck.Rect.anchoredPosition, rings[c].anchoredPosition);
            bool inside = distance <= outerRadius;
            FlowMode previousMode = Session.FlowMode;
            var previousRings = Session.RingOrder;
            var previousPucks = Session.PuckOrder;
            bool inactive = c != Session.ActiveColor;
            // v1 traces already support voluntarily ending an attempt without awarding a match.
            // Use that terminal event for an inactive-puck mistake in the shared Daily.
            if (inactive && Session.DailyRulesVersion == 1) RecordDailyEvent("abandon");
            else RecordDailyEvent("drop", c, puck.Rect.anchoredPosition);
            var failure = DropFailure.MissedRing;
            if (!inside) for (int other = 0; other < 4; other++)
                if (other != c && Vector2.Distance(puck.Rect.anchoredPosition, rings[other].anchoredPosition) <= outerRadius) failure = DropFailure.WrongRing;
            MatchResult result = Session.IsDaily ? Session.DropAt(c, Mathf.RoundToInt(puck.Rect.anchoredPosition.x * 1000), Mathf.RoundToInt(puck.Rect.anchoredPosition.y * 1000))
                : Session.Drop(c, inside, distance <= outerRadius * .35f, failure);
            if (result == MatchResult.Matched)
            {
                CreditMatch();
                audioPlayer.PlayMatch(Session.Combo, Session.LastDropPerfect);
                ripplePosition = rings[c].anchoredPosition; rippleColor = c; rippleTime = Session.LastDropPerfect ? 0 : .4f;
                if (Session.LastDropPerfect) StartPerfectFeedback(ripplePosition, BoardTint(c));
                bool changed = previousMode != Session.FlowMode || previousRings != Session.RingOrder || previousPucks != Session.PuckOrder;
                BeginBoardTransition(changed, previousMode != Session.FlowMode,
                    previousRings != Session.RingOrder && previousPucks != Session.PuckOrder);
            }
            else if (result == MatchResult.ChanceLost) RetryChance();
            else if (result == MatchResult.Failed) HandleRunFailure();
            else if (result == MatchResult.TutorialCompleted)
            {
                audioPlayer.PlayMatch(); audioPlayer.StopMusic();
                Saves.CompleteTutorial();
                ShowTutorialComplete();
            }
            else if (Session.State == RoundState.Tutorial)
                instruction.text = inactive ? "Choose the bright puck.\nThen drag it to its matching ring." : "Aim for the ring with the same color.\nYou've got this.";
        }

        void ShowTutorialComplete()
        {
            CancelAllTouches();
            var panel = NewOverlay("You've got it.", "Match before the bar runs out.\nThe pace picks up as you go.", 330);
            Button(panel, "LET’S PLAY", new Vector2(0, -75), new Vector2(270, 55), new Vector2(.5f, .5f), Palette[0], Color.white, BeginRun);
        }

        void FinishRun()
        {
            if (gameRecorded) return;
            InvalidateRevive();
            if (Session.State == RoundState.AwaitingRevive) RecordDailyEvent("abandon");
            Session.EndRun();
            gameRecorded = true;
            interstitialPending = !Session.WasTutorial && Session.RevivesAvailable == 0;
            bool record = Session.Score > startingBest;
            Saves.FinalizeRun(new LocalRunSummary { RunId = localRunId, Score = Session.Score,
                LongestCombo = Session.BestCombo, LongestPerfectStreak = Session.BestPerfectStreak, PerfectCount = Session.PerfectCount });
            audioPlayer.StopMusic(); audioPlayer.PlayGameOver();
            CancelAllTouches(); SetScreen(results);
            resultScore.text = Session.Score.ToString();
            resultTitle.text = record ? "A new personal best." : Session.Score > 0 ? "Nice rush!" : "Ready to rush?";
            resultTitle.fontSize = record ? 30 : 39;
            resultBest.text = record ? "A little better, one color at a time." : "Your best: " + CurrentRecord().HighScore;
            ShowResultExperience();
            SubmitDailyIfNeeded();
            LayoutResults();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        public void PauseRun()
        {
            if (fullScreenAdShowing) return;
            if (Session.State != RoundState.Playing && Session.State != RoundState.Tutorial && Session.State != RoundState.Transition) return;
            RecordDailyEvent("pause");
            Session.Pause(); CancelAllTouches(); audioPlayer.PauseMusic();
            board.gameObject.SetActive(false);
            foreach (var puck in pucks) puck.MotionPaused = true;
            ShowPausePanel();
        }

        void ShowPausePanel()
        {
            var panel = NewOverlay("Quick breather?", "Your round is right here.", 390);
            Button(panel, "Keep going", new Vector2(0, -15), new Vector2(270, 55), new Vector2(.5f, .5f), Palette[0], Color.white, ResumeRun);
            Button(panel, "Sound settings", new Vector2(0, -80), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Muted, () => ShowSettings(true));
            Button(panel, "End round", new Vector2(0, -133), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Muted, ShowMenu);
        }

        public void ResumeRun()
        {
            if (Session.State != RoundState.Paused) return;
            RecordDailyEvent("resume");
            dailyTickAt = transitionClock = Time.realtimeSinceStartupAsDouble;
            overlay.gameObject.SetActive(false); Session.Resume(); audioPlayer.ResumeMusic();
            board.gameObject.SetActive(true);
            if (daily.IsConfigured) StartCoroutine(daily.RetryPending());
            foreach (var puck in pucks) puck.MotionPaused = false;
            RefreshAdVisibility();
        }

        void ShowSettings(bool fromPause)
        {
            settingsFromPause = fromPause;
            var panel = NewOverlay("Make it yours", "Sound, touch, and readability.", 670);
            SoundToggle(panel, "Background music", 120, () => Saves.Data.MusicEnabled,
                () => Saves.Data.MusicEnabled = !Saves.Data.MusicEnabled);
            SoundToggle(panel, "Match sound", 62, () => Saves.Data.MatchEnabled,
                () => Saves.Data.MatchEnabled = !Saves.Data.MatchEnabled);
            SoundToggle(panel, "Game-over sound", 4, () => Saves.Data.GameOverEnabled,
                () => Saves.Data.GameOverEnabled = !Saves.Data.GameOverEnabled);
            SoundToggle(panel, "Gentle haptics", -54, () => Saves.Data.HapticsEnabled, () => Saves.Data.HapticsEnabled = !Saves.Data.HapticsEnabled);
            SoundToggle(panel, "Matching symbols", -112, () => Saves.Data.SymbolsEnabled, () => Saves.Data.SymbolsEnabled = !Saves.Data.SymbolsEnabled);
            SoundToggle(panel, "Reduce effects", -170, () => Saves.Data.ReduceEffects, () => Saves.Data.ReduceEffects = !Saves.Data.ReduceEffects);
            if (HasPrivacyPolicy())
                Button(panel, "Privacy policy", new Vector2(-72, -225), new Vector2(140, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                    () => Application.OpenURL(adsConfiguration.PrivacyPolicyUrl));
            if (adConsent != null && adConsent.PrivacyOptionsRequired)
                Button(panel, "Privacy choices", new Vector2(72, -225), new Vector2(140, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                    () => ShowAdPrivacy(() => ShowSettings(fromPause)));
            Button(panel, "Done", new Vector2(0, -288), new Vector2(270, 49), new Vector2(.5f, .5f), Palette[0], Color.white,
                () => { if (settingsFromPause) ShowPausePanel(); else overlay.gameObject.SetActive(false); });
        }

        void SoundToggle(RectTransform panel, string label, float y, Func<bool> read, Action toggle)
        {
            var text = Label(panel, label, 15, Ink, new Vector2(-42, y), new Vector2(190, 40));
            text.alignment = TextAnchor.MiddleLeft;
            Button button = null;
            button = Button(panel, read() ? "ON" : "OFF", new Vector2(111, y), new Vector2(58, 36), new Vector2(.5f, .5f),
                read() ? Palette[1] : new Color32(223, 226, 226, 255), read() ? Color.white : Muted, () =>
                {
                    toggle(); Saves.Save(); audioPlayer.ApplyPreferences(Saves.Data); ApplyAppearance();
                    button.GetComponent<SoftShape>().color = read() ? Palette[1] : new Color32(223, 226, 226, 255);
                    var caption = button.GetComponentInChildren<Text>(); caption.text = read() ? "ON" : "OFF";
                    caption.color = read() ? Color.white : Muted;
                });
            button.GetComponentInChildren<Text>().fontSize = 11;
        }

        RectTransform NewOverlay(string title, string subtitle, float height)
        {
            ads?.SetBannerVisible(false);
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
            UpdateAdvertising();
            if (menu.gameObject.activeSelf)
                sculpture.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((safe.rect.width - 22) / 350, (safe.rect.height - 540) / 350), .28f, .65f);
            MatchResult tick;
            if (Session.IsDaily)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                int elapsed = Mathf.Max(0, (int)((now - dailyTickAt) * 1000));
                dailyTickAt += elapsed / 1000.0;
                tick = Session.TickMilliseconds(elapsed);
            }
            else tick = Session.TickResult(Time.unscaledDeltaTime);
            if (tick == MatchResult.Failed) { RecordDailyEvent("timeout"); HandleRunFailure(); }
            else if (tick == MatchResult.ChanceLost) RetryChance();
            UpdateExperience();
            if (Session.State == RoundState.Transition)
            {
                if (Session.IsDaily)
                {
                    double now = Time.realtimeSinceStartupAsDouble;
                    transitionLeft -= (float)(now - transitionClock); transitionClock = now;
                }
                else transitionLeft -= Time.unscaledDeltaTime;
                AnimateBoard(1 - Mathf.Clamp01(transitionLeft / Mathf.Max(.001f, transitionDuration)));
                if (transitionLeft <= 0) CompleteBoardTransition();
            }
            if (game.gameObject.activeSelf) RefreshBoard(Time.unscaledDeltaTime);
        }

        void BeginBoardTransition(bool changed, bool modeChanged, bool bothBoards)
        {
            CancelAllTouches();
            PrepareVariationTransition();
            if (modeChanged) motionBlend = 0;
            transitionRotation = Session.RotationSteps;
            transitionBothBoards = bothBoards && transitionRotation == 0;
            transitionDuration = Mathf.Max(0, difficulty.TransitionSeconds);
            if (reviveCountingDown) transitionDuration = 3;
            if (changed) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.FlowTransitionSeconds, 0, 1));
            if (transitionBothBoards) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.FlowTransitionSeconds * 1.5f, 0, 1.5f));
            if (transitionRotation != 0) transitionDuration = Mathf.Max(transitionDuration, Mathf.Clamp(difficulty.RotationSeconds, 0, 1.5f));
            if (Session.IsDaily) transitionDuration = Mathf.Max(transitionDuration, Session.RequiredTransitionMilliseconds / 1000f);
            SetVariationTransitionDuration();
            for (int c = 0; c < 4; c++)
            {
                ringFrom[c] = rings[c].anchoredPosition;
                puckFrom[c] = pucks[c].Rect.anchoredPosition;
                puckBeforeHomes[c] = pucks[c].Home;
                pucks[c].BoardTransitioning = true;
                pucks[c].SetHighlighted(c == Session.ActiveColor);
                pucks[c].IdleOffset = FloatOffset(c);
                ringTo[c] = ringFrom[c];
            }
            for (int slot = 0; slot < 4; slot++)
            {
                int c = Session.RingOrder[slot];
                ringTo[c] = Session.IsDaily ? ToVector(Session.GetRingCenter(c)) : RegularRingHome(c);
                pucks[Session.PuckOrder[slot]].Home = Session.IsDaily ? PuckSlots[slot] : RegularPuckHome(Session.PuckOrder[slot]);
            }
            transitionLeft = transitionDuration;
            transitionClock = Time.realtimeSinceStartupAsDouble;
            if (transitionDuration <= 0) CompleteBoardTransition();
        }

        void AnimateBoard(float progress)
        {
            progress = AnimateVariationTransition(progress);
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
            FinishVariationTransition();
            Session.CompleteTransition();
            dailyTickAt = Time.realtimeSinceStartupAsDouble;
            reviveCountingDown = false;
            if (reviveCountdownLabel) reviveCountdownLabel.gameObject.SetActive(false);
        }

        Vector2 FloatOffset(int color)
        {
            if (Session.BoardStyle == BoardStyle.Still || Session.IsDaily || !VariationMotion.FloatsPucks(Session.FlowMode) || color == Session.ActiveColor || Session.WasTutorial) return Vector2.zero;
            float phase = motionTime * 1.7f + color * 1.8f;
            return new Vector2(Mathf.Sin(phase * .7f) * .45f, Mathf.Sin(phase))
                * Mathf.Clamp(difficulty.PuckFloatAmplitude, 0, 5) * motionBlend;
        }

        Vector2 DriftOffset(int color)
        {
            if (Session.BoardStyle == BoardStyle.Still || Session.IsDaily || !VariationMotion.DriftsRings(Session.FlowMode) || Session.WasTutorial) return Vector2.zero;
            float phase = motionTime * .8f + color * 1.6f;
            return new Vector2(Mathf.Sin(phase), Mathf.Cos(phase) * .65f)
                * Mathf.Clamp(difficulty.RingDriftRadius, 0, Expanded(Session.FlowMode) ? 6 : 10) * motionBlend;
        }

        void RefreshBoard(float dt)
        {
            if (Session.State == RoundState.Menu || Session.State == RoundState.GameOver || Session.State == RoundState.AwaitingRevive) return;
            FitVariationBoard();
            bool tutorial = Session.State == RoundState.Tutorial || Session.State == RoundState.Paused && Session.WasTutorial;
            if (Session.State == RoundState.Playing)
            {
                motionTime += dt;
                if (Orbit(Session.FlowMode)) orbitSeconds += dt;
                motionBlend = Mathf.MoveTowards(motionBlend, 1, dt * 2);
            }
            scoreText.text = tutorial ? "Ready?" : Session.Score.ToString(); scoreText.fontSize = tutorial ? 44 : 74;
            tempoText.text = tutorial ? "No timer. Try a match." : "";
            if (!tutorial && Session.FlowMode != FlowMode.Steady)
                tempoText.text = VariationCaption();
            timerFill.gameObject.SetActive(!tutorial);
            timerRect.sizeDelta = new Vector2(316 * Mathf.Clamp01(Session.RemainingSeconds / Mathf.Max(.001f, Session.DurationSeconds)), 8);
            timerFill.color = BoardTint(Session.ActiveColor);
            RefreshPuckSymbols();
            timeText.text = tutorial ? "" : Session.RemainingSeconds.ToString("0.0") + "s";
            for (int slot = 0; slot < 4; slot++)
            {
                int c = Session.RingOrder[slot];
                if (Session.State == RoundState.Playing || Session.State == RoundState.Tutorial)
                {
                    rings[c].anchoredPosition = Session.IsDaily ? ToVector(Session.GetRingCenter(c)) : RegularRingHome(c);
                    if (!Session.IsDaily) pucks[c].Home = RegularPuckHome(c);
                    pucks[c].FollowHomeExactly = Orbit(Session.FlowMode);
                    pucks[c].IdleOffset = Session.IsDaily ? ToVector(Session.GetPuckHome(c)) - pucks[c].Home : FloatOffset(c);
                }
                float bounce = !Saves.Data.ReduceEffects && rippleTime > 0 && c == rippleColor ? Mathf.Sin((.4f - rippleTime) / .4f * Mathf.PI) * .1f : 0;
                if (Session.State != RoundState.Paused) rings[c].localScale = Vector3.one * (1 + bounce);
                pucks[c].SetHighlighted(c == Session.ActiveColor);
            }
            if (rippleTime > 0 && Session.State != RoundState.Paused)
            {
                rippleTime = Mathf.Max(0, rippleTime - dt);
                ripple.gameObject.SetActive(rippleTime > 0 && !Saves.Data.ReduceEffects);
                ripple.rectTransform.anchoredPosition = ripplePosition;
                ripple.rectTransform.localScale = Vector3.one * (1 + (.4f - rippleTime) * 1.5f);
                var tint = BoardTint(rippleColor); tint.a = rippleTime / .4f * .65f; ripple.color = tint;
            }
            for (int i = 0; i < guide.Length; i++)
            {
                guide[i].gameObject.SetActive(tutorial && Session.State != RoundState.Paused);
                if (!tutorial) continue;
                int c = Session.ActiveColor;
                float f = (i + 1) / (float)(guide.Length + 1);
                guide[i].rectTransform.anchoredPosition = Vector2.Lerp(pucks[c].Home, rings[c].anchoredPosition, f);
                var tint = BoardTint(c); tint.a = .25f + .3f * (Mathf.Sin(Time.unscaledTime * 4 - f * 6) * .5f + .5f);
                guide[i].color = tint;
            }
        }

        void CancelAllTouches() { foreach (var puck in pucks) if (puck) puck.CancelDrag(); }
        void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused && Session != null) { PauseRun(); Saves.Save(); } }
        void OnApplicationFocus(bool focused) { applicationFocused = focused; if (!focused && Session != null) PauseRun(); }
        void OnDestroy() { InvalidateRevive(); ads?.Dispose(); Saves?.Save(); }

        static RectTransform Container(Transform parent, string name)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); return r;
        }
        static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        { r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; }

        void Logo(Transform parent, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var rect = Container(parent, "Ring Rush logo"); Place(rect, anchor, position, size);
            if (wordmark)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = wordmark; image.preserveAspect = true; image.raycastTarget = false;
            }
            else Label(rect, "ring\nrush", Mathf.RoundToInt(size.y * .38f), Ink, Vector2.zero, size);
        }

        void ColorMarks(Transform parent)
        {
            int[] order = { 1, 0, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                var mark = Box(parent, "Color mark " + i, Palette[order[i]], Vector2.zero, new Vector2(19, 4));
                Place(mark.rectTransform, new Vector2(.5f, 0), new Vector2((i - 1.5f) * 31, 13), new Vector2(19, 4));
                mark.shadow = mark.shaded = false; mark.cornerRadius = 2;
            }
        }

        void IconButton(Transform parent, string name, string glyph, Vector2 position, Vector2 anchor, Action action)
        {
            var button = Button(parent, "", position, new Vector2(44, 44), anchor, Color.white, Ink, action);
            button.name = name;
            Icon(button.transform, glyph, Ink, Vector2.zero, 25);
        }

        void Icon(Transform parent, string glyph, Color tint, Vector2 position, int size)
        {
            var icon = Label(parent, glyph, size, tint, position, Vector2.one * (size + 4));
            if (iconFont) icon.font = iconFont;
        }

        void PrimaryButton(Transform parent, string title, Vector2 position, Vector2 size, Vector2 anchor, Action action)
        {
            var button = Button(parent, title, position, size, anchor, Palette[0], Color.white, action);
            var label = button.GetComponentInChildren<Text>(); label.fontSize = 34;
            label.rectTransform.anchoredPosition = new Vector2(-17, 2);
            Icon(button.transform, "\uE037", Color.white, new Vector2(77, 1), 37);
        }

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
            shape.raycastTarget = true; shape.cornerRadius = size.x <= 62 ? size.y * .5f : 17; shape.shadow = fill.a > 0;
            var button = shape.gameObject.AddComponent<Button>(); button.targetGraphic = shape;
            var colors = button.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(.85f, .87f, .9f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            Label(shape.transform, text, 16, ink, Vector2.zero, size - new Vector2(14, 0));
            return button;
        }
    }
}

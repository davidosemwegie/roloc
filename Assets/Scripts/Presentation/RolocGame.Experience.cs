using System;
using Roloc.Core;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        string localRunId;
        int startingBest, startingCombo, startingPerfect;
        long startingPoints;
        Text modeChoice, progressLabel, livesLabel, chainLabel, feedbackLabel;
        Text resultProgress, dailyLabel, resultReason;
        Button modeButton;
        SoftShape backgroundArt;
        readonly MatchSymbol[] symbols = new MatchSymbol[8];
        readonly SoftShape[] trail = new SoftShape[12];
        readonly float[] trailLife = new float[12];
        int trailIndex;
        float feedbackLeft;
        bool selectedDaily;

        void InitializeExperience()
        {
            if (!Saves.Data.EffectsPreferenceInitialized)
            {
                Saves.Data.ReduceEffects = NativeServices.ReduceMotion;
                Saves.Data.EffectsPreferenceInitialized = true;
                Saves.Save();
            }
            InitializeDaily();
        }

        void CreateRegularSession()
        {
            var mode = Saves.Data.SelectedMode == "Rush" ? GameMode.Rush : GameMode.Flow;
            // Regular runs now use Lively. Keep historical Still records in the save.
            if (Saves.Data.SelectedBoard != "Lively")
            {
                Saves.Data.SelectedBoard = "Lively";
                Saves.Save();
            }
            var style = BoardStyle.Lively;
            var random = RandomSeedOverride.HasValue ? new System.Random(RandomSeedOverride.Value) : new System.Random();
            Session = new GameSession(mode, style, random, mode == GameMode.Flow ? difficulty.GetFlowSeconds : difficulty.GetSeconds,
                difficulty.IsShuffleScore, difficulty.IsPuckShuffleScore, difficulty.RandomFlowEnabled, difficulty.Variations);
        }

        ModeRecord CurrentRecord() => Saves.GetRecord(Session.Mode.ToString(), Session.BoardStyle.ToString());

        void BuildExperienceUI()
        {
            modeButton = Button(menu, "", new Vector2(0, 282), new Vector2(286, 44), new Vector2(.5f, 0), Color.white, Ink, () => {
                Saves.Data.SelectedMode = Saves.Data.SelectedMode == "Flow" ? "Rush" : "Flow";
                selectedDaily = false; Saves.Save(); RefreshMenuExperience();
            });
            modeChoice = Label(modeButton.transform, "FLOW · 3 CHANCES", 12, Ink, Vector2.zero, new Vector2(280, 42));
            progressLabel = Label(menu, "", 12, Muted, new Vector2(0, 234), new Vector2(350, 35), new Vector2(.5f, 0));
            var dailyButton = Button(menu, "", new Vector2(0, 114), new Vector2(286, 48), new Vector2(.5f, 0), Palette[1], Color.white, ShowDaily);
            dailyLabel = Label(dailyButton.transform, "DAILY · SAME BOARD FOR EVERYONE", 12, Color.white, Vector2.zero, new Vector2(280, 44));
            Button(menu, "Collection", new Vector2(-82, 57), new Vector2(145, 44), new Vector2(.5f, 0), Color.clear, Ink, ShowCollection);
            livesLabel = Label(game, "", 11, Ink, new Vector2(-92, -161), new Vector2(170, 22), new Vector2(.5f, 1));
            chainLabel = Label(game, "", 11, Ink, new Vector2(90, -161), new Vector2(172, 22), new Vector2(.5f, 1));
            feedbackLabel = Label(game, "", 15, Ink, new Vector2(0, 104), new Vector2(360, 32), new Vector2(.5f, 0));
            for (int c = 0; c < 4; c++)
            {
                symbols[c] = MakeSymbol(pucks[c].transform, c, 22);
                symbols[c + 4] = MakeSymbol(rings[c], c, 22);
                pucks[c].Moved = DrawTrail;
            }
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Circle(board, "Ribbon " + i, Palette[0], Vector2.zero, 13, false);
                trail[i].shadow = trail[i].shaded = false; trail[i].transform.SetAsFirstSibling();
                trail[i].gameObject.SetActive(false);
            }
            BuildDailyUI();
        }

        MatchSymbol MakeSymbol(Transform parent, int colorIndex, int size)
        {
            var r = Container(parent, "Matching symbol"); Place(r, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * size);
            var symbol = r.gameObject.AddComponent<MatchSymbol>(); symbol.Symbol = colorIndex;
            symbol.color = Ink; symbol.raycastTarget = false; return symbol;
        }

        void StartLocalCredit()
        {
            var record = CurrentRecord(); startingBest = record.HighScore;
            startingCombo = record.LongestCombo; startingPerfect = record.LongestPerfectStreak;
            startingPoints = Saves.Data.ProgressPoints;
            localRunId = Saves.StartRun(Session.Mode.ToString(), Session.BoardStyle.ToString());
            if (Session.Mode == GameMode.Flow && !Saves.Data.ChancesHintShown)
            {
                Hint("Three chances. A mistake retries this color.", 5);
                Saves.Data.ChancesHintShown = true; Saves.Save();
            }
        }

        void CreditMatch()
        {
            Saves.RecordMatch(localRunId, Session.Score, Session.Mode.ToString(), Session.BoardStyle.ToString(),
                Session.LastDropPerfect, Session.Combo, Session.PerfectStreak);
            bool milestone = Session.Combo == 5 || Session.Combo == 10 || Session.Combo == 25 || Session.Combo % 50 == 0;
            NativeServices.Haptic(Saves.Data.HapticsEnabled, milestone || Session.LastChanceRestored);
            if (Session.LastChanceRestored)
            {
                Hint("Chance restored · 15 clean matches", 2.5f);
                Saves.Data.RecoveryHintShown = true; Saves.Save();
            }
            else if (Session.LastDropPerfect && !Saves.Data.PerfectHintShown)
            {
                Hint("Perfect! Centered drops earn +1 progress.", 3.5f);
                Saves.Data.PerfectHintShown = true; Saves.Save();
            }
            else if (milestone) Hint(Session.Combo + " in a row!", 1.3f);
            else if (Session.LastDropPerfect) Hint(Session.PerfectStreak > 1 ? "Perfect × " + Session.PerfectStreak : "Perfect!", .8f);
        }

        void RetryChance()
        {
            CancelAllTouches();
            Hint(Session.LastFailure == DropFailure.InactivePuck ? "Choose the bright puck · try again" :
                Session.LastFailure == DropFailure.TimeExpired ? "Time ran out · try this color again" : "Outside the matching ring · try again", 2.8f);
            BeginBoardTransition(false, false, false);
            if (transitionLeft > 0) { transitionDuration = transitionLeft = Mathf.Max(.45f, transitionDuration); }
            if (!Saves.Data.RecoveryHintShown)
            {
                instruction.text = "15 matches without a miss restores a chance.";
                Saves.Data.RecoveryHintShown = true; Saves.Save();
            }
        }

        void Hint(string message, float seconds) { if (feedbackLabel) feedbackLabel.text = message; feedbackLeft = seconds; }

        void UpdateExperience()
        {
            if (feedbackLeft > 0 && Session.State != RoundState.Paused)
            {
                feedbackLeft -= Time.unscaledDeltaTime;
                if (feedbackLeft <= 0) feedbackLabel.text = "";
            }
            if (livesLabel) livesLabel.text = Session.WasTutorial ? "" : Session.Mode == GameMode.Flow ? Session.Chances + " / 3 CHANCES" : Session.IsDaily ? "DAILY · RUSH" : "RUSH · 1 CHANCE";
            if (chainLabel) chainLabel.text = Session.WasTutorial ? "" : "COMBO " + Session.Combo + "  ·  PERFECT " + Session.PerfectStreak;
            for (int i = 0; i < trail.Length; i++)
            {
                if (!trail[i] || trailLife[i] <= 0) continue;
                if (Session.State == RoundState.Paused) continue;
                trailLife[i] -= Time.unscaledDeltaTime;
                var tint = trail[i].color; tint.a = Mathf.Max(0, trailLife[i]) * .7f;
                trail[i].color = tint; trail[i].gameObject.SetActive(trailLife[i] > 0 && !Saves.Data.ReduceEffects);
            }
        }

        string NextProgress()
        {
            var snapshot = Saves.GetProgressSnapshot();
            return snapshot.AllUnlocked ? snapshot.TotalMatches + " lifetime matches · " + snapshot.MatchesToMilestone + " to " + snapshot.NextMatchMilestone
                : snapshot.PointsToNextUnlock + " more progress to unlock " + snapshot.NextUnlock.Name;
        }

        void RefreshMenuExperience()
        {
            selectedDaily = false;
            menuBest.text = Saves.GetRecord(Saves.Data.SelectedMode, Saves.Data.SelectedBoard).HighScore.ToString();
            if (modeChoice) modeChoice.text = Saves.Data.SelectedMode == "Rush" ? "RUSH · 1 CHANCE" : "FLOW · 3 CHANCES";
            if (progressLabel) progressLabel.text = NextProgress();
            ApplyAppearance();
            if (Saves.ShouldSuggestRush())
            {
                Saves.Data.RushSuggestionShown = true; Saves.Save();
                var panel = NewOverlay("Ready for Rush?", "One chance. A quicker clock.\nYour Flow record stays yours.", 360);
                Button(panel, "Try Rush", new Vector2(0, -40), new Vector2(270, 50), new Vector2(.5f, .5f), Palette[1], Color.white,
                    () => { Saves.Data.SelectedMode = "Rush"; Saves.Save(); overlay.gameObject.SetActive(false); RefreshMenuExperience(); });
                Button(panel, "Keep my flow", new Vector2(0, -102), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                    () => overlay.gameObject.SetActive(false));
            }
            LoadFinalStandingOnReturn();
        }

        void ShowResultExperience()
        {
            resultReason.text = Session.LastFailure == DropFailure.InactivePuck ? "Choose the highlighted puck next time." :
                Session.LastFailure == DropFailure.TimeExpired ? "Just out of time. One more?" :
                Session.LastFailure == DropFailure.WrongRing ? "Right puck, different color's ring." : "Just outside the matching ring.";
            resultBest.text = Session.Mode.ToString().ToUpperInvariant() + " · " + Session.BoardStyle.ToString().ToUpperInvariant();
            RefreshResultRewards();
            if (dailyStandingLabel) dailyStandingLabel.gameObject.SetActive(dailyRun);
            if (shareButton) shareButton.gameObject.SetActive(dailyRun);
        }

        void ShowCollection()
        {
            var panel = NewOverlay("Your collection", Saves.Data.ProgressPoints + " progress · " + NextProgress(), 660);
            int i = 0;
            foreach (var item in CosmeticCatalog.Unlocks)
            {
                var captured = item; float y = 145 - i++ * 65;
                var preview = Circle(panel, item.Name + " preview", item.Category == CosmeticCategory.Background ? new Color32(211, 208, 240, 255) : Palette[(i - 1) % 4], new Vector2(-112, y), 46, item.Category == CosmeticCategory.Ring);
                preview.Finish = item.Id;
                if (item.Category == CosmeticCategory.Trail) { preview.kind = SoftShape.Shape.Arc; preview.progress = .65f; }
                bool unlocked = Saves.Data.ProgressPoints >= item.UnlockAt;
                bool equipped = Saves.GetEquipped(item.Category) == item.Id;
                Label(panel, item.Name + " · " + item.Category, 13, Ink, new Vector2(2, y + 13), new Vector2(160, 24));
                Label(panel, unlocked ? (equipped ? "Equipped" : "Unlocked") : item.UnlockAt + " progress", 11, Muted, new Vector2(2, y - 11), new Vector2(160, 24));
                var choose = Button(panel, equipped ? "OFF" : unlocked ? "USE" : "LOCK", new Vector2(118, y), new Vector2(51, 44), new Vector2(.5f, .5f), Color.white, Ink, () => {
                    Saves.Equip(captured.Category, Saves.GetEquipped(captured.Category) == captured.Id ? "classic" : captured.Id);
                    ApplyAppearance(); ShowCollection();
                });
                choose.interactable = unlocked;
                choose.GetComponentInChildren<Text>().fontSize = 10;
            }
            Button(panel, "Daily goals", new Vector2(-70, -265), new Vector2(135, 48), new Vector2(.5f, .5f), Color.white, Ink, ShowGoals);
            Button(panel, "Done", new Vector2(76, -265), new Vector2(135, 48), new Vector2(.5f, .5f), Palette[0], Color.white, () => overlay.gameObject.SetActive(false));
        }

        void ShowGoals()
        {
            var goals = Saves.GetDailyGoals();
            var panel = NewOverlay("A little every day", "Two goals. +10 progress each.\nRefreshes at midnight UTC.", 390);
            for (int i = 0; i < goals.Count; i++)
                Label(panel, goals[i].Label + "\n" + goals[i].Current + " / " + goals[i].Target + (goals[i].Awarded ? " · Complete!" : ""), 16, Ink,
                    new Vector2(0, 10 - i * 75), new Vector2(294, 65));
            Button(panel, "Done", new Vector2(0, -138), new Vector2(270, 48), new Vector2(.5f, .5f), Palette[0], Color.white, () => overlay.gameObject.SetActive(false));
        }

        void ApplyAppearance()
        {
            if (backgroundArt) backgroundArt.color = Saves.Data.EquippedBackground == "dusk" ? new Color32(217, 216, 237, 255) : Paper;
            for (int c = 0; c < 4; c++)
            {
                if (!pucks[c]) continue;
                pucks[c].ReduceEffects = Saves.Data.ReduceEffects;
                pucks[c].GetComponent<SoftShape>().Finish = Saves.Data.EquippedPuck;
                ringArt[c].Finish = Saves.Data.EquippedRing;
                if (symbols[c]) symbols[c].gameObject.SetActive(Saves.Data.SymbolsEnabled);
                if (symbols[c + 4]) symbols[c + 4].gameObject.SetActive(Saves.Data.SymbolsEnabled);
            }
        }

        void DrawTrail(PuckView puck)
        {
            if (Saves.Data.ReduceEffects || Saves.Data.EquippedTrail != "ribbon") return;
            var dot = trail[trailIndex]; dot.rectTransform.anchoredPosition = puck.Rect.anchoredPosition;
            dot.color = BoardTint(puck.ColorIndex); dot.gameObject.SetActive(true); trailLife[trailIndex] = .26f;
            trailIndex = (trailIndex + 1) % trail.Length;
        }
        static Vector2 ToVector(BoardPoint point) => new Vector2(point.X / 1000f, point.Y / 1000f);
    }
}

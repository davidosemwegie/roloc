using System;
using Roloc.Core;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        LeaderboardClient leaderboards;
        LeaderboardTicket regularTicket;
        Button leaderboardResultButton;
        int leaderboardViewGeneration, regularStartGeneration, regularRevives;
        bool regularStarting, regularSubmitted, leaderboardRetrying;
        double regularStartedAt;
        string regularRankingCaption = "Local score · join Leaderboards to compete";
        string leaderboardMode = "Flow", leaderboardDay = "today";
        bool leaderboardVisible;

        void InitializeLeaderboards()
        {
            leaderboards = new LeaderboardClient(daily, SaveDirectoryOverride);
            leaderboards.ResultChanged += LeaderboardResultChanged;
        }

        void BuildLeaderboardUI()
        {
            var button = Button(menu, "Leaderboards", new Vector2(0, 57), new Vector2(116, 44),
                new Vector2(.5f, 0), Color.clear, Ink, OpenLeaderboards);
            button.GetComponentInChildren<Text>().fontSize = 12;
            leaderboardResultButton = Button(resultFooter, "Leaderboards", Vector2.zero, new Vector2(170, 44),
                new Vector2(.5f, .5f), Color.clear, Palette[1], OpenLeaderboards);
            RetryLeaderboards();
        }

        void StartRegularLeaderboardRun()
        {
            if (regularStarting) return;
            regularTicket = null;
            regularSubmitted = false;
            regularRankingCaption = "Local score · join Leaderboards to compete";
            if (!leaderboards.IsConfigured || !leaderboards.IsParticipating) { LaunchRegularRun(); return; }
            regularStarting = true;
            int generation = ++regularStartGeneration;
            string mode = Saves.Data.SelectedMode == "Rush" ? "Rush" : "Flow";
            var panel = NewOverlay("Ready to compete", "Connecting your " + mode + " run…", 300);
            Button(panel, "Back", new Vector2(0, -92), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                () => { CancelRegularLeaderboardStart(); overlay.gameObject.SetActive(false); });
            StartCoroutine(leaderboards.StartRun(mode.ToLowerInvariant(), ticket => {
                if (generation != regularStartGeneration) return;
                regularStarting = false; regularTicket = ticket;
                regularRankingCaption = "GLOBAL DAILY · your best score counts";
                LaunchRegularRun();
            }, error => {
                if (generation != regularStartGeneration) return;
                regularStarting = false;
                regularRankingCaption = "Local score · leaderboard connection unavailable";
                LaunchRegularRun();
            }));
        }

        void CancelRegularLeaderboardStart() { regularStartGeneration++; regularStarting = false; }

        void RegularLeaderboardStarted()
        {
            regularStartedAt = Time.realtimeSinceStartupAsDouble;
            regularRevives = 0;
            Hint(regularTicket != null ? "Global daily leaderboard · your best counts" : regularRankingCaption, 3);
        }

        void SubmitRegularLeaderboard()
        {
            if (dailyRun || Session.WasTutorial || regularSubmitted || regularTicket == null || Session.State != RoundState.GameOver) return;
            regularSubmitted = true;
            var ticket = regularTicket;
            SetRegularRanking("Saving your leaderboard score…");
            StartCoroutine(leaderboards.SubmitRun(ticket, Session.Score, regularRevives,
                Math.Max(0, (Time.realtimeSinceStartupAsDouble - regularStartedAt) * 1000), value => {
                    if (regularTicket != null && regularTicket.runId == ticket.runId) LeaderboardResultChanged(value);
                }, error => {
                    if (regularTicket != null && regularTicket.runId == ticket.runId)
                        SetRegularRanking(error);
                }));
        }

        void SetRegularRanking(string caption)
        {
            regularRankingCaption = caption;
            if (!dailyRun && dailyStandingLabel) { dailyStandingLabel.text = caption; LayoutResults(); }
        }

        void LeaderboardResultChanged(LeaderboardTicket ticket)
        {
            if (regularTicket != null && regularTicket.runId == ticket.runId)
                SetRegularRanking(ticket.status == "accepted" ? "Score saved · view your global daily rank" :
                    ticket.status == "expired" ? "Upload window closed · local score saved" : "Score not ranked · local score saved");
            if (ticket.status == "accepted" && leaderboardVisible && overlay.gameObject.activeSelf)
                ShowLeaderboardBoard();
        }

        void RetryLeaderboards()
        {
            if (leaderboards == null || !leaderboards.IsConfigured || leaderboardRetrying) return;
            leaderboardRetrying = true;
            StartCoroutine(leaderboards.RetryPending(_ => leaderboardRetrying = false, _ => leaderboardRetrying = false));
        }

        void OpenLeaderboards()
        {
            leaderboardMode = dailyRun ? "Flow" : Session.Mode == GameMode.Rush ? "Rush" : Saves.Data.SelectedMode == "Rush" ? "Rush" : "Flow";
            leaderboardDay = "today";
            ShowLeaderboardBoard();
            RetryLeaderboards();
        }

        bool LeaderboardViewCurrent(int generation, RectTransform panel)
            => generation == leaderboardViewGeneration && panel && overlay.gameObject.activeSelf;

        void ShowLeaderboardBoard()
        {
            float height = Mathf.Min(620, safe.rect.height - 16);
            var panel = NewOverlay("Leaderboards", "Global daily highscores · best run per player", height);
            leaderboardVisible = true;
            int generation = leaderboardViewGeneration;
            float top = height / 2;
            LeaderboardTab(panel, "Flow", -75, top - 158, leaderboardMode == "Flow", () => { leaderboardMode = "Flow"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Rush", 75, top - 158, leaderboardMode == "Rush", () => { leaderboardMode = "Rush"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Today", -75, top - 205, leaderboardDay == "today", () => { leaderboardDay = "today"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Yesterday", 75, top - 205, leaderboardDay == "yesterday", () => { leaderboardDay = "yesterday"; ShowLeaderboardBoard(); });
            var status = Label(panel, "Loading scores…", 12, Muted, new Vector2(0, top - 247), new Vector2(298, 38));
            float listTop = top - 270, listBottom = -top + 147;
            var viewport = Container(panel, "Leaderboard viewport");
            Place(viewport, new Vector2(.5f, .5f), new Vector2(0, (listTop + listBottom) / 2), new Vector2(302, Mathf.Max(44, listTop - listBottom)));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Container(viewport, "Leaderboard entries");
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var personal = Label(panel, "Your best: no ranked score yet", 12, Ink, new Vector2(0, -top + 122), new Vector2(300, 38));
            Button(panel, "Nickname & participation", new Vector2(0, -top + 77), new Vector2(294, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                () => OpenLeaderboardProfile(ShowLeaderboardBoard));
            Button(panel, "Back", new Vector2(0, -top + 29), new Vector2(142, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                () => { leaderboardVisible = false; leaderboardViewGeneration++; overlay.gameObject.SetActive(false); });
            StartCoroutine(leaderboards.GetBoard(leaderboardMode.ToLowerInvariant(), leaderboardDay, value => {
                if (!LeaderboardViewCurrent(generation, panel)) return;
                RenderLeaderboardBoard(value, content, status, personal);
            }, error => {
                if (!LeaderboardViewCurrent(generation, panel)) return;
                status.text = "Couldn’t load scores. Connect and retry.";
                Button(content, "Retry", new Vector2(151, -24), new Vector2(270, 44), new Vector2(0, 1), Color.clear, Palette[1], ShowLeaderboardBoard);
                content.sizeDelta = new Vector2(302, 48);
            }));
        }

        void LeaderboardTab(RectTransform panel, string caption, float x, float y, bool selected, Action action)
        {
            Button(panel, caption, new Vector2(x, y), new Vector2(142, 44), new Vector2(.5f, .5f),
                selected ? Palette[1] : Color.white, selected ? Color.white : Ink, action);
        }

        void RenderLeaderboardBoard(LeaderboardBoard value, RectTransform content, Text status, Text personal)
        {
            if (value == null) { status.text = "Leaderboard unavailable. Try again shortly."; return; }
            status.text = value.date + " UTC · " + value.participants + " players · " + (value.provisional ? "Provisional" : "Final")
                + (value.enabled ? "" : "\nSubmissions paused");
            var entries = value.entries ?? new LeaderboardEntry[0];
            if (entries.Length == 0)
                Label(content, "No scores yet. Be the first!", 14, Muted, new Vector2(151, -24), new Vector2(296, 44), new Vector2(0, 1));
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var row = Box(content, "Leaderboard row " + i, entry.isMe ? new Color32(223, 239, 249, 255) : Color.white,
                    new Vector2(151, -24 - i * 48), new Vector2(296, 44));
                Place(row.rectTransform, new Vector2(0, 1), new Vector2(151, -24 - i * 48), new Vector2(296, 44));
                row.shadow = row.shaded = false;
                Label(row.transform, "#" + entry.rank, 13, Muted, new Vector2(-115, 0), new Vector2(50, 38));
                var name = Label(row.transform, entry.nickname + (entry.isMe ? " · you" : ""), 13, Ink, new Vector2(-10, 0), new Vector2(156, 38));
                name.supportRichText = false; name.alignment = TextAnchor.MiddleLeft;
                Label(row.transform, entry.score.ToString(), 17, Palette[1], new Vector2(111, 0), new Vector2(65, 38));
            }
            content.sizeDelta = new Vector2(302, Mathf.Max(48, entries.Length * 48));
            content.anchoredPosition = Vector2.zero;
            personal.text = value.personal == null ? "Your best: no ranked score yet" : "Your best: #" + value.personal.rank + " · " + value.personal.score + " matches";
        }

        void OpenLeaderboardProfile(Action back)
        {
            leaderboardVisible = false;
            var panel = NewOverlay("Your leaderboard", "Loading your player profile…", 340);
            int generation = leaderboardViewGeneration;
            Button(panel, "Back", new Vector2(0, -98), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink, back);
            StartCoroutine(leaderboards.LoadProfile(profile => {
                if (LeaderboardViewCurrent(generation, panel)) ShowLeaderboardProfile(profile, back);
            }, error => {
                if (!LeaderboardViewCurrent(generation, panel)) return;
                ShowLeaderboardProfile(leaderboards.CachedProfile, back, "Connect to save your nickname and participation.");
            }));
        }

        void ShowLeaderboardProfile(LeaderboardProfile profile, Action back, string error = null)
        {
            var panel = NewOverlay("Your leaderboard", "A unique nickname for public Flow and Rush scores.\n3–16 letters, numbers or underscores.", 500);
            int generation = leaderboardViewGeneration;
            bool participating = profile == null || profile.participating;
            var fieldBox = Box(panel, "Nickname input", Color.white, new Vector2(0, 62), new Vector2(292, 48));
            var input = fieldBox.gameObject.AddComponent<InputField>();
            var text = Label(fieldBox.transform, profile?.nickname ?? "", 18, Ink, Vector2.zero, new Vector2(270, 44));
            text.supportRichText = false; text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text; input.targetGraphic = fieldBox; input.characterLimit = 16;
            input.lineType = InputField.LineType.SingleLine; input.text = profile?.nickname ?? "";
            input.onValidateInput = (_, __, character) => IsNicknameCharacter(character) ? character : '\0';
            var note = Label(panel, error ?? "Joining publishes your best eligible score each day.\nTurning off stops new ranked starts.", 12, Muted, new Vector2(0, -59), new Vector2(296, 72));
            Button toggle = null;
            toggle = Button(panel, participating ? "PARTICIPATION ON" : "PARTICIPATION OFF", new Vector2(0, 0), new Vector2(292, 44),
                new Vector2(.5f, .5f), Color.white, Ink, () => {
                    participating = !participating;
                    toggle.GetComponentInChildren<Text>().text = participating ? "PARTICIPATION ON" : "PARTICIPATION OFF";
                });
            Button save = null;
            save = Button(panel, profile == null ? "Join" : "Save", new Vector2(0, -145), new Vector2(270, 49), new Vector2(.5f, .5f), Palette[1], Color.white, () => {
                string nickname = input.text.Trim();
                if (!ValidLeaderboardNickname(nickname)) { note.text = "Use 3–16 letters, numbers or underscores."; return; }
                save.interactable = false;
                StartCoroutine(leaderboards.SetProfile(nickname, participating, updated => {
                    if (LeaderboardViewCurrent(generation, panel)) back();
                }, message => {
                    if (!LeaderboardViewCurrent(generation, panel)) return;
                    note.text = message; save.interactable = true;
                }));
            });
            Button(panel, "Back", new Vector2(0, -207), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink, back);
        }

        static bool IsNicknameCharacter(char value) => value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z' || value >= '0' && value <= '9' || value == '_';
        static bool ValidLeaderboardNickname(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 3 || value.Length > 16) return false;
            foreach (char character in value) if (!IsNicknameCharacter(character)) return false;
            return true;
        }
    }
}

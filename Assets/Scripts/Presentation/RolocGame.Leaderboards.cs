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
                Math.Max(0, (Time.realtimeSinceStartupAsDouble - regularStartedAt) * 1000), null, error => {
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
            CaptureTelemetry("leaderboard_viewed");
            leaderboardMode = dailyRun ? "Flow" : Session.Mode == GameMode.Rush ? "Rush" : Saves.Data.SelectedMode == "Rush" ? "Rush" : "Flow";
            leaderboardDay = "today";
            ShowLeaderboardBoard();
            RetryLeaderboards();
        }

        bool LeaderboardViewCurrent(int generation, RectTransform panel)
            => generation == leaderboardViewGeneration && panel && overlay.gameObject.activeSelf;

        void ShowLeaderboardBoard()
        {
            float height = Mathf.Min(580, safe.rect.height - 24);
            var panel = NewOverlayPanel(height);
            leaderboardVisible = true;
            int generation = leaderboardViewGeneration;
            float top = height / 2;
            var title = Label(panel, "Leaderboards", 26, Ink, new Vector2(0, top - 37), new Vector2(298, 38));
            title.fontStyle = FontStyle.Bold;
            Label(panel, "Your best run each UTC day", 13, Muted, new Vector2(0, top - 72), new Vector2(298, 24));
            LeaderboardTab(panel, "Flow", -77, top - 116, leaderboardMode == "Flow", () => { leaderboardMode = "Flow"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Rush", 77, top - 116, leaderboardMode == "Rush", () => { leaderboardMode = "Rush"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Today", -77, top - 172, leaderboardDay == "today", () => { leaderboardDay = "today"; ShowLeaderboardBoard(); });
            LeaderboardTab(panel, "Yesterday", 77, top - 172, leaderboardDay == "yesterday", () => { leaderboardDay = "yesterday"; ShowLeaderboardBoard(); });
            var status = Label(panel, "Loading scores…", 12, Muted, new Vector2(0, top - 226), new Vector2(298, 40));
            float listTop = top - 258, listBottom = -top + 163;
            var viewport = Container(panel, "Leaderboard viewport");
            Place(viewport, new Vector2(.5f, .5f), new Vector2(0, (listTop + listBottom) / 2), new Vector2(302, Mathf.Max(44, listTop - listBottom)));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Container(viewport, "Leaderboard entries");
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var personal = Label(panel, "Your best: no ranked score yet", 12, Ink, new Vector2(0, -top + 132), new Vector2(298, 38));
            bool hasNickname = !string.IsNullOrEmpty(leaderboards.CachedProfile?.nickname);
            var profileButton = Button(panel, hasNickname ? "Your profile" : "Join leaderboards", new Vector2(0, -top + 82), new Vector2(294, 44),
                new Vector2(.5f, .5f), hasNickname ? Color.white : Palette[1], hasNickname ? Ink : Color.white,
                () => OpenLeaderboardProfile(ShowLeaderboardBoard));
            profileButton.GetComponent<SoftShape>().shadow = false;
            Button(panel, "Back", new Vector2(0, -top + 30), new Vector2(142, 44), new Vector2(.5f, .5f), Color.clear, Ink,
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
            var button = Button(panel, caption, new Vector2(x, y), new Vector2(142, 44), new Vector2(.5f, .5f),
                selected ? Palette[1] : Color.white, selected ? Color.white : Ink, action);
            button.GetComponent<SoftShape>().shadow = false;
            button.GetComponentInChildren<Text>().fontSize = 15;
        }

        void RenderLeaderboardBoard(LeaderboardBoard value, RectTransform content, Text status, Text personal)
        {
            if (value == null) { status.text = "Leaderboard unavailable. Try again shortly."; return; }
            status.text = value.date + " UTC · " + value.participants + " players\n"
                + (value.provisional ? "Provisional" : "Final") + (value.enabled ? " results" : " · submissions paused");
            var entries = value.entries ?? new LeaderboardEntry[0];
            if (entries.Length == 0)
            {
                float emptyHeight = ((RectTransform)content.parent).rect.height;
                content.sizeDelta = new Vector2(302, emptyHeight);
                content.anchoredPosition = Vector2.zero;
                Label(content, value.enabled ? "No scores yet\nPlay a ranked run to join the board." : "Rankings are paused\nYou can still choose your nickname.",
                    14, Muted, new Vector2(151, -emptyHeight / 2), new Vector2(296, 60), new Vector2(0, 1));
            }
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
            if (entries.Length > 0) content.sizeDelta = new Vector2(302, entries.Length * 48);
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
            bool joining = string.IsNullOrEmpty(profile?.nickname);
            const float height = 500;
            var panel = NewOverlayPanel(height);
            float top = height / 2;
            var title = Label(panel, joining ? "Join leaderboards" : "Your nickname", 25, Ink, new Vector2(0, top - 37), new Vector2(298, 38));
            title.fontStyle = FontStyle.Bold;
            Label(panel, "Choose a name other players will see.", 14, Muted, new Vector2(0, top - 79), new Vector2(298, 40));
            int generation = leaderboardViewGeneration;
            bool participating = joining || profile.participating;
            var fieldLabel = Label(panel, "Nickname", 14, Ink, new Vector2(0, top - 120), new Vector2(292, 24));
            fieldLabel.alignment = TextAnchor.MiddleLeft;
            var fieldBox = Box(panel, "Nickname input", Color.white, new Vector2(0, top - 162), new Vector2(292, 48));
            fieldBox.raycastTarget = true; fieldBox.shadow = false;
            var input = fieldBox.gameObject.AddComponent<InputField>();
            var text = Label(fieldBox.transform, profile?.nickname ?? "", 18, Ink, Vector2.zero, new Vector2(270, 44));
            text.supportRichText = false; text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text; input.targetGraphic = fieldBox; input.characterLimit = 16;
            var placeholder = Label(fieldBox.transform, "Enter nickname", 16, Muted, Vector2.zero, new Vector2(270, 44));
            placeholder.alignment = TextAnchor.MiddleLeft;
            input.placeholder = placeholder;
            input.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            input.customCaretColor = true; input.caretColor = Ink;
            var inputColors = input.colors;
            inputColors.selectedColor = new Color32(218, 232, 255, 255);
            input.colors = inputColors;
            input.lineType = InputField.LineType.SingleLine; input.text = profile?.nickname ?? "";
            input.onValidateInput = (_, __, character) => IsNicknameCharacter(character) ? character : '\0';
            Label(panel, "3–16 letters, numbers or underscores.", 12, Muted, new Vector2(0, top - 208), new Vector2(292, 32));
            if (joining)
                Label(panel, "Your nickname is public.\nYour best Flow and Rush run counts each day.", 14, Muted,
                    new Vector2(0, top - 265), new Vector2(292, 64));
            else
            {
                Button toggle = null;
                toggle = Button(panel, participating ? "Rank my runs: ON" : "Rank my runs: OFF", new Vector2(0, top - 264), new Vector2(292, 44),
                    new Vector2(.5f, .5f), Color.white, Ink, () => {
                        participating = !participating;
                        toggle.GetComponentInChildren<Text>().text = participating ? "Rank my runs: ON" : "Rank my runs: OFF";
                    });
                toggle.GetComponent<SoftShape>().shadow = false;
            }
            var note = Label(panel, error ?? "", 13, Muted, new Vector2(0, top - 332), new Vector2(296, 48));
            Button save = null;
            save = Button(panel, joining ? "Join leaderboards" : "Save changes", new Vector2(0, -top + 90), new Vector2(292, 48), new Vector2(.5f, .5f), Palette[1], Color.white, () => {
                string nickname = input.text.Trim();
                if (!ValidLeaderboardNickname(nickname)) { note.text = "Use 3–16 letters, numbers or underscores."; return; }
                save.interactable = false;
                save.GetComponentInChildren<Text>().text = "Saving…";
                input.DeactivateInputField();
                StartCoroutine(leaderboards.SetProfile(nickname, participating, updated => {
                    if (LeaderboardViewCurrent(generation, panel)) back();
                }, message => {
                    if (!LeaderboardViewCurrent(generation, panel)) return;
                    note.text = message; save.interactable = true;
                    save.GetComponentInChildren<Text>().text = joining ? "Join leaderboards" : "Save changes";
                }));
            });
            save.GetComponent<SoftShape>().shadow = false;
            Button(panel, joining ? "Cancel" : "Back", new Vector2(0, -top + 30), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                () => { input.DeactivateInputField(); back(); });
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

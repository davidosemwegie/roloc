using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Roloc.Core;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        DailyClient daily;
        DailyChallenge challenge;
        DailyAttempt dailyAttempt;
        DailyStanding standing;
        readonly List<DailyTraceEvent> dailyTrace = new List<DailyTraceEvent>();
        bool dailyRun, dailySubmitted, dailyBusy;
        double dailyStartedAt, dailyTickAt, transitionClock;
        Text dailyStandingLabel;
        Button shareButton;
        string rankingCaption = "";

        void InitializeDaily() { daily = new DailyClient(null, SaveDirectoryOverride); }
        void BuildDailyUI()
        {
            if (daily.IsConfigured) StartCoroutine(daily.RetryPending());
        }

        void ShowDaily()
        {
            if (dailyBusy) return;
            dailyBusy = true;
            var panel = NewOverlay("One board. Everyone.", "Finding today's Daily…", 380);
            Button(panel, "Back", new Vector2(0, -120), new Vector2(270, 48), new Vector2(.5f, .5f), Color.white, Ink,
                () => { dailyBusy = false; overlay.gameObject.SetActive(false); });
            StartCoroutine(daily.LoadCurrent(current => {
                if (!dailyBusy) return;
                dailyBusy = false; challenge = current;
                if (current == null) { DailyUnavailable("Today's challenge is being prepared."); return; }
                DailyEntry(true);
            }, error => {
                if (!dailyBusy) return;
                dailyBusy = false; challenge = daily.CachedChallenge;
                if (challenge != null) DailyEntry(false);
                else DailyUnavailable(daily.IsConfigured ? "Connect to the internet to load your first Daily." : "Daily is available in the invited test build.\nFlow and Rush are ready offline.");
            }));
        }

        void DailyUnavailable(string reason)
        {
            var panel = NewOverlay("Daily is resting", reason, 340);
            Button(panel, "Back to play", new Vector2(0, -98), new Vector2(270, 50), new Vector2(.5f, .5f), Palette[0], Color.white,
                () => overlay.gameObject.SetActive(false));
        }

        void DailyEntry(bool online)
        {
            if (!challenge.Supported)
            {
                DailyUnavailable("Update Ring Rush to play this Daily.\nYour regular modes are still ready."); return;
            }
            bool ranked = online && challenge.rankedEnabled && challenge.serverNow < challenge.closesAt;
            string deadline = DateTimeOffset.FromUnixTimeMilliseconds(challenge.uploadDeadline).UtcDateTime.ToString("MMM d, HH:mm 'UTC'");
            var panel = NewOverlay("Daily · " + challenge.date, challenge.variant.ToUpperInvariant() + " · Rush · Unlimited retries\nUpload by " + deadline, 460);
            Label(panel, ranked ? "Your best attempt counts.\nEveryone starts with the same sequence." : "Cached practice · no ranking\nYou still earn local progress.", 15, Ink,
                new Vector2(0, 10), new Vector2(294, 65));
            Button(panel, ranked ? "PLAY RANKED" : "PLAY PRACTICE", new Vector2(0, -76), new Vector2(270, 54), new Vector2(.5f, .5f), Palette[1], Color.white, () => {
                if (!Saves.Data.TutorialCompleted) { overlay.gameObject.SetActive(false); BeginTutorial(); return; }
                RequestGameStart(() => { if (ranked) StartDailyRanked(); else LaunchDaily(null); });
            });
            Button(panel, "Back", new Vector2(0, -145), new Vector2(270, 44), new Vector2(.5f, .5f), Color.clear, Ink,
                () => overlay.gameObject.SetActive(false));
        }

        void StartDailyRanked()
        {
            if (dailyBusy) return;
            dailyBusy = true;
            StartCoroutine(daily.StartRanked(challenge, attempt => {
                dailyBusy = false; LaunchDaily(attempt);
            }, error => { dailyBusy = false; DailyUnavailable(error); }));
        }

        void LaunchDaily(DailyAttempt attempt)
        {
            CancelAllTouches();
            dailyRun = selectedDaily = true; dailySubmitted = false; dailyAttempt = attempt;
            dailyTrace.Clear(); standing = null;
            rankingCaption = attempt == null ? "PRACTICE · local progress counts" : "RANKED DAILY · best attempt counts";
            Session = GameSession.CreateDaily((uint)challenge.seed, challenge.variant == "still" ? BoardStyle.Still : BoardStyle.Lively, challenge.rulesVersion);
            Session.StartGame(); StartLocalCredit(); gameRecorded = false;
            SetScreen(game); ResetBoard(); audioPlayer.StartMusic();
            dailyStartedAt = dailyTickAt = Time.realtimeSinceStartupAsDouble;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Hint(attempt == null ? "Practice · earn progress, without a ranking" : "Today's shared Daily · good luck!", 3);
        }

        bool ReplayDailyIfNeeded()
        {
            if (!selectedDaily || !dailyRun) return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (challenge != null && now < challenge.closesAt)
            {
                if (dailyAttempt == null) LaunchDaily(null); else StartDailyRanked();
            }
            else ShowDaily();
            return true;
        }

        void RecordDailyEvent(string kind, int color = -1, Vector2 point = default)
        {
            if (!dailyRun || dailyAttempt == null || dailySubmitted) return;
            dailyTrace.Add(new DailyTraceEvent {
                kind = kind, round = Session.Score,
                tMs = (int)Math.Ceiling((Time.realtimeSinceStartupAsDouble - dailyStartedAt) * 1000),
                elapsedMs = Session.RoundElapsedMilliseconds, color = color,
                xQ = Mathf.RoundToInt(point.x * 1000), yQ = Mathf.RoundToInt(point.y * 1000)
            });
        }

        void AbandonDailyIfNeeded()
        {
            if (!dailyRun || dailySubmitted || dailyAttempt == null || Session.State == RoundState.Menu || Session.State == RoundState.GameOver) return;
            RecordDailyEvent("abandon"); SubmitDailyIfNeeded();
        }

        void SubmitDailyIfNeeded()
        {
            if (!dailyRun || dailySubmitted) return;
            dailySubmitted = true;
            if (dailyAttempt == null) { SetRanking("PRACTICE · local progress saved"); return; }
            var attempt = dailyAttempt; var submittedChallenge = challenge;
            PlayerPrefs.SetString("ring-rush.last-daily", attempt.challengeId); PlayerPrefs.Save();
            SetRanking("Saving your Daily result…");
            StartCoroutine(daily.SubmitRun(attempt, dailyTrace, result => {
                bool currentResult = dailyRun && dailyAttempt != null && dailyAttempt.attemptId == attempt.attemptId;
                if (result.status == "accepted")
                    StartCoroutine(daily.GetStanding(attempt.challengeId, value => {
                        if (dailyRun && dailyAttempt != null && dailyAttempt.attemptId == attempt.attemptId) { standing = value; SetRanking(StandingText(value)); }
                    }, error => { if (currentResult) SetRanking("Score saved · reconnect for your standing"); }));
                else if (currentResult)
                    SetRanking(result.status == "validating" ? "Result saved · ranking is being checked" : result.status == "expired" ? "Upload window closed · local progress kept" : "Ranking unavailable · local progress kept");
            }, error => {
                if (dailyAttempt != null && dailyAttempt.attemptId == attempt.attemptId)
                    SetRanking("Upload queued · reconnect before " + DateTimeOffset.FromUnixTimeMilliseconds(submittedChallenge.uploadDeadline).UtcDateTime.ToString("HH:mm 'UTC'"));
            }));
        }

        string StandingText(DailyStanding value)
        {
            if (value.excluded) return "Result excluded from ranking · local progress kept";
            if (!value.hasBestScore) return "No ranked score yet";
            string cohort = value.waiting ? "Waiting for more players" : "Top " + Math.Max(.1, value.topPercent).ToString("0.#") + "% " + (value.provisional ? "so far" : "final");
            return cohort + " · " + value.participants + " players\nDaily best " + value.bestScore + (value.early && !value.waiting ? " · early results" : "");
        }
        void SetRanking(string caption) { rankingCaption = caption; if (dailyStandingLabel) dailyStandingLabel.text = caption; LayoutResults(); }

        void LoadFinalStandingOnReturn()
        {
            if (daily == null || !daily.IsConfigured || !menu.gameObject.activeSelf) return;
            StartCoroutine(daily.RetryPending());
            string last = PlayerPrefs.GetString("ring-rush.last-daily", "");
            if (string.IsNullOrEmpty(last) || PlayerPrefs.GetString("ring-rush.final-seen", "") == last) return;
            StartCoroutine(daily.GetStanding(last, value => {
                if (value.provisional || !menu.gameObject.activeSelf || overlay.gameObject.activeSelf) return;
                PlayerPrefs.SetString("ring-rush.final-seen", last); PlayerPrefs.Save();
                var panel = NewOverlay("Your Daily is final", value.date + "\n" + StandingText(value), 350);
                Button(panel, "Keep playing", new Vector2(0, -97), new Vector2(270, 52), new Vector2(.5f, .5f), Palette[1], Color.white,
                    () => overlay.gameObject.SetActive(false));
            }));
        }

        IEnumerator ShareDailyCard()
        {
            if (!dailyRun || !results.gameObject.activeSelf) yield break;
            yield return new WaitForEndOfFrame();
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(Application.temporaryCachePath, "Ring-Rush-Daily.png");
            try { File.WriteAllBytes(path, capture.EncodeToPNG()); }
            finally { Destroy(capture); }
            NativeServices.Share("Ring Rush Daily · " + challenge.date + "\n" + Session.Score + " matches\n" + rankingCaption, path);
        }
    }
}

using System;
using System.Collections;
using Roloc.Core;
using Roloc.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Roloc.Presentation
{
    public sealed partial class RolocGame
    {
        [NonSerialized] public IAdService AdServiceOverride;
        [NonSerialized] public Func<double> AdRandomOverride;
        IAdService ads;
        AdsConfiguration adsConfiguration;
        readonly System.Random adRandom = new System.Random();
        bool interstitialPending, fullScreenAdShowing, startingAfterAd, privacyBusy;
        bool applicationFocused = true, applicationPaused, reviveCountingDown;
        int reviveGeneration;
        int appliedTrackingStatus = -1;
        Text reviveCountdownLabel, reviveBankLabel;
        Button reviveWatchButton;
        RewardPlayback rewardPlayback;

        sealed class RewardPlayback
        {
            public int Generation;
            public GameSession Session;
            public bool Rewarded, Closed;
        }

        void InitializeAdvertising()
        {
            adsConfiguration = Resources.Load<AdsConfiguration>("AdsConfiguration");
            ads = AdServiceOverride ?? new LevelPlayAdService(adsConfiguration);
            reviveBankLabel = Label(game, "", 10, Muted, new Vector2(-120, -206), new Vector2(115, 24), new Vector2(.5f, 1));
            reviveBankLabel.name = "Revive bank";
            reviveCountdownLabel = Label(game, "", 64, Ink, new Vector2(0, 24), new Vector2(240, 100));
            reviveCountdownLabel.name = "Revive countdown";
            reviveCountdownLabel.fontStyle = FontStyle.Bold;
            reviveCountdownLabel.gameObject.SetActive(false);
            if (ads.IsConfigured && Saves.Data.AdPrivacyChoiceMade && AdServiceOverride == null)
                StartCoroutine(ApplyAdPrivacy(null));
        }

        void ReserveBannerSpace()
        {
            // Native ad dimensions are pixels; CanvasScaler units differ on every iPhone.
            float pixelsToCanvas = safe.rect.height / Mathf.Max(1, Screen.safeArea.height);
            float reserve = ads != null && ads.IsConfigured && !Session.WasTutorial
                ? ads.BannerHeightPixels * pixelsToCanvas + 12 : 0;
            game.offsetMin = new Vector2(0, reserve);
        }

        void RefreshAdVisibility()
        {
            if (ads == null || !game || !overlay || Session == null) return;
            bool visible = game.gameObject.activeSelf && !Session.WasTutorial
                && !overlay.gameObject.activeSelf && !fullScreenAdShowing && !startingAfterAd
                && !applicationPaused && applicationFocused
                && (Session.State == RoundState.Playing || Session.State == RoundState.Transition);
            ads.SetBannerVisible(visible);
        }

        void UpdateAdvertising()
        {
            if (AdServiceOverride == null && ads != null && ads.IsInitialized && !privacyBusy && !fullScreenAdShowing
                && applicationFocused && !applicationPaused && Saves.Data.AdPrivacyChoiceMade
                && NativeServices.TrackingAuthorizationStatus != appliedTrackingStatus)
                StartCoroutine(ApplyAdPrivacy(null));
            if (rewardPlayback != null && rewardPlayback.Rewarded && rewardPlayback.Closed
                && applicationFocused && !applicationPaused) CompleteRewardedRevive();
            if (reviveCountdownLabel)
            {
                bool counting = reviveCountingDown && Session.State == RoundState.Transition;
                reviveCountdownLabel.gameObject.SetActive(counting);
                if (counting) reviveCountdownLabel.text = Mathf.Max(1, Mathf.CeilToInt(transitionLeft)).ToString();
            }
            if (reviveBankLabel)
                reviveBankLabel.text = Session.WasTutorial || !Session.RevivesEnabled ? "" : "REVIVES " + Session.RevivesAvailable + " / 3";
            if (reviveWatchButton && Session.State == RoundState.AwaitingRevive && !fullScreenAdShowing)
                reviveWatchButton.interactable = ads != null && ads.IsRewardedReady;
            if (game.gameObject.activeSelf) ReserveBannerSpace();
            RefreshAdVisibility();
        }

        void HandleRunFailure()
        {
            CancelAllTouches();
            ClearBoardEffects();
            foreach (var puck in pucks) puck.MotionPaused = true;
            if (Session.State != RoundState.AwaitingRevive || ads == null || !ads.IsRewardedReady)
            {
                FinishRun();
                return;
            }
            audioPlayer.PauseMusic();
            ShowReviveOffer();
        }

        void ShowReviveOffer(string message = null)
        {
            var panel = NewOverlay("Continue your streak?", message ?? "Watch an ad to keep your score and combo.\nYour Perfect streak starts fresh.", 440);
            Label(panel, Session.Score + " MATCHES   ·   COMBO " + Session.ComboBeforeFailure,
                20, Ink, new Vector2(0, 27), new Vector2(306, 42)).fontStyle = FontStyle.Bold;
            Label(panel, Session.RevivesAvailable + " / 3 revives available", 14, Muted,
                new Vector2(0, -12), new Vector2(290, 30));
            var watch = Button(panel, "Watch ad & continue", new Vector2(0, -81), new Vector2(290, 56),
                new Vector2(.5f, .5f), Palette[1], Color.white, WatchReviveAd);
            reviveWatchButton = watch;
            watch.interactable = ads != null && ads.IsRewardedReady;
            Button(panel, "End game", new Vector2(0, -151), new Vector2(270, 44),
                new Vector2(.5f, .5f), Color.clear, Muted, FinishRun);
        }

        void WatchReviveAd()
        {
            if (fullScreenAdShowing || Session.State != RoundState.AwaitingRevive) return;
            if (ads == null || !ads.IsRewardedReady) { FinishRun(); return; }
            var playback = new RewardPlayback { Generation = ++reviveGeneration, Session = Session };
            rewardPlayback = playback;
            fullScreenAdShowing = true;
            ads.SetBannerVisible(false);
            // The SDK owns the full-screen UI; keep gameplay behind a blocking scrim.
            NewOverlay("Your streak is waiting", "Finish the ad to continue this game.", 270);
            ads.ShowRewarded(value => {
                if (!this || rewardPlayback != playback || playback.Generation != reviveGeneration
                    || Session != playback.Session || Session.State != RoundState.AwaitingRevive || gameRecorded) return;
                if (value == RewardedAdEvent.Rewarded) playback.Rewarded = true;
                else if (value == RewardedAdEvent.Closed)
                {
                    playback.Closed = true;
                    fullScreenAdShowing = false;
                    if (!playback.Rewarded) ShowReviveOffer("The ad has not confirmed a reward.\nYou can try again or end this game.");
                }
                else
                {
                    fullScreenAdShowing = false;
                    FinishRun();
                }
            });
        }

        void CompleteRewardedRevive()
        {
            if (!Session.ApplyRewardedRevive()) { InvalidateRevive(); return; }
            // The event starts a fixed 3-second transition, matching the server replay.
            RecordDailyEvent("revive");
            InvalidateRevive();
            reviveCountingDown = true;
            overlay.gameObject.SetActive(false);
            foreach (var puck in pucks) { puck.MotionPaused = false; puck.SnapHome(); }
            audioPlayer.ResumeMusic();
            BeginBoardTransition(false, false, false);
            Hint("Streak continued · make it count", 3);
        }

        void InvalidateRevive()
        {
            reviveGeneration++;
            rewardPlayback = null;
            fullScreenAdShowing = false;
            reviveCountingDown = false;
            if (reviveCountdownLabel) reviveCountdownLabel.gameObject.SetActive(false);
        }

        void RequestGameStart(Action start)
        {
            if (startingAfterAd || fullScreenAdShowing || privacyBusy || dailyBusy) return;
            if (ads != null && ads.IsConfigured && !Saves.Data.AdPrivacyChoiceMade
                && Saves.Data.TutorialCompleted && AdServiceOverride == null && HasPrivacyPolicy())
            {
                ShowAdPrivacy(() => RequestGameStart(start));
                return;
            }
            bool considerAd = interstitialPending && Saves.Data.TutorialCompleted;
            interstitialPending = false;
            if (!considerAd || (AdRandomOverride?.Invoke() ?? adRandom.NextDouble()) >= .25
                || ads == null || !ads.IsInterstitialReady)
            {
                start();
                return;
            }
            startingAfterAd = fullScreenAdShowing = true;
            ads.SetBannerVisible(false);
            bool completed = false;
            ads.ShowInterstitial(() => {
                if (!this || completed) return;
                completed = true;
                startingAfterAd = fullScreenAdShowing = false;
                start();
            });
        }

        bool HasPrivacyPolicy() => adsConfiguration && Uri.TryCreate(adsConfiguration.PrivacyPolicyUrl,
            UriKind.Absolute, out var uri) && uri.Scheme == "https";

        void ShowAdPrivacy(Action done)
        {
            if (privacyBusy) return;
            if (!HasPrivacyPolicy())
            {
                var unavailable = NewOverlay("Ad privacy", "Advertising is not configured in this build.\nYour game is still ready to play.", 330);
                Button(unavailable, "Done", new Vector2(0, -90), new Vector2(270, 49), new Vector2(.5f, .5f),
                    Palette[0], Color.white, () => { overlay.gameObject.SetActive(false); done?.Invoke(); });
                return;
            }
            var panel = NewOverlay("Your ad privacy", "Unity Ads helps support Ring Rush.\nChoose how your ad data is used.", 520);
            Label(panel, "Allow use and sharing of device and ad activity for personalized ads and measurement, or choose limited ads. Either choice lets you play and watch ads for revives.",
                14, Ink, new Vector2(0, 18), new Vector2(294, 106));
            Button(panel, "Privacy policy", new Vector2(0, -55), new Vector2(270, 40), new Vector2(.5f, .5f), Color.clear, Palette[1],
                () => Application.OpenURL(adsConfiguration.PrivacyPolicyUrl));
            Button(panel, "Allow personalized ads", new Vector2(0, -113), new Vector2(290, 50), new Vector2(.5f, .5f), Palette[1], Color.white,
                () => SaveAdPrivacy(true, done));
            Button(panel, "Use limited ads", new Vector2(0, -177), new Vector2(290, 50), new Vector2(.5f, .5f), Color.white, Ink,
                () => SaveAdPrivacy(false, done));
            Label(panel, "You can change this in Ad privacy settings.", 11, Muted, new Vector2(0, -225), new Vector2(300, 24));
        }

        void SaveAdPrivacy(bool personalized, Action done)
        {
            if (privacyBusy) return;
            Saves.Data.AdPrivacyChoiceMade = true;
            Saves.Data.PersonalizedAdsAllowed = personalized;
            Saves.Save();
            overlay.gameObject.SetActive(false);
            StartCoroutine(ApplyAdPrivacy(done));
        }

        IEnumerator ApplyAdPrivacy(Action done)
        {
            privacyBusy = true;
            if (Saves.Data.PersonalizedAdsAllowed && NativeServices.TrackingAuthorizationStatus == 0)
            {
                while (applicationPaused || !applicationFocused) yield return null;
                // Let our consent panel disappear before iOS presents its own permission sheet.
                yield return null;
                bool answered = false;
                NativeServices.RequestTrackingAuthorization(_ => answered = true);
                while (!answered) yield return null;
            }
            appliedTrackingStatus = NativeServices.TrackingAuthorizationStatus;
            bool personalized = Saves.Data.PersonalizedAdsAllowed && appliedTrackingStatus == 3;
            ads.Initialize(personalized, _ => { });
            privacyBusy = false;
            done?.Invoke();
        }
    }
}

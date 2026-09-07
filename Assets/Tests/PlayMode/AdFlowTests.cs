using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using Roloc.Services;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Roloc.Tests
{
    public sealed class AdFlowTests
    {
        sealed class ScriptedAds : IAdService
        {
            public bool IsConfigured => true;
            public bool IsInitialized => true;
            public bool IsRewardedReady { get; set; } = true;
            public bool IsInterstitialReady { get; set; } = true;
            public int BannerHeightPixels => 50;
            public bool BannerVisible;
            public readonly List<Action<RewardedAdEvent>> Rewards = new List<Action<RewardedAdEvent>>();
            public readonly List<Action> Interstitials = new List<Action>();
            public void Initialize(bool personalized, Action<bool> completed) => completed?.Invoke(true);
            public void SetBannerVisible(bool visible) => BannerVisible = visible;
            public void ShowRewarded(Action<RewardedAdEvent> callback) => Rewards.Add(callback);
            public void ShowInterstitial(Action completed) => Interstitials.Add(completed);
            public void Dispose() { }
        }

        GameObject root;
        RolocGame game;
        ScriptedAds ads;
        string directory;
        int randomCalls;
        double randomValue;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "roloc-ads-" + Guid.NewGuid().ToString("N"));
            ads = new ScriptedAds();
            root = new GameObject("Ad flow test");
            root.SetActive(false);
            root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>();
            game.SaveDirectoryOverride = directory;
            game.AdServiceOverride = ads;
            randomCalls = 0;
            randomValue = 0;
            game.AdRandomOverride = () => { randomCalls++; return randomValue; };
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            game.difficulty.RandomFlowEnabled = false;
            game.difficulty.TransitionSeconds = 0;
            game.difficulty.FlowTransitionSeconds = 0;
            game.difficulty.RotationSeconds = 0;
            root.SetActive(true);
            game.Saves.Data.SelectedMode = "Rush";
            game.Saves.Data.TutorialCompleted = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty);
            UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        // Core scoring keeps these integration tests fast. The production failure handler owns
        // offers, saved results, ad callbacks, and the next-game decision under test.
        void FailAt(int score)
        {
            while (game.Session.Score < score)
            {
                Assert.That(game.Session.Drop(game.Session.ActiveColor, true, true), Is.EqualTo(MatchResult.Matched));
                game.Session.CompleteTransition();
            }
            Assert.That(game.Session.Drop(game.Session.ActiveColor, false), Is.EqualTo(MatchResult.Failed));
            Invoke("HandleRunFailure");
        }

        void Invoke(string method, params object[] args)
        {
            var target = typeof(RolocGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(target, Is.Not.Null, method);
            target.Invoke(game, args);
        }

        T Field<T>(string name)
            => (T)typeof(RolocGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        void Click(string title)
        {
            var button = root.GetComponentsInChildren<Button>()
                .Last(b => b.GetComponentInChildren<Text>()?.text == title);
            Assert.That(button.interactable, Is.True, title);
            button.onClick.Invoke();
        }

        bool HasText(string text) => root.GetComponentsInChildren<Text>().Any(label => label.text == text);

        [UnityTest]
        public IEnumerator ReviveOfferFitsCompactAndTallSafeAreas()
        {
            var camera = new GameObject("Ad layout camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            var canvas = root.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (var size in new[] { new Vector2Int(750, 1334), new Vector2Int(1320, 2868) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                target.Create(); camera.targetTexture = target;
                game.ShowMenu(); game.BeginRun(); FailAt(20);
                foreach (var area in root.GetComponentsInChildren<SafeArea>(true))
                {
                    area.enabled = false;
                    var rect = (RectTransform)area.transform;
                    rect.anchorMin = new Vector2(0, .04f);
                    rect.anchorMax = new Vector2(1, .94f);
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var button in root.GetComponentsInChildren<Button>().Where(b =>
                    b.GetComponentInChildren<Text>()?.text == "Watch ad & continue" || b.GetComponentInChildren<Text>()?.text == "End game"))
                {
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.x, Is.InRange(0f, 1f));
                        Assert.That(point.y, Is.InRange(.04f, .94f));
                    }
                }
                string output = Environment.GetEnvironmentVariable("RING_RUSH_AD_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output); camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, "revive-" + size.x + ".png"), texture.EncodeToPNG());
                    RenderTexture.active = previous; UnityEngine.Object.Destroy(texture);
                }
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator EligibleFailureWaitsWithoutRecordingOrSpendingAndEndRecordsOnce()
        {
            game.BeginRun();
            FailAt(20);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(HasText("Continue your streak?"), Is.True);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(1));
            float timer = game.Session.RemainingSeconds;
            var pucks = root.GetComponentsInChildren<PuckView>(true);
            var positions = pucks.Select(p => p.Rect.anchoredPosition).ToArray();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(timer));
            CollectionAssert.AreEqual(positions, pucks.Select(p => p.Rect.anchoredPosition).ToArray());
            Assert.That(ads.BannerVisible, Is.False);
            Click("End game");
            Invoke("FinishRun");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(1));
            game.BeginRun();
            Assert.That(randomCalls, Is.Zero, "A declined earned revive does not qualify for an interstitial.");
        }

        [UnityTest]
        public IEnumerator RewardBeforeCloseWaitsThenRevivesExactlyOnce()
        {
            game.BeginRun(); FailAt(20); Click("Watch ad & continue");
            Assert.That(ads.Rewards.Count, Is.EqualTo(1));
            var callback = ads.Rewards[0];
            callback(RewardedAdEvent.Rewarded);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(1));
            callback(RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(game.Session.RevivesAvailable, Is.Zero);
            Assert.That(game.Session.Combo, Is.EqualTo(20));
            Assert.That(game.Session.PerfectStreak, Is.Zero);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
            callback(RewardedAdEvent.Rewarded); callback(RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.RevivesAvailable, Is.Zero);
            Assert.That(Field<float>("transitionLeft"), Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseBeforeRewardReturnsOfferThenAcceptsLateReward()
        {
            game.BeginRun(); FailAt(20); Click("Watch ad & continue");
            ads.Rewards[0](RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(HasText("Continue your streak?"), Is.True);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
            ads.Rewards[0](RewardedAdEvent.Rewarded);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(game.Session.RevivesAvailable, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetryAndEndInvalidateEarlierAdCallbacksAndCannotRestartNewRun()
        {
            game.BeginRun(); FailAt(50); Click("Watch ad & continue");
            var first = ads.Rewards[0];
            first(RewardedAdEvent.Closed);
            yield return null; // Retire the previous modal's UI before selecting retry.
            Click("Watch ad & continue");
            first(RewardedAdEvent.Rewarded);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.AwaitingRevive));
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(2));
            var second = ads.Rewards[1];
            second(RewardedAdEvent.Closed);
            yield return null;
            Click("End game");
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
            game.BeginRun();
            var next = game.Session;
            first(RewardedAdEvent.Rewarded); second(RewardedAdEvent.Rewarded); second(RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session, Is.SameAs(next));
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(game.Session.Score, Is.Zero);
            Assert.That(game.Session.RevivesAvailable, Is.Zero);
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingRewardInventoryOrDisplayFailureEndsRunWithoutSpending()
        {
            game.BeginRun(); ads.IsRewardedReady = false; FailAt(20);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
            Assert.That(ads.Rewards, Is.Empty);
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(1));
            ads.IsRewardedReady = true;
            game.BeginRun(); FailAt(20); Click("Watch ad & continue");
            ads.Rewards[0](RewardedAdEvent.Failed);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Session.RevivesAvailable, Is.EqualTo(1));
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(2));
            ads.Rewards[0](RewardedAdEvent.Rewarded); ads.Rewards[0](RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RewardWaitsForForegroundAndCountdownPausesWithFullRoundTimer()
        {
            game.BeginRun(); FailAt(20); Click("Watch ad & continue");
            Invoke("OnApplicationPause", true);
            ads.Rewards[0](RewardedAdEvent.Rewarded); ads.Rewards[0](RewardedAdEvent.Closed);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.AwaitingRevive));
            Invoke("OnApplicationPause", false);
            Invoke("UpdateAdvertising");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(Field<float>("transitionLeft"), Is.EqualTo(3));
            Assert.That(game.Session.RoundElapsedMilliseconds, Is.Zero);
            game.PauseRun();
            float countdown = Field<float>("transitionLeft");
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Paused));
            Assert.That(Field<float>("transitionLeft"), Is.EqualTo(countdown));
            Assert.That(game.Session.RoundElapsedMilliseconds, Is.Zero);
            game.ResumeRun();
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(game.Session.RemainingSeconds, Is.GreaterThan(game.Session.DurationSeconds - .5f));
        }

        [UnityTest]
        public IEnumerator EmptyBankRollsOnceBeforeNextGameThroughMenuAndDuplicateCloseIsIgnored()
        {
            randomValue = .249999;
            game.BeginRun(); FailAt(0);
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
            Assert.That(ads.Interstitials, Is.Empty);
            Assert.That(randomCalls, Is.Zero);
            game.ShowMenu();
            Assert.That(randomCalls, Is.Zero);
            game.BeginRun(); game.BeginRun();
            Assert.That(randomCalls, Is.EqualTo(1));
            Assert.That(ads.Interstitials.Count, Is.EqualTo(1));
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Menu));
            Assert.That(ads.BannerVisible, Is.False);
            ads.Interstitials[0]();
            var next = game.Session;
            ads.Interstitials[0]();
            Assert.That(game.Session, Is.SameAs(next));
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(randomCalls, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InterstitialCompletionWaitsForBothForegroundSignalsBeforeStartingOnce()
        {
            game.BeginRun();
            // Close and display failure share IAdService's completion callback. Exercise both
            // orders of iOS focus/unpause delivery after that callback has arrived in background.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                FailAt(0); game.ShowMenu(); game.BeginRun();
                Assert.That(ads.Interstitials.Count, Is.EqualTo(attempt + 1));
                var waitingSession = game.Session;
                Invoke("OnApplicationFocus", false);
                Invoke("OnApplicationPause", true);
                ads.Interstitials[attempt]();
                ads.Interstitials[attempt]();
                Invoke("UpdateAdvertising");
                game.BeginRun();
                yield return new WaitForSecondsRealtime(.06f);
                Assert.That(game.Session, Is.SameAs(waitingSession));
                Assert.That(game.Session.State, Is.EqualTo(RoundState.Menu));
                Assert.That(ads.BannerVisible, Is.False);
                Assert.That(randomCalls, Is.EqualTo(attempt + 1));

                if (attempt == 0) Invoke("OnApplicationPause", false);
                else Invoke("OnApplicationFocus", true);
                Invoke("UpdateAdvertising");
                Assert.That(game.Session, Is.SameAs(waitingSession), "Both foreground signals are required.");
                Assert.That(game.Session.State, Is.EqualTo(RoundState.Menu));

                if (attempt == 0) Invoke("OnApplicationFocus", true);
                else Invoke("OnApplicationPause", false);
                Invoke("UpdateAdvertising");
                var next = game.Session;
                Assert.That(next, Is.Not.SameAs(waitingSession));
                Assert.That(next.State, Is.EqualTo(RoundState.Playing));
                Assert.That(next.RoundElapsedMilliseconds, Is.Zero);
                Assert.That(next.RemainingSeconds, Is.EqualTo(next.DurationSeconds));
                ads.Interstitials[attempt]();
                Invoke("UpdateAdvertising");
                Assert.That(game.Session, Is.SameAs(next));
                Assert.That(randomCalls, Is.EqualTo(attempt + 1));
                Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(attempt + 1));
            }
        }

        [UnityTest]
        public IEnumerator ProbabilityBoundaryAndUnavailableInventoryStartImmediatelyWithoutReroll()
        {
            randomValue = .25;
            game.BeginRun(); FailAt(0); game.BeginRun();
            Assert.That(randomCalls, Is.EqualTo(1));
            Assert.That(ads.Interstitials, Is.Empty);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            randomValue = 0; ads.IsInterstitialReady = false;
            FailAt(0); game.BeginRun();
            Assert.That(randomCalls, Is.EqualTo(2));
            Assert.That(ads.Interstitials, Is.Empty);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            ads.IsInterstitialReady = true;
            game.ShowMenu(); game.BeginRun();
            Assert.That(randomCalls, Is.EqualTo(2), "Voluntary abandonment does not schedule an ad.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BannerSpaceStaysReservedWhileOverlaysHideAdAndGameplayDoesNotOverlapIt()
        {
            Assert.That(ads.BannerVisible, Is.False);
            game.BeginRun();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(ads.BannerVisible, Is.True);
            var content = Field<RectTransform>("game");
            float reserve = content.offsetMin.y;
            Assert.That(reserve, Is.GreaterThan(0));
            var corners = new Vector3[4];
            content.GetWorldCorners(corners);
            float contentBottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
            Assert.That(contentBottom, Is.GreaterThanOrEqualTo(Screen.safeArea.yMin + ads.BannerHeightPixels - 1));
            foreach (var puck in root.GetComponentsInChildren<PuckView>())
            {
                puck.Rect.GetWorldCorners(corners);
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y,
                    Is.GreaterThanOrEqualTo(contentBottom), puck.name);
            }
            game.PauseRun();
            Assert.That(ads.BannerVisible, Is.False);
            Assert.That(content.offsetMin.y, Is.EqualTo(reserve));
            game.ResumeRun();
            Assert.That(ads.BannerVisible, Is.True);
            FailAt(20);
            Assert.That(ads.BannerVisible, Is.False);
            Assert.That(content.offsetMin.y, Is.EqualTo(reserve));
            Click("Watch ad & continue");
            Assert.That(ads.BannerVisible, Is.False);
            ads.Rewards[0](RewardedAdEvent.Failed);
            Assert.That(ads.BannerVisible, Is.False);
            game.BeginTutorial();
            yield return null;
            Assert.That(ads.BannerVisible, Is.False);
            Assert.That(content.offsetMin.y, Is.Zero);
        }
    }
}

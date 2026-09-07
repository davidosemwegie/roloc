using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using Roloc.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public sealed class ExperienceIntegrationTests
    {
        GameObject root;
        RolocGame game;
        string directory;
        [UnitySetUp] public IEnumerator Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-experience-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Experience test"); root.SetActive(false); root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>(); game.SaveDirectoryOverride = directory;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            root.SetActive(true); game.Saves.Data.TutorialCompleted = true;
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty); UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        void Invoke(string method, params object[] args) => typeof(RolocGame).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, args);
        T Get<T>(string field) => (T)typeof(RolocGame).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
        void Drop(bool match)
        {
            Canvas.ForceUpdateCanvases();
            var puck = root.GetComponentsInChildren<PuckView>().Single(p => p.ColorIndex == game.Session.ActiveColor);
            var ring = root.GetComponentsInChildren<SoftShape>().Single(s => s.name == "Ring " + puck.ColorIndex);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position) };
            puck.OnPointerDown(pointer);
            if (match) pointer.position = RectTransformUtility.WorldToScreenPoint(null, ring.rectTransform.position);
            puck.OnDrag(pointer); puck.OnPointerUp(pointer);
        }
        IEnumerator Capture(string name, int width = 400, int height = 860, bool save = true)
        {
            // A render target also works in headless batch tests, where no Game View exists.
            var canvas = root.GetComponentInChildren<Canvas>();
            var cameraObject = new GameObject("Capture camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var safe = Get<RectTransform>("safe");
            safe.GetComponent<SafeArea>().enabled = false;
            safe.anchorMin = new Vector2(0, 34f / height); safe.anchorMax = new Vector2(1, 1 - 62f / height);
            yield return null;
            Canvas.ForceUpdateCanvases(); Invoke("LayoutResults"); camera.Render();
            if (Get<RectTransform>("results").gameObject.activeSelf) AssertResultRowsFit();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (save)
            {
                Directory.CreateDirectory("TestResults/screens");
                File.WriteAllBytes("TestResults/screens/" + name + ".png", image.EncodeToPNG());
            }
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }

        void AssertResultRowsFit()
        {
            var panel = Get<RectTransform>("results");
            float bottom = 0;
            foreach (RectTransform row in panel)
            {
                if (!row.gameObject.activeSelf) continue;
                float top = -row.anchoredPosition.y - row.rect.height * .5f;
                Assert.That(top, Is.GreaterThanOrEqualTo(bottom + 4), row.name + " overlaps the previous row");
                bottom = top + row.rect.height;
                Assert.That(bottom, Is.LessThanOrEqualTo(panel.rect.height - 4), row.name + " leaves the safe area");
                var text = row.GetComponent<UnityEngine.UI.Text>();
                if (text) Assert.That(row.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight), "Wrapped text must fit its row");
            }
        }

        void FinishRecordRun(int points = 240, int matches = 24, int perfects = 3)
        {
            game.ShowMenu();
            game.Saves.Data.ProgressPoints = points;
            game.BeginRun();
            for (int i = 0; i < matches; i++)
            {
                game.Session.Drop(game.Session.ActiveColor, true, i >= matches - perfects);
                Invoke("CreditMatch"); game.Session.CompleteTransition();
            }
            while (game.Session.State != RoundState.GameOver)
            {
                game.Session.Drop(game.Session.ActiveColor, false);
                if (game.Session.State == RoundState.Transition) game.Session.CompleteTransition();
            }
            Invoke("FinishRun");
        }

        [UnityTest]
        public IEnumerator ResultsFitShortAndTallSafeAreasWithLongRecordsAndDailyRanking()
        {
            FinishRecordRun();
            yield return Capture("results-compact", 375, 667, false);
            yield return Capture("results-iphone", 440, 956, false);
            typeof(RolocGame).GetField("dailyRun", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, true);
            Invoke("ShowResultExperience");
            Invoke("SetRanking", "Top 12.5% so far · 12345 players\nDaily best 240 · early results");
            yield return Capture("results-daily-compact", 375, 667, false);
            game.Saves.Data.SelectedMode = "Rush";
            game.Saves.Data.TotalScore = 392;
            game.Saves.GetRecord("Rush", "Lively").HighScore = 99;
            FinishRecordRun(520, 45, 10);
            Assert.That(Get<UnityEngine.UI.Text[]>("resultStatValues")[0].text, Is.EqualTo("99"));
            Assert.That(Get<UnityEngine.UI.Text[]>("resultStatValues")[1].text, Is.EqualTo("45"));
            Assert.That(Get<UnityEngine.UI.Text[]>("resultStatValues")[2].text, Is.EqualTo("10"));
            Assert.That(Get<UnityEngine.UI.Text>("resultRewardTitle").text, Is.EqualTo("Unlocked Orbit!"));
            yield return Capture("results-unlocked-compact", 375, 667, false);
            game.Saves.Data.ReduceEffects = true;
            FinishRecordRun(0, 0, 0);
            yield return Capture("results-zero-compact", 375, 667, false);
        }

        [UnityTest, Explicit("Captures actual Unity UI for visual review.")]
        public IEnumerator CapturePlayerScreens()
        {
            yield return Capture("menu");
            game.Saves.Data.ProgressPoints = 600; game.Saves.Data.SymbolsEnabled = true;
            Invoke("ShowCollection"); yield return Capture("collection");
            game.BeginRun(); yield return null; yield return Capture("flow");
            game.PauseRun(); yield return Capture("pause");
            game.ResumeRun(); FinishRecordRun();
            yield return Capture("results", 440, 956);
            yield return Capture("results-compact", 375, 667);
            typeof(RolocGame).GetField("dailyRun", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, true);
            Invoke("ShowResultExperience");
            Invoke("SetRanking", "Top 12.5% so far · 12345 players\nDaily best 240 · early results");
            yield return Capture("results-daily-compact", 375, 667);
            game.Saves.Data.SelectedMode = "Rush";
            game.Saves.Data.TotalScore = 392;
            game.Saves.GetRecord("Rush", "Lively").HighScore = 99;
            FinishRecordRun(520, 45, 10);
            yield return Capture("results-unlocked", 440, 956);
            yield return Capture("results-unlocked-compact", 375, 667);
        }

        [UnityTest, Explicit("Development-only end-to-end ranked game with real rendered pointer drops.")]
        public IEnumerator DailyRenderedPlaythroughUploadsValidatedScore()
        {
            var config = Resources.Load<DailyConnection>("DailyConnection");
            if (!config || config.url.TrimEnd('/') != "https://determined-aardvark-934.convex.cloud") Assert.Ignore("Requires Clearjar development.");
            var client = Get<DailyClient>("daily");
            DailyChallenge challenge = null; DailyAttempt attempt = null; string error = null;
            yield return client.LoadCurrent(value => challenge = value, value => error = value);
            Assert.That(error, Is.Null); Assert.That(challenge, Is.Not.Null);
            yield return client.StartRanked(challenge, value => attempt = value, value => error = value);
            Assert.That(error, Is.Null); Assert.That(attempt?.status, Is.EqualTo("open"));
            typeof(RolocGame).GetField("challenge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, challenge);
            Invoke("LaunchDaily", attempt);
            for (int score = 0; score < 45; score++)
            {
                while (game.Session.State == RoundState.Transition) yield return null;
                Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
                yield return new WaitForSecondsRealtime(.035f);
                Drop(true); Assert.That(game.Session.Score, Is.EqualTo(score + 1));
                if (score == 19)
                {
                    game.PauseRun(); yield return new WaitForSecondsRealtime(.12f);
                    Assert.That(game.Session.State, Is.EqualTo(RoundState.Paused)); game.ResumeRun();
                }
            }
            while (game.Session.State == RoundState.Transition) yield return null;
            Drop(false); Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            double deadline = Time.realtimeSinceStartupAsDouble + 50;
            while (client.PendingCount > 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            DailyAttempt result = null;
            yield return client.PollAttempt(attempt.attemptId, value => result = value, value => error = value);
            Assert.That(error, Is.Null);
            Assert.That(result?.status, Is.EqualTo("accepted"), result?.reason);
            Assert.That(result.score, Is.EqualTo(45));
            Assert.That(game.Saves.Data.ProgressPoints, Is.GreaterThanOrEqualTo(90));
        }
    }
}

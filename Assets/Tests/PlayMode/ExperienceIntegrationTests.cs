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
        IEnumerator Capture(string name)
        {
            // A render target also works in headless batch tests, where no Game View exists.
            var canvas = root.GetComponentInChildren<Canvas>();
            var cameraObject = new GameObject("Capture camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(400, 860, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            yield return null;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(400, 860, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 400, 860), 0, 0); image.Apply();
            Directory.CreateDirectory("TestResults/screens");
            File.WriteAllBytes("TestResults/screens/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }

        [UnityTest, Explicit("Captures actual Unity UI for visual review.")]
        public IEnumerator CapturePlayerScreens()
        {
            yield return Capture("menu");
            game.Saves.Data.ProgressPoints = 600; game.Saves.Data.SymbolsEnabled = true;
            Invoke("ShowCollection"); yield return Capture("collection");
            game.BeginRun(); yield return null; yield return Capture("flow");
            game.PauseRun(); yield return Capture("pause");
            game.ResumeRun();
            while (game.Session.State != RoundState.GameOver) { game.Session.Tick(10); yield return null; }
            Invoke("FinishRun"); yield return Capture("results");
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

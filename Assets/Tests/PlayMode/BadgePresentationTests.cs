using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using Roloc.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public sealed class BadgePresentationTests
    {
        GameObject root;
        RolocGame game;
        string directory;
        void Invoke(string method, params object[] args) => typeof(RolocGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
        T Field<T>(string name) => (T)typeof(RolocGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        [UnitySetUp] public IEnumerator Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-badge-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Badges test"); root.SetActive(false); root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>(); game.SaveDirectoryOverride = directory;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>(); root.SetActive(true);
            game.Saves.Data.TutorialCompleted = true;
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(game.difficulty); UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [UnityTest] public IEnumerator BadgesRenderInResultsAndCollection()
        {
            game.Saves.Data.TotalScore = 80; game.BeginRun();
            for (int i = 0; i < 20; i++)
            { game.Session.Drop(game.Session.ActiveColor, true); Invoke("CreditMatch"); game.Session.CompleteTransition(); }
            while (game.Session.State == RoundState.Playing || game.Session.State == RoundState.Transition)
            { game.Session.Drop(game.Session.ActiveColor, false); game.Session.CompleteTransition(); }
            Invoke("FinishRun");
            Assert.That(Field<RectTransform>("resultBadgeRow").gameObject.activeSelf, Is.True);
            Assert.That(Field<RectTransform>("resultBadgeContent").childCount, Is.EqualTo(2));
            yield return Capture("badges-results", 440, 956);
            yield return Capture("badges-results-compact", 375, 667);
            // Preview fixtures: show the finished art across each full collection.
            game.Saves.Data.TotalScore = 100000; game.Saves.Data.HighScore = 1000;
            foreach (var badge in BadgeCatalog.All)
                if (!game.Saves.Data.UnlockedBadges.Contains(badge.Id)) game.Saves.Data.UnlockedBadges.Add(badge.Id);
            yield return Capture("badges-run", 440, 956, () => Invoke("ShowBadges", BadgeTrack.Run));
            yield return Capture("badges-lifetime", 440, 956, () => Invoke("ShowBadges", BadgeTrack.Lifetime));
            yield return Capture("badge-100k", 440, 956, () => Invoke("ShowBadgeDetail", BadgeCatalog.Find("lifetime-100000")));
        }
        [UnityTest] public IEnumerator PerfectRipplePausesAndRespectsReducedEffects()
        {
            game.BeginRun();
            Invoke("StartPerfectFeedback", Vector2.zero, Color.magenta);
            yield return null;
            Assert.That(Field<SoftShape[]>("perfectRipples")[0].gameObject.activeSelf, Is.True);
            game.PauseRun();
            float remaining = Field<float>("perfectFeedbackLeft");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(Field<float>("perfectFeedbackLeft"), Is.EqualTo(remaining));
            game.ResumeRun(); yield return new WaitForSecondsRealtime(.4f);
            foreach (var ripple in Field<SoftShape[]>("perfectRipples")) Assert.That(ripple.gameObject.activeSelf, Is.False);
            Invoke("StartPerfectFeedback", Vector2.zero, Color.magenta); yield return null;
            Invoke("ClearBoardEffects");
            Assert.That(Field<float>("perfectFeedbackLeft"), Is.Zero);
            foreach (var ripple in Field<SoftShape[]>("perfectRipples")) Assert.That(ripple.gameObject.activeSelf, Is.False);
            game.Saves.Data.ReduceEffects = true;
            Invoke("StartPerfectFeedback", Vector2.zero, Color.magenta); yield return null;
            Assert.That(Field<float>("perfectFeedbackLeft"), Is.Zero);
        }

        IEnumerator Capture(string name, int width, int height, Action setup = null)
        {
            var canvas = root.GetComponentInChildren<Canvas>();
            var go = new GameObject("Badge capture camera"); var camera = go.AddComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var safe = Field<RectTransform>("safe"); safe.GetComponent<SafeArea>().enabled = false;
            safe.anchorMin = new Vector2(0, 34f / height); safe.anchorMax = new Vector2(1, 1 - 62f / height);
            yield return null; Canvas.ForceUpdateCanvases(); setup?.Invoke();
            foreach (var component in root.GetComponentsInChildren<SafeArea>()) component.enabled = false;
            Canvas.ForceUpdateCanvases(); Invoke("LayoutResults"); camera.Render();
            var panel = Field<RectTransform>("results");
            if (!Field<RectTransform>("overlay").gameObject.activeSelf)
            {
                float bottom = 0;
                foreach (RectTransform row in panel)
                {
                    if (!row.gameObject.activeSelf) continue;
                    float top = -row.anchoredPosition.y - row.rect.height * .5f;
                    Assert.That(top, Is.GreaterThanOrEqualTo(bottom + 3), row.name);
                    bottom = top + row.rect.height;
                    Assert.That(bottom, Is.LessThanOrEqualTo(panel.rect.height), row.name);
                }
            }
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory("TestResults/screens"); File.WriteAllBytes("TestResults/screens/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(go);
        }
    }
}

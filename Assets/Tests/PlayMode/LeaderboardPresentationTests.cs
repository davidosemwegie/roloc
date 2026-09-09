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
using UnityEngine.UI;
using SafeArea = Roloc.Presentation.SafeArea;

namespace Roloc.Tests
{
    public sealed class LeaderboardPresentationTests
    {
        GameObject root;
        RolocGame game;
        DailyConnection connection;
        string directory;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-board-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Leaderboard test"); root.SetActive(false); root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>(); game.SaveDirectoryOverride = directory;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            root.SetActive(true); game.Saves.Data.TutorialCompleted = true;
            connection = ScriptableObject.CreateInstance<DailyConnection>();
            Set("leaderboards", new LeaderboardClient(new DailyClient(connection, directory), directory));
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty); UnityEngine.Object.Destroy(connection); UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        object Invoke(string method, params object[] args) => typeof(RolocGame).GetMethod(method, Private).Invoke(game, args);
        T Get<T>(string field) => (T)typeof(RolocGame).GetField(field, Private).GetValue(game);
        void Set(string field, object value) => typeof(RolocGame).GetField(field, Private).SetValue(game, value);

        [UnityTest] public IEnumerator OfflineRunStartsImmediatelyAndIncompleteRunDoesNotSubmit()
        {
            Invoke("BeginRunNow");
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(Get<LeaderboardTicket>("regularTicket"), Is.Null);
            Assert.That(Get<string>("regularRankingCaption"), Does.Contain("Local score"));
            Set("regularTicket", new LeaderboardTicket { runId = "fixture" });
            Invoke("SubmitRegularLeaderboard");
            Assert.That(Get<bool>("regularSubmitted"), Is.False);
            game.ShowMenu();
            Assert.That(Get<LeaderboardTicket>("regularTicket"), Is.Null);
            yield return null;
        }

        [UnityTest] public IEnumerator BoardRowsScrollAndFitCompactAndTallSafeAreas()
        {
            foreach (float height in new[] { 560f, 840f })
            {
                var safe = Get<RectTransform>("safe"); safe.GetComponent<Roloc.Presentation.SafeArea>().enabled = false;
                safe.anchorMin = safe.anchorMax = new Vector2(.5f, .5f); safe.sizeDelta = new Vector2(360, height);
                Invoke("ShowLeaderboardBoard"); yield return null; Canvas.ForceUpdateCanvases();
                var viewport = Get<RectTransform>("overlay").GetComponentsInChildren<ScrollRect>().Single();
                var panel = (RectTransform)viewport.transform.parent;
                Assert.That(panel.rect.height, Is.LessThanOrEqualTo(height));
                Assert.That(viewport.viewport.rect.height, Is.GreaterThan(44));
                var labels = panel.GetComponentsInChildren<Text>();
                var status = labels.Single(label => label.text.StartsWith("Couldn’t load"));
                var personal = labels.Single(label => label.text.StartsWith("Your best:"));
                foreach (Transform child in viewport.content) UnityEngine.Object.Destroy(child.gameObject);
                yield return null;
                var entries = Enumerable.Range(1, 100).Select(rank => new LeaderboardEntry {
                    nickname = "Player_123456789", rank = rank, score = 101 - rank, isMe = rank == 20
                }).ToArray();
                Invoke("RenderLeaderboardBoard", new LeaderboardBoard { enabled = true, date = "2026-09-08", participants = 120,
                    provisional = true, entries = entries, personal = new LeaderboardEntry { rank = 119, score = 1 } }, viewport.content, status, personal);
                Assert.That(viewport.content.childCount, Is.EqualTo(100));
                Assert.That(viewport.content.rect.height, Is.GreaterThan(viewport.viewport.rect.height));
                Assert.That(personal.text, Does.Contain("#119"));
                AssertLayout(panel);
            }
        }

        static void AssertLayout(RectTransform panel)
        {
            var rows = panel.Cast<RectTransform>().Where(child => child.gameObject.activeSelf).ToArray();
            foreach (var child in rows)
            {
                Assert.That(Mathf.Abs(child.anchoredPosition.y) + child.rect.height / 2, Is.LessThanOrEqualTo(panel.rect.height / 2 + 1), child.name);
                var label = child.GetComponent<Text>();
                if (label) Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(child.rect.height + 1), label.text + " clips vertically");
            }
            for (int a = 0; a < rows.Length; a++)
                for (int b = a + 1; b < rows.Length; b++)
                {
                    var first = new Rect(rows[a].anchoredPosition + rows[a].rect.min, rows[a].rect.size);
                    var second = new Rect(rows[b].anchoredPosition + rows[b].rect.min, rows[b].rect.size);
                    Assert.That(first.Overlaps(second), Is.False, rows[a].name + " overlaps " + rows[b].name);
                }
            var buttons = panel.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(44));
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(44));
            }
        }

        [UnityTest] public IEnumerator NicknameFieldReceivesTapFocusAndFilteredText()
        {
            Invoke("ShowLeaderboardProfile", null, (Action)(() => { }), null);
            yield return null; Canvas.ForceUpdateCanvases();
            var input = Get<RectTransform>("overlay").GetComponentInChildren<InputField>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, input.transform.position) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.EqualTo(input.gameObject), "A real tap must hit the nickname input, not the overlay panel.");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(input.isFocused, Is.True);
            input.ProcessEvent(Event.KeyboardEvent("a"));
            input.ProcessEvent(Event.KeyboardEvent("b"));
            input.ProcessEvent(Event.KeyboardEvent("c"));
            input.ProcessEvent(new Event { type = EventType.KeyDown, character = 'é' });
            input.ForceLabelUpdate(); // ProcessEvent bypasses OnUpdateSelected's final label refresh.
            Assert.That(input.text, Is.EqualTo("abc"));
            Assert.That(input.placeholder.gameObject.activeSelf, Is.False);
            Assert.That(input.keyboardType, Is.EqualTo(TouchScreenKeyboardType.ASCIICapable));
            AssertLayout((RectTransform)input.transform.parent);
            Assert.That(input.transform.parent.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Rank my runs")), Is.False);
        }

        [UnityTest] public IEnumerator CaptureLeaderboardStatesAtPhoneSizes()
        {
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(440, 956) })
            {
                var canvas = root.GetComponentInChildren<Canvas>();
                var cameraObject = new GameObject("Leaderboard capture camera");
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.transform.position = new Vector3(0, 0, -10);
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var safe = Get<RectTransform>("safe"); safe.GetComponent<SafeArea>().enabled = false;
                safe.anchorMin = new Vector2(0, 34f / size.y); safe.anchorMax = new Vector2(1, 1 - 62f / size.y);
                yield return null; Canvas.ForceUpdateCanvases();
                foreach (string state in new[] { "empty", "scores", "join", "profile" })
                {
                    if (state == "join" || state == "profile")
                        Invoke("ShowLeaderboardProfile", state == "join" ? null : new LeaderboardProfile { nickname = "Player_1234567890", participating = false }, (Action)(() => { }), null);
                    else Invoke("ShowLeaderboardBoard");
                    var overlay = Get<RectTransform>("overlay");
                    var overlaySafe = overlay.GetComponentsInChildren<SafeArea>().Last(); overlaySafe.enabled = false;
                    var overlayRect = (RectTransform)overlaySafe.transform;
                    overlayRect.anchorMin = safe.anchorMin; overlayRect.anchorMax = safe.anchorMax;
                    yield return null; Canvas.ForceUpdateCanvases();
                    RectTransform panel;
                    if (state == "empty" || state == "scores")
                    {
                        var scroll = overlay.GetComponentInChildren<ScrollRect>(); panel = (RectTransform)scroll.transform.parent;
                        var status = panel.GetComponentsInChildren<Text>().Single(text => text.text.StartsWith("Couldn’t load"));
                        var personal = panel.GetComponentsInChildren<Text>().Single(text => text.text.StartsWith("Your best:"));
                        foreach (Transform child in scroll.content) UnityEngine.Object.Destroy(child.gameObject);
                        yield return null;
                        var entries = state == "empty" ? new LeaderboardEntry[0] : Enumerable.Range(1, 100).Select(rank => new LeaderboardEntry {
                            nickname = "Player_1234567890", score = 101 - rank, rank = rank
                        }).ToArray();
                        Invoke("RenderLeaderboardBoard", new LeaderboardBoard { date = "2026-09-09", enabled = state != "empty", participants = entries.Length,
                            provisional = true, entries = entries, personal = state == "empty" ? null : new LeaderboardEntry { rank = 119, score = 1 } }, scroll.content, status, personal);
                        if (state == "empty")
                        {
                            Assert.That(scroll.content.rect.height, Is.EqualTo(scroll.viewport.rect.height).Within(1));
                            var message = scroll.content.GetComponentInChildren<Text>();
                            Assert.That(message.rectTransform.anchoredPosition.y, Is.EqualTo(-scroll.viewport.rect.height / 2).Within(1));
                        }
                    }
                    else panel = (RectTransform)overlay.GetComponentInChildren<InputField>().transform.parent;
                    Canvas.ForceUpdateCanvases(); AssertLayout(panel);
                    Assert.That(panel.rect.height, Is.LessThanOrEqualTo(overlayRect.rect.height));
                    camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    Directory.CreateDirectory("TestResults/screens");
                    File.WriteAllBytes("TestResults/screens/leaderboard-" + state + "-" + size.x + ".png", image.EncodeToPNG());
                    RenderTexture.active = previous; UnityEngine.Object.Destroy(image);
                }
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                camera.targetTexture = null; target.Release();
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
            }
        }

        [UnityTest] public IEnumerator MenuResultsActionsAndNewOverlayInvalidateOldBoard()
        {
            var menu = Get<RectTransform>("menu");
            Assert.That(menu.GetComponentsInChildren<Button>().Count(button => button.GetComponentInChildren<Text>().text == "Leaderboards"), Is.EqualTo(1));
            Invoke("BeginRunNow"); Invoke("FinishRun");
            Assert.That(Get<Button>("leaderboardResultButton").gameObject.activeSelf, Is.True);
            Assert.That(Get<Button>("shareButton").gameObject.activeSelf, Is.False);
            Invoke("ShowLeaderboardBoard"); yield return null;
            int generation = Get<int>("leaderboardViewGeneration");
            var panel = (RectTransform)Get<RectTransform>("overlay").GetComponentInChildren<ScrollRect>().transform.parent;
            Invoke("ShowSettings", false);
            Assert.That(Invoke("LeaderboardViewCurrent", generation, panel), Is.False);
            Assert.That(Get<bool>("leaderboardVisible"), Is.False);
        }

        [TestCase("abc", true)] [TestCase("Player_123456789", true)]
        [TestCase("ab", false)] [TestCase("Player_1234567890", false)]
        [TestCase("a b", false)] [TestCase("éab", false)] [TestCase("<b>", false)]
        public void NicknameRulesMatchPublicContract(string nickname, bool valid)
        {
            var method = typeof(RolocGame).GetMethod("ValidLeaderboardNickname", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method.Invoke(null, new object[] { nickname }), Is.EqualTo(valid));
        }
    }
}

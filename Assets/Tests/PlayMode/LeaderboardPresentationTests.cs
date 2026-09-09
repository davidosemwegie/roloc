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
using UnityEngine.TestTools;
using UnityEngine.UI;

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
                    nickname = "Player_1234567890", rank = rank, score = 101 - rank, isMe = rank == 20
                }).ToArray();
                Invoke("RenderLeaderboardBoard", new LeaderboardBoard { enabled = true, date = "2026-09-08", participants = 120,
                    provisional = true, entries = entries, personal = new LeaderboardEntry { rank = 119, score = 1 } }, viewport.content, status, personal);
                Assert.That(viewport.content.childCount, Is.EqualTo(100));
                Assert.That(viewport.content.rect.height, Is.GreaterThan(viewport.viewport.rect.height));
                Assert.That(personal.text, Does.Contain("#119"));
                foreach (RectTransform child in panel)
                    Assert.That(Mathf.Abs(child.anchoredPosition.y) + child.rect.height / 2, Is.LessThanOrEqualTo(panel.rect.height / 2 + 1), child.name);
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

        [TestCase("abc", true)] [TestCase("Player_1234567890", true)]
        [TestCase("ab", false)] [TestCase("Player_12345678901", false)]
        [TestCase("a b", false)] [TestCase("éab", false)] [TestCase("<b>", false)]
        public void NicknameRulesMatchPublicContract(string nickname, bool valid)
        {
            var method = typeof(RolocGame).GetMethod("ValidLeaderboardNickname", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method.Invoke(null, new object[] { nickname }), Is.EqualTo(valid));
        }
    }
}

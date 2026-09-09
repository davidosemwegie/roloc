using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public class GameFlowTests
    {
        GameObject root;
        RolocGame game;
        string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "roloc-flow-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Test game"); root.SetActive(false);
            root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>(); game.SaveDirectoryOverride = directory;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            game.difficulty.RandomFlowEnabled = false;
            game.difficulty.FlowTransitionSeconds = 0;
            game.difficulty.RotationSeconds = 0;
            root.SetActive(true);
            game.Saves.Data.SelectedMode = "Flow";
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty);
            UnityEngine.Object.Destroy(root); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        PuckView ActivePuck() => root.GetComponentsInChildren<PuckView>(true).Single(p => p.ColorIndex == game.Session.ActiveColor);

        void Drop(PuckView puck, bool match)
        {
            Canvas.ForceUpdateCanvases();
            var ring = root.GetComponentsInChildren<SoftShape>(true).Single(s => s.name == "Ring " + puck.ColorIndex);
            Vector2 down = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Vector2 up = match ? RectTransformUtility.WorldToScreenPoint(null, ring.rectTransform.position) : down;
            var e = new PointerEventData(EventSystem.current) { pointerId = 1, position = down };
            puck.OnPointerDown(e); e.position = up; puck.OnDrag(e); puck.OnPointerUp(e);
        }

        [UnityTest]
        public IEnumerator FlowRetriesTwiceAndKeepsCreditedProgressOnAbandon()
        {
            game.Saves.Data.SelectedMode = "Flow"; game.Saves.Data.TutorialCompleted = true;
            game.difficulty.TransitionSeconds = 0;
            game.BeginRun(); yield return null;
            Assert.That(game.Session.Chances, Is.EqualTo(3));
            Assert.That(game.Session.DurationSeconds, Is.EqualTo(3.5f));
            Drop(ActivePuck(), true);
            Assert.That(game.Session.PerfectCount, Is.EqualTo(1));
            Assert.That(game.Saves.Data.ProgressPoints, Is.EqualTo(2));
            int target = game.Session.ActiveColor;
            Drop(ActivePuck(), false);
            Assert.That(game.Session.Chances, Is.EqualTo(2));
            Assert.That(game.Session.ActiveColor, Is.EqualTo(target));
            Assert.That(game.Session.Combo, Is.Zero);
            Assert.That(game.Session.PerfectStreak, Is.Zero);
            game.ShowMenu();
            var reloaded = new Roloc.Services.SaveService(directory);
            Assert.That(reloaded.Data.ProgressPoints, Is.EqualTo(2));
            Assert.That(reloaded.Data.GamesPlayed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FlowPauseHidesBoardAndPreservesPerfectStreak()
        {
            game.Saves.Data.SelectedMode = "Flow"; game.Saves.Data.TutorialCompleted = true;
            game.difficulty.TransitionSeconds = 0; game.BeginRun(); yield return null;
            Drop(ActivePuck(), true);
            game.PauseRun();
            Assert.That(root.GetComponentsInChildren<PuckView>().Length, Is.Zero);
            float remaining = game.Session.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(game.Session.PerfectStreak, Is.EqualTo(1));
            game.ResumeRun();
            Assert.That(root.GetComponentsInChildren<PuckView>().Length, Is.EqualTo(4));
            Drop(ActivePuck(), true);
            Assert.That(game.Session.PerfectStreak, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TutorialCompletesWithoutRecordingGame()
        {
            game.BeginRun(); yield return null;
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Tutorial));
            Drop(ActivePuck(), true);
            Assert.That(game.Saves.Data.TutorialCompleted, Is.True);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
            game.BeginRun(); Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
        }

        [UnityTest]
        public IEnumerator ValidDragScoresAndWrongDropRecordsExactlyOnce()
        {
            game.Saves.Data.TutorialCompleted = true; game.BeginRun(); yield return null;
            Drop(ActivePuck(), true);
            Assert.That(game.Session.Score, Is.EqualTo(1));
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            for (int chances = 2; chances >= 1; chances--)
            {
                Drop(ActivePuck(), false);
                Assert.That(game.Session.Chances, Is.EqualTo(chances));
                Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            }
            Drop(ActivePuck(), false);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
            Assert.That(game.Saves.Data.HighScore, Is.EqualTo(1));
            yield return null;
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PauseCancelsDragAndFreezesTimer()
        {
            game.Saves.Data.TutorialCompleted = true; game.BeginRun(); yield return null;
            var puck = ActivePuck();
            var e = new PointerEventData(EventSystem.current) { pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position) };
            puck.OnPointerDown(e); Assert.That(puck.IsDragging, Is.True);
            game.PauseRun(); float remaining = game.Session.RemainingSeconds;
            puck.OnPointerUp(e); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Paused));
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(puck.IsDragging, Is.False);
            game.ResumeRun(); Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PuckShuffleMovesHomesAndPausesUntilTransitionFinishes()
        {
            game.Saves.Data.TutorialCompleted = true;
            game.difficulty.TransitionSeconds = 0;
            game.BeginRun(); yield return null;
            for (int i = 0; i < 44; i++) Drop(ActivePuck(), true);
            var pucks = root.GetComponentsInChildren<PuckView>();
            var previousHomes = pucks.Select(p => p.Home).ToArray();
            game.difficulty.TransitionSeconds = .24f;

            Drop(ActivePuck(), true);
            Assert.That(game.Session.Score, Is.EqualTo(45));
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            Assert.That(pucks.Select(p => p.Home).SequenceEqual(previousHomes), Is.False);
            Assert.That(pucks.Select(p => p.Home).Distinct().Count(), Is.EqualTo(4));
            yield return null;
            game.PauseRun();
            var pausedPositions = pucks.Select(p => p.Rect.anchoredPosition).ToArray();
            float remaining = game.Session.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.1f);
            CollectionAssert.AreEqual(pausedPositions, pucks.Select(p => p.Rect.anchoredPosition).ToArray());
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(remaining));

            game.ResumeRun();
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            var e = new PointerEventData(EventSystem.current) { position = Vector2.zero };
            ActivePuck().OnPointerDown(e);
            Assert.That(ActivePuck().IsDragging, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            foreach (var puck in pucks)
                Assert.That(Vector2.Distance(puck.Rect.anchoredPosition, puck.Home), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator RestartsReuseExactlyOneMusicSource()
        {
            game.Saves.Data.TutorialCompleted = true;
            for (int i = 0; i < 4; i++) { game.BeginRun(); game.ShowMenu(); }
            yield return null;
            var sources = root.GetComponents<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(3));
            Assert.That(sources.Count(s => s.loop), Is.EqualTo(1));
            Assert.That(sources.All(s => !s.isPlaying), Is.True);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
        }
    }
}

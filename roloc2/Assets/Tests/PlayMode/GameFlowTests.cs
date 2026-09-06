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
            root.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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

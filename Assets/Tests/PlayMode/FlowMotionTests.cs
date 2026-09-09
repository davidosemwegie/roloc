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
    public sealed class FlowMotionTests
    {
        GameObject root;
        RolocGame game;
        DifficultySettings settings;
        string directory;
        PuckView[] Pucks => root.GetComponentsInChildren<PuckView>().OrderBy(p => p.ColorIndex).ToArray();
        SoftShape[] Rings => root.GetComponentsInChildren<SoftShape>().Where(s => s.name.StartsWith("Ring "))
            .OrderBy(s => s.name).ToArray();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "roloc-flow-motion-" + Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<DifficultySettings>();
            settings.Flow = new FlowSettings { StartScore = 0, DriftStartScore = 0, RotationStartScore = 0,
                MinMatches = 3, MaxMatches = 3, BreatherCooldownMatches = 0 };
            settings.TransitionSeconds = settings.FlowTransitionSeconds = settings.RotationSeconds = 0;
            root = new GameObject("Flow motion test"); root.SetActive(false);
            root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>();
            game.difficulty = settings; game.RandomSeedOverride = 42; game.SaveDirectoryOverride = directory;
            root.SetActive(true); game.Saves.Data.SelectedMode = "Flow"; yield return null;
            game.Saves.Data.TutorialCompleted = true;
            game.BeginRun(); yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(settings);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        void Match()
        {
            Canvas.ForceUpdateCanvases();
            var puck = Pucks[game.Session.ActiveColor];
            var e = new PointerEventData(EventSystem.current) { pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position) };
            puck.OnPointerDown(e);
            e.position = RectTransformUtility.WorldToScreenPoint(null, Rings[puck.ColorIndex].rectTransform.position);
            puck.OnDrag(e); puck.OnPointerUp(e);
        }

        void Reach(FlowMode mode)
        {
            for (int i = 0; i < 200 && game.Session.FlowMode != mode; i++)
            {
                Match();
                // Expanded layouts impose a minimum transition even with test durations zero.
                // Finish intervening transitions, preserving the target animation under test.
                if (game.Session.State == RoundState.Transition && game.Session.FlowMode != mode)
                    typeof(RolocGame).GetMethod("CompleteBoardTransition",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, null);
            }
            Assert.That(game.Session.FlowMode, Is.EqualTo(mode), "Seeded play must exercise the requested mode.");
        }

        [UnityTest]
        public IEnumerator FloatingMovesInactivePucksButNeverTakesOverADrag()
        {
            Reach(FlowMode.Floating);
            yield return new WaitForSecondsRealtime(.25f);
            var pucks = Pucks;
            var active = pucks[game.Session.ActiveColor];
            Assert.That(Vector2.Distance(active.Rect.anchoredPosition, active.Home), Is.LessThan(.01f));
            Assert.That(pucks.Where(p => p != active).Any(p => Vector2.Distance(p.Rect.anchoredPosition, p.Home) > .1f), Is.True);
            foreach (var p in pucks) Assert.That(p.IdleOffset.magnitude, Is.LessThanOrEqualTo(5));
            var e = new PointerEventData(EventSystem.current) { pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, active.Rect.position) };
            active.OnPointerDown(e); e.position += new Vector2(15, 15); active.OnDrag(e);
            var held = active.Rect.anchoredPosition;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(active.Rect.anchoredPosition, Is.EqualTo(held));
            Assert.That(active.IsDragging, Is.True);
        }

        [UnityTest]
        public IEnumerator DriftingUsesVisibleTargetsAndFreezesPositionScaleAndTimerOnPause()
        {
            Reach(FlowMode.Drifting);
            var rings = Rings;
            var homes = rings.Select(r => r.rectTransform.anchoredPosition).ToArray();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(rings.Where((r, c) => Vector2.Distance(r.rectTransform.anchoredPosition, homes[c]) > .1f).Any(), Is.True);
            for (int c = 0; c < 4; c++)
                Assert.That(Vector2.Distance(rings[c].rectTransform.anchoredPosition, homes[c]), Is.LessThanOrEqualTo(10));
            game.PauseRun();
            var positions = rings.Select(r => r.rectTransform.anchoredPosition).ToArray();
            var scales = rings.Select(r => r.transform.localScale).ToArray();
            float time = game.Session.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.1f);
            CollectionAssert.AreEqual(positions, rings.Select(r => r.rectTransform.anchoredPosition).ToArray());
            CollectionAssert.AreEqual(scales, rings.Select(r => r.transform.localScale).ToArray());
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(time));
            game.ResumeRun();
            int score = game.Session.Score;
            Match(); Assert.That(game.Session.Score, Is.EqualTo(score + 1));
        }

        [UnityTest]
        public IEnumerator RotationFollowsSeparatedArcsAndCompletesBeforeTimingResumes()
        {
            settings.RotationSeconds = .6f;
            Reach(FlowMode.Rotation);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Transition));
            float time = game.Session.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(time));
            var pucks = Pucks;
            for (int a = 0; a < 4; a++)
                for (int b = a + 1; b < 4; b++)
                    Assert.That(Vector2.Distance(pucks[a].Rect.anchoredPosition, pucks[b].Rect.anchoredPosition), Is.GreaterThan(80));
            game.PauseRun();
            var positions = pucks.Select(p => p.Rect.anchoredPosition).ToArray();
            yield return new WaitForSecondsRealtime(.1f);
            CollectionAssert.AreEqual(positions, pucks.Select(p => p.Rect.anchoredPosition).ToArray());
            game.ResumeRun();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            foreach (var p in pucks) Assert.That(Vector2.Distance(p.Rect.anchoredPosition, p.Home), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator BreatherAddsTimeAndRestartClearsTheVariation()
        {
            Reach(FlowMode.Breather);
            Assert.That(game.Session.DurationSeconds, Is.EqualTo(settings.GetFlowSeconds(game.Session.Score) + .65f).Within(.001f));
            Assert.That(game.Session.RemainingSeconds, Is.EqualTo(game.Session.DurationSeconds));
            game.BeginRun(); yield return null;
            Assert.That(game.Session.FlowMode, Is.EqualTo(FlowMode.Steady));
            Assert.That(game.Session.DurationSeconds, Is.EqualTo(3.5f));
            foreach (var puck in Pucks) Assert.That(puck.IdleOffset, Is.EqualTo(Vector2.zero));
        }
    }
}

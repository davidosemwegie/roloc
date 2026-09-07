using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Roloc.Tests
{
    /// <summary>Exercise device events, UI raycasts and drag dispatch rather than calling puck handlers.</summary>
    public sealed class PointerInputTests
    {
        GameObject root;
        RolocGame game;
        Touchscreen touchscreen;
        string directory;
        bool previousRunInBackground;
        InputSettings.BackgroundBehavior previousBackgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousRunInBackground = Application.runInBackground;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            // Headless PlayMode has no focused Game View. Keep queued touchscreen events
            // in the player input update instead of routing pointer events to the Editor.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            directory = Path.Combine(Path.GetTempPath(), "roloc-pointer-" + Guid.NewGuid().ToString("N"));
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            root = new GameObject("Pointer input test game");
            root.SetActive(false);
            root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>();
            game.SaveDirectoryOverride = directory;
            root.SetActive(true);
            yield return null;
            game.BeginTutorial();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            Application.runInBackground = previousRunInBackground;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        PuckView ActivePuck() => root.GetComponentsInChildren<PuckView>().Single(p => p.ColorIndex == game.Session.ActiveColor);

        Vector2 RingPosition(PuckView puck)
        {
            var ring = root.GetComponentsInChildren<SoftShape>().Single(s => s.name == "Ring " + puck.ColorIndex);
            return RectTransformUtility.WorldToScreenPoint(null, ring.rectTransform.position);
        }

        static void AssertPuckReceivesRaycast(PuckView puck, Vector2 position)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty, "The rendered puck must participate in UI raycasting.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject),
                Is.EqualTo(puck.gameObject), "The active puck must be the first pointer-down handler at its center.");
        }

        IEnumerator Touch(TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 41,
                phase = phase,
                position = position,
                pressure = phase == TouchPhase.Ended || phase == TouchPhase.Canceled ? 0 : 1
            });
            // Let the normal Input System update and EventSystem dispatch run in their own frame.
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TouchscreenDragThroughEventSystemCompletesTutorial()
        {
            var puck = ActivePuck();
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Vector2 target = RingPosition(puck);
            AssertPuckReceivesRaycast(puck, start);

            yield return Touch(TouchPhase.Began, start);
            Assert.That(puck.IsDragging, Is.True, "The device press must reach OnPointerDown.");
            yield return Touch(TouchPhase.Moved, Vector2.Lerp(start, target, .5f));
            Assert.That(puck.IsDragging, Is.True, "Beginning a drag must not release the puck.");
            Vector2 moved = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Assert.That(Vector2.Distance(moved, start), Is.GreaterThan(10), "The module must dispatch actual drag movement.");
            yield return Touch(TouchPhase.Moved, target);
            yield return Touch(TouchPhase.Ended, target);

            Assert.That(puck.IsDragging, Is.False);
            Assert.That(game.Saves.Data.TutorialCompleted, Is.True);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InactivePuckCanMoveButLosesAFlowChanceOnRelease()
        {
            game.Saves.Data.TutorialCompleted = true;
            game.BeginRun();
            yield return null;
            Canvas.ForceUpdateCanvases();
            int active = game.Session.ActiveColor;
            var puck = root.GetComponentsInChildren<PuckView>().First(p => p.ColorIndex != active);
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Vector2 target = RingPosition(puck);
            AssertPuckReceivesRaycast(puck, start);
            yield return Touch(TouchPhase.Began, start);
            yield return Touch(TouchPhase.Moved, target);
            Assert.That(puck.IsDragging, Is.True);
            Assert.That(game.Session.Chances, Is.EqualTo(3), "Dragging alone must not spend a chance.");
            Assert.That(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position), target), Is.LessThan(1));
            yield return Touch(TouchPhase.Ended, target);
            Assert.That(game.Session.Chances, Is.EqualTo(2));
            Assert.That(game.Session.LastFailure, Is.EqualTo(DropFailure.InactivePuck));
            Assert.That(game.Session.Score, Is.Zero);
            Assert.That(game.Session.ActiveColor, Is.EqualTo(active));
            Assert.That(game.Saves.Data.ProgressPoints, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InactivePuckReleaseEndsRushButCanceledTouchDoesNot()
        {
            game.Saves.Data.TutorialCompleted = true;
            game.Saves.Data.SelectedMode = "Rush";
            game.BeginRun();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var puck = root.GetComponentsInChildren<PuckView>().First(p => p.ColorIndex != game.Session.ActiveColor);
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Vector2 target = RingPosition(puck);
            yield return Touch(TouchPhase.Began, start);
            yield return Touch(TouchPhase.Moved, target);
            yield return Touch(TouchPhase.Canceled, target);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            start = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            yield return Touch(TouchPhase.Began, start);
            Assert.That(puck.IsDragging, Is.True);
            yield return Touch(TouchPhase.Moved, target);
            yield return Touch(TouchPhase.Ended, target);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Session.LastFailure, Is.EqualTo(DropFailure.InactivePuck));
            Assert.That(game.Session.Score, Is.Zero);
            Assert.That(game.Saves.Data.GamesPlayed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CanceledTouchInsideMatchingRingDoesNotScore()
        {
            game.Saves.Data.TutorialCompleted = true;
            game.BeginRun();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var puck = ActivePuck();
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position);
            Vector2 target = RingPosition(puck);
            AssertPuckReceivesRaycast(puck, start);

            yield return Touch(TouchPhase.Began, start);
            Assert.That(puck.IsDragging, Is.True);
            yield return Touch(TouchPhase.Moved, target);
            yield return Touch(TouchPhase.Canceled, target);

            Assert.That(puck.IsDragging, Is.False);
            Assert.That(game.Session.State, Is.EqualTo(RoundState.Playing));
            Assert.That(game.Session.Score, Is.Zero);
            Assert.That(game.Saves.Data.GamesPlayed, Is.Zero);
        }
    }
}

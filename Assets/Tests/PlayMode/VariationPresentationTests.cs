using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Roloc.Core;
using Roloc.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Roloc.Tests
{
    public sealed class VariationPresentationTests
    {
        GameObject root;
        RolocGame game;
        string directory;
        PuckView[] Pucks => Get<PuckView[]>("pucks");
        RectTransform[] Rings => Get<RectTransform[]>("rings");
        T Get<T>(string name) => (T)typeof(RolocGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        void Invoke(string name, params object[] args) => typeof(RolocGame)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "ring-rush-variation-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Variation presentation test"); root.SetActive(false);
            root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>();
            game.SaveDirectoryOverride = directory; game.RandomSeedOverride = 283;
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            root.SetActive(true); game.Saves.Data.TutorialCompleted = true;
            game.Saves.Data.SymbolsEnabled = true;
            game.Saves.Data.SelectedMode = "Flow";
            yield return null;
            // Advance the presentation explicitly so capture frame latency cannot time out a run.
            game.enabled = false;
            game.BeginRun();
            foreach (var puck in Pucks) puck.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(game.difficulty); UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        void Match()
        {
            var previousMode = game.Session.FlowMode;
            var rings = game.Session.RingOrder; var pucks = game.Session.PuckOrder;
            Assert.That(game.Session.Drop(game.Session.ActiveColor, true), Is.EqualTo(MatchResult.Matched));
            Invoke("BeginBoardTransition", previousMode != game.Session.FlowMode || rings != game.Session.RingOrder || pucks != game.Session.PuckOrder,
                previousMode != game.Session.FlowMode, rings != game.Session.RingOrder && pucks != game.Session.PuckOrder);
            Invoke("CompleteBoardTransition"); Refresh(0);
        }

        void Reach(FlowMode mode)
        {
            game.ShowMenu(); game.BeginRun();
            for (int i = 0; i < 3000 && game.Session.FlowMode != mode; i++) Match();
            Assert.That(game.Session.FlowMode, Is.EqualTo(mode));
            Refresh(0);
        }

        void Refresh(float dt)
        {
            Invoke("RefreshBoard", dt);
            Invoke("UpdateExperience");
            foreach (var puck in Pucks)
                if (puck.gameObject.activeSelf && !puck.IsDragging) puck.SnapHome();
        }

        [UnityTest]
        public IEnumerator FormerStillSelectionUsesLivelyAndPreservesItsRecord()
        {
            game.Saves.Data.SelectedBoard = "Still";
            game.Saves.GetRecord("Flow", "Still").HighScore = 73;
            game.BeginRun();
            Assert.That(game.Session.BoardStyle, Is.EqualTo(BoardStyle.Lively));
            Assert.That(game.Saves.Data.SelectedBoard, Is.EqualTo("Lively"));
            Assert.That(game.Saves.GetRecord("Flow", "Still").HighScore, Is.EqualTo(73));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrbitMovesDisplayedTargetsAndFreezesOnPauseWithoutPullingDraggedPuck()
        {
            Reach(FlowMode.DualOrbit);
            var ring = Rings[0].anchoredPosition; var home = Pucks[0].Home;
            Refresh(1);
            Assert.That(Vector2.Distance(ring, Rings[0].anchoredPosition), Is.GreaterThan(1));
            Assert.That(Vector2.Distance(home, Pucks[0].Home), Is.GreaterThan(1));
            float clock = Get<float>("orbitSeconds");
            game.PauseRun(); Refresh(10);
            Assert.That(Get<float>("orbitSeconds"), Is.EqualTo(clock));
            Assert.That(Get<RectTransform>("board").gameObject.activeSelf, Is.False);
            game.ResumeRun(); Refresh(0);
            Canvas.ForceUpdateCanvases();
            var puck = Pucks[game.Session.ActiveColor];
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 7,
                position = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position) };
            puck.OnPointerDown(pointer); pointer.position += new Vector2(12, 5); puck.OnDrag(pointer);
            Assert.That(puck.IsDragging, Is.True);
            var fingerPosition = puck.Rect.anchoredPosition;
            Refresh(1);
            Assert.That(puck.Rect.anchoredPosition, Is.EqualTo(fingerPosition));
            puck.CancelDrag(); Refresh(0);
            yield return Capture("dual-orbit", 440, 956, true);
            yield return Capture("dual-orbit-compact", 375, 667, true);
            Reach(FlowMode.PuckOrbit);
            yield return Capture("puck-orbit", 440, 956, true);
            yield return Capture("puck-orbit-compact", 375, 667, true);
            Reach(FlowMode.RingOrbit);
            yield return Capture("ring-orbit", 440, 956, true);
            yield return Capture("ring-orbit-compact", 375, 667, true);
        }

        [UnityTest]
        public IEnumerator FrameDrivenOrbitTracksHomeAndDoesNotPullTheFinger()
        {
            Reach(FlowMode.DualOrbit);
            foreach (var item in Pucks) item.enabled = true;
            game.enabled = true;
            var puck = Pucks[game.Session.ActiveColor];
            Vector2 start = puck.Rect.anchoredPosition;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Vector2.Distance(start, puck.Rect.anchoredPosition), Is.GreaterThan(1));
            Assert.That(Vector2.Distance(puck.Home, puck.Rect.anchoredPosition), Is.LessThan(3));
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 17,
                position = RectTransformUtility.WorldToScreenPoint(null, puck.Rect.position) };
            puck.OnPointerDown(pointer); pointer.position += new Vector2(12, 5); puck.OnDrag(pointer);
            Vector2 held = puck.Rect.anchoredPosition;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(puck.IsDragging, Is.True);
            Assert.That(puck.Rect.anchoredPosition, Is.EqualTo(held));
            int score = game.Session.Score;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, Rings[puck.ColorIndex].position);
            puck.OnPointerUp(pointer);
            Assert.That(game.Session.Score, Is.EqualTo(score + 1), "A drop uses the ring that is actually displayed.");
            game.PauseRun();
        }

        [UnityTest]
        public IEnumerator CombinedMotionUsesBothGroupsWithoutOverlappingOrClipping()
        {
            foreach (var mode in new[] { FlowMode.FloatingDrifting, FlowMode.PuckOrbitDrifting, FlowMode.RingOrbitFloating })
            {
                Reach(mode);
                int inactive = (game.Session.ActiveColor + 1) % 4;
                Refresh(1);
                Vector2 ring = Rings[inactive].anchoredPosition;
                Vector2 puck = Pucks[inactive].Rect.anchoredPosition;
                Refresh(1);
                Assert.That(Vector2.Distance(ring, Rings[inactive].anchoredPosition), Is.GreaterThan(.01f));
                Assert.That(Vector2.Distance(puck, Pucks[inactive].Rect.anchoredPosition), Is.GreaterThan(.01f));
                yield return Capture(mode.ToString(), 440, 956, true);
                game.Saves.Data.ReduceEffects = true;
                game.Saves.Data.EquippedPuck = "glass"; game.Saves.Data.EquippedRing = "orbit";
                game.Saves.Data.EquippedBackground = "dusk"; Invoke("ApplyAppearance");
                yield return Capture(mode + "-compact", 375, 667, true);
            }
        }

        [UnityTest]
        public IEnumerator PalettePersistsIntoMovementAndOnlyResetsForANewRun()
        {
            Reach(FlowMode.ColorShift);
            int palette = game.Session.PaletteIndex;
            Assert.That(Get<float>("transitionDuration"), Is.EqualTo(Get<float>("paletteMoveSeconds") + .45f).Within(.001f));
            Assert.That(Get<int>("displayedPalette"), Is.EqualTo(palette));
            AssertPalette();
            int active = game.Session.ActiveColor;
            game.Session.Drop(active, false);
            Invoke("BeginBoardTransition", false, false, false); Invoke("CompleteBoardTransition");
            Assert.That(Get<int>("displayedPalette"), Is.EqualTo(palette));
            Assert.That(game.Session.ActiveColor, Is.EqualTo(active));
            yield return Capture("color-shift", 440, 956);
            // Only the preview palette is overridden; real scheduling was exercised above.
            Invoke("ApplyBoardPalette", 4); Refresh(0); AssertPalette();
            yield return Capture("four-pinks", 440, 956);
            yield return Capture("four-pinks-compact", 375, 667);
            while (game.Session.FlowMode == FlowMode.ColorShift) Match();
            Assert.That(Get<int>("displayedPalette"), Is.EqualTo(palette));
            Assert.That(Get<CanvasGroup>("boardVisibility").alpha, Is.EqualTo(1));
            AssertPalette();
            for (int i = 0; i < 3000 && !VariationMotion.HasOrbit(game.Session.FlowMode); i++) Match();
            Assert.That(VariationMotion.HasOrbit(game.Session.FlowMode), Is.True);
            Assert.That(Get<int>("displayedPalette"), Is.EqualTo(game.Session.PaletteIndex));
            Assert.That(game.Session.PaletteIndex, Is.GreaterThanOrEqualTo(0));
            AssertPalette();
            yield return Capture("persistent-palette-orbit", 440, 956, true);
            game.ShowMenu(); game.BeginRun();
            Assert.That(Get<int>("displayedPalette"), Is.EqualTo(-1));
        }

        void AssertPalette()
        {
            for (int i = 0; i < 4; i++)
            {
                var ring = Rings[i].GetComponent<SoftShape>().color;
                var puck = Pucks[i].GetComponent<SoftShape>().color;
                Assert.That(puck.r, Is.EqualTo(ring.r)); Assert.That(puck.g, Is.EqualTo(ring.g));
                Assert.That(puck.b, Is.EqualTo(ring.b));
                Assert.That(puck.a, Is.EqualTo(i == game.Session.ActiveColor ? 1 : .18f));
                for (int j = i + 1; j < 4; j++)
                    Assert.That(ring, Is.Not.EqualTo(Rings[j].GetComponent<SoftShape>().color));
            }
        }

        IEnumerator Capture(string name, int width, int height, bool sweep = false)
        {
            var canvas = root.GetComponentInChildren<Canvas>();
            var cameraObject = new GameObject("Variation capture camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var safe = Get<RectTransform>("safe"); safe.GetComponent<SafeArea>().enabled = false;
            safe.anchorMin = new Vector2(0, 34f / height); safe.anchorMax = new Vector2(1, 1 - 62f / height);
            yield return null;
            Canvas.ForceUpdateCanvases(); Refresh(0);
            int phases = sweep ? 120 : 1;
            for (int i = 0; i < phases; i++)
            {
                Refresh(sweep ? 1 : 0);
                if (sweep) AssertVisualBounds();
            }
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory("TestResults/screens");
            File.WriteAllBytes("TestResults/screens/variation-" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }

        void AssertVisualBounds()
        {
            var safe = Get<RectTransform>("safe"); var board = Get<RectTransform>("board");
            for (int i = 0; i < 4; i++)
            {
                // Include maximum bounce/shadow envelopes, not only the flat circular hit radius.
                AssertInside(Rings[i], 137 * .5f * 1.1f, safe, board);
                AssertInside(Pucks[i].Rect, 79 * .5f * 1.045f, safe, board);
                for (int j = 0; j < 4; j++)
                    Assert.That(Vector2.Distance(Pucks[i].Rect.anchoredPosition, Rings[j].anchoredPosition)
                        - 137 * .5f * 1.1f - 79 * .5f * 1.045f, Is.GreaterThanOrEqualTo(12));
            }
        }

        static void AssertInside(RectTransform piece, float radius, RectTransform safe, RectTransform board)
        {
            Vector2 center = safe.InverseTransformPoint(piece.position);
            float extent = radius * board.localScale.x;
            Assert.That(center.x - extent, Is.GreaterThanOrEqualTo(safe.rect.xMin + 5), piece.name + " left clipping");
            Assert.That(center.x + extent, Is.LessThanOrEqualTo(safe.rect.xMax - 5), piece.name + " right clipping");
            Assert.That(center.y - extent, Is.GreaterThanOrEqualTo(safe.rect.yMin + 118), piece.name + " bottom clipping");
            Assert.That(center.y + extent, Is.LessThanOrEqualTo(safe.rect.yMax - 235), piece.name + " top clipping");
        }
    }
}

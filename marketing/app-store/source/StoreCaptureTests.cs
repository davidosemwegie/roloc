// Capture-only harness. Copied into an isolated Unity project, never into the game source.
using System;
using System.Collections;
using System.Collections.Generic;
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

namespace Roloc.Tests
{
    public class StoreCaptureTests
    {
        GameObject root;
        RolocGame game;
        Camera camera;
        Canvas canvas;
        RenderTexture target;
        Texture2D readback;
        string output;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        void Call(string method, params object[] args) => typeof(RolocGame).GetMethod(method, Private).Invoke(game, args);
        T Field<T>(string name) => (T)typeof(RolocGame).GetField(name, Private).GetValue(game);

        void SetTarget(int width, int height)
        {
            camera.targetTexture = null;
            if (target) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (readback) UnityEngine.Object.Destroy(readback);
            target = new RenderTexture(width, height, 24); target.Create();
            readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            var safe = Field<RectTransform>("safe"); safe.GetComponent<Roloc.Presentation.SafeArea>().enabled = false;
            safe.anchorMin = new Vector2(0, 34f / 956); safe.anchorMax = new Vector2(1, 1 - 62f / 956);
            Canvas.ForceUpdateCanvases();
        }
        void Capture(string relative)
        {
            Canvas.ForceUpdateCanvases(); Call("LayoutResults"); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            File.WriteAllBytes(Path.Combine(output, relative), readback.EncodeToPNG());
            RenderTexture.active = previous;
        }
        PuckView Active() => root.GetComponentsInChildren<PuckView>().Single(p => p.ColorIndex == game.Session.ActiveColor);
        Vector2 ScreenPoint(Vector3 world) => RectTransformUtility.WorldToScreenPoint(camera, world);
        Vector2 RingPoint(int color) => ScreenPoint(Field<RectTransform[]>("rings")[color].position);
        PointerEventData Pointer(PuckView puck) => new PointerEventData(EventSystem.current) {
            pointerId = 33, position = ScreenPoint(puck.Rect.position),
            pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() }
        };
        void ImmediateMatch()
        {
            if (game.Session.State == RoundState.Transition) Call("CompleteBoardTransition");
            Canvas.ForceUpdateCanvases();
            var p = Active(); var e = Pointer(p); p.OnPointerDown(e);
            e.position = RingPoint(p.ColorIndex); p.OnDrag(e); p.OnPointerUp(e);
            Assert.That(game.Session.LastResult, Is.EqualTo(MatchResult.Matched));
            Call("CompleteBoardTransition"); Call("RefreshBoard", 0f);
        }
        void Begin(string mode, string style, int seed)
        {
            game.ShowMenu(); game.Saves.Data.SelectedMode = mode; game.Saves.Data.SelectedBoard = style;
            game.RandomSeedOverride = seed; game.BeginRun(); Call("RefreshBoard", 0f);
        }
        [UnityTest]
        public IEnumerator CaptureStoreAssets()
        {
            output = Environment.GetEnvironmentVariable("RING_RUSH_CAPTURE_OUTPUT");
            Assert.That(output, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(output);
            root = new GameObject("Store capture"); root.SetActive(false); root.AddComponent<AudioListener>();
            game = root.AddComponent<RolocGame>();
            game.SaveDirectoryOverride = Path.Combine(Application.temporaryCachePath, "store-capture-" + Guid.NewGuid().ToString("N"));
            game.difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
            root.SetActive(true);
            game.Saves.Data.TutorialCompleted = true; game.Saves.Data.ChancesHintShown = true;
            game.Saves.Data.PerfectHintShown = true; game.Saves.Data.HapticsEnabled = false;
            camera = new GameObject("Store camera").AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(240,246,252,255); camera.transform.position = new Vector3(0,0,-10);
            canvas = root.GetComponentInChildren<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            SetTarget(1320,2868); yield return null;
            Begin("Flow", "Lively", 17); for(int n=0;n<6;n++) ImmediateMatch();
            yield return null; Capture("flow.png");
            var heroPuck=Active(); var heroPointer=Pointer(heroPuck); heroPuck.OnPointerDown(heroPointer);
            heroPointer.position=Vector2.Lerp(heroPointer.position,RingPoint(heroPuck.ColorIndex),.64f);
            heroPuck.OnDrag(heroPointer); Capture("hero.png"); heroPuck.CancelDrag();
            Begin("Rush", "Lively", 17); for(int n=0;n<8;n++) ImmediateMatch();
            yield return null; Capture("rush.png");
            Begin("Flow", "Lively", 17);
            int motionScore = 0;
            while (game.Session.Score < 500)
            {
                ImmediateMatch();
                if (VariationMotion.DriftsRings(game.Session.FlowMode)
                    && (VariationMotion.FloatsPucks(game.Session.FlowMode) || VariationMotion.OrbitsPucks(game.Session.FlowMode))
                    && game.Session.PaletteIndex >= 0) break;
            }
            Assert.That(game.Session.Score, Is.LessThan(500), "Capture a real combined movement episode with shifted colors.");
            motionScore = game.Session.Score;
            yield return new WaitForSecondsRealtime(.45f);
            Capture("moving.png");
            string motionMode = game.Session.FlowMode.ToString();
            int motionPalette = game.Session.PaletteIndex;
            Begin("Flow", "Lively", 17);
            for(int n=0;n<30;n++) ImmediateMatch();
            yield return new WaitForSecondsRealtime(.09f);
            Capture("perfect.png");
            while(game.Session.Score<145) ImmediateMatch();
            for(int n=0;n<3;n++) { game.Session.Drop(game.Session.ActiveColor,false); if(game.Session.State==RoundState.Transition) Call("CompleteBoardTransition"); }
            Call("FinishRun"); Capture("results.png");
            game.ShowMenu(); Capture("menu.png");
            game.Saves.Equip(CosmeticCategory.Puck,"glass"); game.Saves.Equip(CosmeticCategory.Trail,"ribbon");
            game.Saves.Equip(CosmeticCategory.Ring,"porcelain"); Call("ApplyAppearance"); Call("ShowCollection");
            yield return null; Capture("collection.png");
            Begin("Flow","Lively",17); for(int n=0;n<8;n++) ImmediateMatch();
            yield return null; Capture("cosmetics.png");
            game.Saves.Data.SymbolsEnabled=true; Call("ApplyAppearance"); Capture("symbols.png");
            game.ShowMenu(); Call("ShowSettings",false); yield return null; Capture("settings.png");
            File.WriteAllText(Path.Combine(output,"capture-info.txt"),
                "Screenshot-only refresh\nUnity " + Application.unityVersion + "\nVersion " + Application.version
                + "\nSeed 17\nMotion score " + motionScore + "\nMotion mode " + motionMode
                + "\nMotion palette " + motionPalette + "\nResolution 1320x2868\n"
                + "Real pointer handlers and gameplay rules; fresh temporary save; no video files modified.\n");
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(camera.gameObject);
            target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(readback);
            yield return null;
        }
    }
}

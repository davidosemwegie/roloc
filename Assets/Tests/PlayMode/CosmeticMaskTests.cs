using System.Collections;
using NUnit.Framework;
using Roloc.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Roloc.Tests
{
    public sealed class CosmeticMaskTests
    {
        GameObject root;
        Camera camera;
        RenderTexture target;
        Texture2D pixels;

        [TearDown]
        public void TearDown()
        {
            if (camera) camera.targetTexture = null;
            if (root) Object.DestroyImmediate(root);
            if (target) { target.Release(); Object.DestroyImmediate(target); }
            if (pixels) Object.DestroyImmediate(pixels);
        }

        [UnityTest]
        public IEnumerator GlassRespectsRectMaskAndCanvasGroupAlpha() => CheckMask(false);

        [UnityTest]
        public IEnumerator GlassRespectsStencilMaskAndCanvasGroupAlpha() => CheckMask(true);

        IEnumerator CheckMask(bool stencil)
        {
            root = new GameObject("Cosmetic mask test");
            var cameraObject = new GameObject("Mask camera");
            cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(256, 256, 24);
            target.Create();
            camera.targetTexture = target;
            pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);

            var canvasObject = new GameObject("Mask canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 31;
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var maskRect = MakeRect(canvas.transform, "Clip area", new Vector2(80, 80));
            var group = maskRect.gameObject.AddComponent<CanvasGroup>();
            Behaviour mask;
            if (stencil)
            {
                maskRect.gameObject.AddComponent<Image>();
                var stencilMask = maskRect.gameObject.AddComponent<Mask>();
                stencilMask.showMaskGraphic = false;
                mask = stencilMask;
            }
            else mask = maskRect.gameObject.AddComponent<RectMask2D>();
            var shapeRect = MakeRect(maskRect, "Glass disc", new Vector2(180, 180));
            var shape = shapeRect.gameObject.AddComponent<SoftShape>();
            shape.color = new Color(1, .4f, .1f);
            shape.Finish = "glass";
            shape.AnimateFinish = false;

            yield return null;
            Assert.That(shape.material.shader.name, Is.EqualTo("ROLOC/Cosmetic Finish"));
            ReadPixels();
            Assert.That(Sample(shapeRect, Vector2.zero).maxColorComponent, Is.GreaterThan(.15f), "Mask must retain the disc center.");
            Assert.That(Sample(shapeRect, new Vector2(60, 0)).maxColorComponent, Is.LessThan(.03f), "Disc must be clipped outside the mask.");

            // Prove the clipped sample lies on rendered glass when clipping is removed.
            mask.enabled = false;
            yield return null;
            ReadPixels();
            Assert.That(Sample(shapeRect, new Vector2(60, 0)).maxColorComponent, Is.GreaterThan(.15f));

            mask.enabled = true;
            group.alpha = 0;
            yield return null;
            ReadPixels();
            Assert.That(Sample(shapeRect, Vector2.zero).maxColorComponent, Is.LessThan(.03f), "CanvasGroup alpha must hide the material finish.");
        }

        static RectTransform MakeRect(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 31;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        void ReadPixels()
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        Color Sample(RectTransform rect, Vector2 localPoint)
        {
            var screen = camera.WorldToScreenPoint(rect.TransformPoint(localPoint));
            var color = pixels.GetPixel(Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y));
            color.a = 0;
            return color;
        }
    }
}
